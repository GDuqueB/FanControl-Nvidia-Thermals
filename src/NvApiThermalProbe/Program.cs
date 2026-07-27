using System.Runtime.InteropServices;
using System.Text;

const uint NvApiInitializeId = 0x0150E828;
const uint NvApiEnumPhysicalGpusId = 0xE5AC921F;
const uint NvApiGpuGetFullNameId = 0xCEEE8E9F;
const uint NvApiGpuGetThermalSensorsId = 0x65FE3AAD;
const uint NvApiGpuGetThermalSettingsId = 0xE3640A56;
const int NvApiOk = 0;
const int MaxPhysicalGpus = 64;

T GetDelegate<T>(uint id) where T : Delegate
{
    IntPtr address = Native.NvApiQueryInterface(id);
    if (address == IntPtr.Zero)
        throw new EntryPointNotFoundException($"NVAPI interface 0x{id:X8} is unavailable.");
    return Marshal.GetDelegateForFunctionPointer<T>(address);
}

var initialize = GetDelegate<NvApiInitialize>(NvApiInitializeId);
var enumPhysicalGpus = GetDelegate<NvApiEnumPhysicalGpus>(NvApiEnumPhysicalGpusId);
var getFullName = GetDelegate<NvApiGpuGetFullName>(NvApiGpuGetFullNameId);
var getThermalSensors = GetDelegate<NvApiGpuGetThermalSensors>(NvApiGpuGetThermalSensorsId);
var getThermalSettings = GetDelegate<NvApiGpuGetThermalSettings>(NvApiGpuGetThermalSettingsId);

int status = initialize();
if (status != NvApiOk)
    throw new InvalidOperationException($"NvAPI_Initialize failed: {status}");

var handles = new IntPtr[MaxPhysicalGpus];
status = enumPhysicalGpus(handles, out int gpuCount);
if (status != NvApiOk)
    throw new InvalidOperationException($"NvAPI_EnumPhysicalGPUs failed: {status}");

Console.WriteLine($"Physical GPUs: {gpuCount}");
for (int gpu = 0; gpu < gpuCount; gpu++)
{
    var name = new StringBuilder(64);
    status = getFullName(handles[gpu], name);
    Console.WriteLine($"\nGPU {gpu}: {name} (name status {status})");

    uint supportedMask = 0;
    for (int bit = 0; bit < 32; bit++)
    {
        uint candidate = 1u << bit;
        NvThermalSensors single = CreateThermalSensors(candidate);
        int candidateStatus = getThermalSensors(handles[gpu], ref single);
        Console.WriteLine($"  mask bit {bit,2} (0x{candidate:X8}): status {candidateStatus}");
        if (candidateStatus != NvApiOk)
            break;
        supportedMask |= candidate;
    }

    Console.WriteLine($"  cumulative mask: 0x{supportedMask:X8}");
    NvThermalSensors sensors = CreateThermalSensors(supportedMask);
    status = getThermalSensors(handles[gpu], ref sensors);
    Console.WriteLine($"  cumulative status: {status}");
    if (status != NvApiOk)
        continue;

    for (int index = 0; index < sensors.Temperatures.Length; index++)
    {
        int raw = sensors.Temperatures[index];
        if (raw != 0)
            Console.WriteLine($"  temperature[{index,2}] raw={raw,8} (0x{raw:X8}) value={raw / 256.0,8:F3} C");
    }

    Console.WriteLine("\n  Ten-sample correlation (standard core / sensor[1] / sensor[2]):");
    for (int sample = 0; sample < 10; sample++)
    {
        NvThermalSettings settings = CreateThermalSettings();
        int settingsStatus = getThermalSettings(handles[gpu], 15, ref settings);
        sensors = CreateThermalSensors(supportedMask);
        int sensorsStatus = getThermalSensors(handles[gpu], ref sensors);
        string standardCore = settingsStatus == NvApiOk && settings.Count > 0
            ? $"{settings.Sensors[0].CurrentTemp:F3}"
            : $"N/A (status {settingsStatus})";
        string indexed = sensorsStatus == NvApiOk
            ? $"{sensors.Temperatures[1] / 256.0:F3} / {sensors.Temperatures[2] / 256.0:F3}"
            : $"N/A (status {sensorsStatus})";
        Console.WriteLine($"  {DateTime.Now:HH:mm:ss.fff}  {standardCore,12} / {indexed}");
        Thread.Sleep(1000);
    }
}

static NvThermalSensors CreateThermalSensors(uint mask)
{
    return new NvThermalSensors
    {
        Version = (uint)(Marshal.SizeOf<NvThermalSensors>() | (2 << 16)),
        Mask = mask,
        Reserved = new int[8],
        Temperatures = new int[32]
    };
}

static NvThermalSettings CreateThermalSettings()
{
    return new NvThermalSettings
    {
        Version = (uint)(Marshal.SizeOf<NvThermalSettings>() | (2 << 16)),
        Count = 3,
        Sensors = new NvSensor[3]
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct NvThermalSensors
{
    public uint Version;
    public uint Mask;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public int[] Reserved;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public int[] Temperatures;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct NvSensor
{
    public int Controller;
    public uint DefaultMinTemp;
    public uint DefaultMaxTemp;
    public uint CurrentTemp;
    public int Target;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct NvThermalSettings
{
    public uint Version;
    public uint Count;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public NvSensor[] Sensors;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NvApiInitialize();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NvApiEnumPhysicalGpus([Out] IntPtr[] handles, out int gpuCount);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NvApiGpuGetFullName(IntPtr gpuHandle, StringBuilder name);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NvApiGpuGetThermalSensors(IntPtr gpuHandle, ref NvThermalSensors sensors);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NvApiGpuGetThermalSettings(IntPtr gpuHandle, int sensorIndex, ref NvThermalSettings settings);

static class Native
{
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr NvApiQueryInterface(uint id);
}
