using FanControl.Plugins;

namespace FanControlNvidiaThermals;

public sealed class Plugin : IPlugin, IPlugin2
{
    private readonly List<NvidiaGpuContext> _gpus = [];

    public string Name => "NVIDIA Thermal Bridge";

    public void Initialize()
    {
        NvApi.Initialize();
    }

    public void Load(IPluginSensorsContainer container)
    {
        if (!NvApi.IsAvailable)
            return;

        foreach (NvidiaGpuContext gpu in NvidiaGpuContext.Enumerate())
        {
            _gpus.Add(gpu);
            container.TempSensors.Add(gpu.CoreSensor);
            container.TempSensors.Add(gpu.MemorySensor);
            container.TempSensors.Add(gpu.HotspotSensor);
        }
    }

    public void Update()
    {
        foreach (NvidiaGpuContext gpu in _gpus)
            gpu.Update();
    }

    public void Close()
    {
        foreach (NvidiaGpuContext gpu in _gpus)
            gpu.Dispose();

        _gpus.Clear();
    }
}

internal sealed class NvidiaGpuContext : IDisposable
{
    private readonly NvApi.NvPhysicalGpuHandle _handle;
    private readonly int _adapterIndex;
    private readonly uint _busId;
    private readonly string _name;
    private readonly bool _isBlackwell;
    private readonly uint _thermalSensorsMask;
    private readonly PawnIoHotspotReader? _hotspotReader;

    public NvidiaSensor CoreSensor { get; }
    public NvidiaSensor MemorySensor { get; }
    public NvidiaSensor HotspotSensor { get; }

    private NvidiaGpuContext(int adapterIndex, NvApi.NvPhysicalGpuHandle handle, string name, uint busId)
    {
        _adapterIndex = adapterIndex;
        _handle = handle;
        _busId = busId;
        _name = name;
        _isBlackwell = name.StartsWith("NVIDIA GeForce RTX 50", StringComparison.OrdinalIgnoreCase);

        _thermalSensorsMask = ProbeThermalSensorsMask();
        _hotspotReader = _isBlackwell ? new PawnIoHotspotReader(_busId) : null;

        string prefix = $"{_name} [{_adapterIndex}]";
        CoreSensor = new NvidiaSensor(
            $"{prefix} - GPU Core",
            $"{_busId:X2}-core",
            ReadCore);
        MemorySensor = new NvidiaSensor(
            $"{prefix} - GPU Memory Junction",
            $"{_busId:X2}-memory",
            ReadMemory);
        HotspotSensor = new NvidiaSensor(
            $"{prefix} - GPU Hot Spot",
            $"{_busId:X2}-hotspot",
            ReadHotspot);
    }

    public static IEnumerable<NvidiaGpuContext> Enumerate()
    {
        var enumPhysicalGpus = NvApi.EnumPhysicalGPUs;
        var getBusId = NvApi.GetBusId;
        if (enumPhysicalGpus is null || getBusId is null)
            yield break;

        NvApi.NvPhysicalGpuHandle[] handles = new NvApi.NvPhysicalGpuHandle[NvApi.MaxPhysicalGpus];
        if (enumPhysicalGpus(handles, out int gpuCount) != 0)
            yield break;

        for (int index = 0; index < gpuCount; index++)
        {
            NvApi.NvPhysicalGpuHandle handle = handles[index];
            string? name = NvApi.GetGpuName(handle);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (getBusId(handle, out uint busId) != 0)
                continue;

            yield return new NvidiaGpuContext(index, handle, name!, busId);
        }
    }

    public void Update()
    {
        CoreSensor.Update();
        MemorySensor.Update();
        HotspotSensor.Update();
    }

    private float? ReadCore()
    {
        if (_isBlackwell && _thermalSensorsMask > 0 && NvApi.GetThermalSensors is not null)
        {
            NvApi.NvThermalSensors sensors = NvApi.CreateThermalSensors(_thermalSensorsMask);
            if (NvApi.GetThermalSensors(_handle, ref sensors) == 0)
                return DecodeTemperature(sensors.Temperatures[1]);
        }

        if (NvApi.GetThermalSettings is null)
            return null;

        NvApi.NvThermalSettings settings = NvApi.CreateThermalSettings();
        if (NvApi.GetThermalSettings(_handle, (int)NvApi.NvThermalTarget.All, ref settings) != 0)
            return null;

        for (int i = 0; i < settings.Count; i++)
        {
            if (settings.Sensor[i].Target == NvApi.NvThermalTarget.Gpu)
                return settings.Sensor[i].CurrentTemp;
        }

        return null;
    }

    private float? ReadMemory()
    {
        if (_thermalSensorsMask > 0 && NvApi.GetThermalSensors is not null)
        {
            NvApi.NvThermalSensors sensors = NvApi.CreateThermalSensors(_thermalSensorsMask);
            if (NvApi.GetThermalSensors(_handle, ref sensors) == 0)
            {
                if (_isBlackwell)
                    return DecodeTemperature(sensors.Temperatures[2]);

                if (_name.StartsWith("NVIDIA GeForce RTX 40", StringComparison.OrdinalIgnoreCase))
                    return DecodeTemperature(sensors.Temperatures[7]);

                return DecodeTemperature(sensors.Temperatures[9]);
            }
        }

        if (NvApi.GetThermalSettings is null)
            return null;

        NvApi.NvThermalSettings settings = NvApi.CreateThermalSettings();
        if (NvApi.GetThermalSettings(_handle, (int)NvApi.NvThermalTarget.All, ref settings) != 0)
            return null;

        for (int i = 0; i < settings.Count; i++)
        {
            if (settings.Sensor[i].Target == NvApi.NvThermalTarget.Memory)
                return settings.Sensor[i].CurrentTemp;
        }

        return null;
    }

    private float? ReadHotspot()
    {
        if (_isBlackwell)
            return _hotspotReader?.ReadTemperature();

        if (_thermalSensorsMask == 0 || NvApi.GetThermalSensors is null)
            return null;

        NvApi.NvThermalSensors sensors = NvApi.CreateThermalSensors(_thermalSensorsMask);
        if (NvApi.GetThermalSensors(_handle, ref sensors) != 0)
            return null;

        return DecodeTemperature(sensors.Temperatures[1]);
    }

    private uint ProbeThermalSensorsMask()
    {
        if (NvApi.GetThermalSensors is null)
            return 0;

        uint mask = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            uint candidate = mask | (1u << bit);
            NvApi.NvThermalSensors probe = NvApi.CreateThermalSensors(candidate);
            if (NvApi.GetThermalSensors(_handle, ref probe) == 0)
            {
                mask = candidate;
                continue;
            }

            break;
        }

        return mask;
    }

    private static float DecodeTemperature(int raw)
    {
        return (raw & 0xFFFF) / 256.0f;
    }

    public void Dispose()
    {
        _hotspotReader?.Dispose();
    }
}

internal sealed class NvidiaSensor : IPluginSensor
{
    private readonly Func<float?> _readValue;

    public NvidiaSensor(string name, string id, Func<float?> readValue)
    {
        Name = name;
        Id = id;
        _readValue = readValue;
    }

    public string Id { get; }

    public string Name { get; }

    public float? Value { get; private set; }

    public void Update()
    {
        try
        {
            Value = _readValue();
        }
        catch
        {
            Value = null;
        }
    }
}
