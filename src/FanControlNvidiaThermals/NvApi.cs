using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FanControlNvidiaThermals;

internal static class NvApi
{
    private const uint NvApiInitializeId = 0x0150E828;
    private const uint NvApiEnumPhysicalGpusId = 0xE5AC921F;
    private const uint NvApiGpuGetFullNameId = 0xCEEE8E9F;
    private const uint NvApiGpuGetBusIdId = 0x1BE0B8E5;
    private const uint NvApiGpuGetThermalSensorsId = 0x65FE3AAD;
    private const uint NvApiGpuGetThermalSettingsId = 0xE3640A56;

    private const int NvApiOk = 0;

    public const int MaxPhysicalGpus = 64;

    private static NvApiInitializeDelegate? _initialize;
    private static NvApiEnumPhysicalGpusDelegate? _enumPhysicalGpus;
    private static NvApiGpuGetFullNameDelegate? _getFullName;
    private static NvApiGpuGetBusIdDelegate? _getBusId;
    private static NvApiGpuGetThermalSensorsDelegate? _getThermalSensors;
    private static NvApiGpuGetThermalSettingsDelegate? _getThermalSettings;

    public static bool IsAvailable { get; private set; }

    public static NvApiEnumPhysicalGpusDelegate? EnumPhysicalGPUs => _enumPhysicalGpus;
    public static NvApiGpuGetFullNameDelegate? GetFullName => _getFullName;
    public static NvApiGpuGetBusIdDelegate? GetBusId => _getBusId;
    public static NvApiGpuGetThermalSensorsDelegate? GetThermalSensors => _getThermalSensors;
    public static NvApiGpuGetThermalSettingsDelegate? GetThermalSettings => _getThermalSettings;

    public static void Initialize()
    {
        try
        {
            _initialize = GetDelegate<NvApiInitializeDelegate>(NvApiInitializeId);
            _enumPhysicalGpus = GetDelegate<NvApiEnumPhysicalGpusDelegate>(NvApiEnumPhysicalGpusId);
            _getFullName = GetDelegate<NvApiGpuGetFullNameDelegate>(NvApiGpuGetFullNameId);
            _getBusId = GetDelegate<NvApiGpuGetBusIdDelegate>(NvApiGpuGetBusIdId);
            _getThermalSensors = GetDelegate<NvApiGpuGetThermalSensorsDelegate>(NvApiGpuGetThermalSensorsId);
            _getThermalSettings = GetDelegate<NvApiGpuGetThermalSettingsDelegate>(NvApiGpuGetThermalSettingsId);
        }
        catch
        {
            IsAvailable = false;
            return;
        }

        if (_initialize is null || _enumPhysicalGpus is null || _getFullName is null || _getBusId is null)
        {
            IsAvailable = false;
            return;
        }

        IsAvailable = _initialize() == NvApiOk;
    }

    public static string? GetGpuName(NvPhysicalGpuHandle handle)
    {
        if (_getFullName is null)
            return null;

        StringBuilder builder = new(64);
        return _getFullName(handle, builder) == NvApiOk ? builder.ToString() : null;
    }

    private static T GetDelegate<T>(uint id) where T : Delegate
    {
        IntPtr address = Native.NvApiQueryInterface(id);
        if (address == IntPtr.Zero)
            throw new EntryPointNotFoundException($"NVAPI interface 0x{id:X8} is unavailable.");

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiEnumPhysicalGpusDelegate([Out] NvPhysicalGpuHandle[] handles, out int gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiGpuGetFullNameDelegate(NvPhysicalGpuHandle handle, StringBuilder name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiGpuGetBusIdDelegate(NvPhysicalGpuHandle handle, out uint busId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiGpuGetThermalSensorsDelegate(NvPhysicalGpuHandle handle, ref NvThermalSensors sensors);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int NvApiGpuGetThermalSettingsDelegate(NvPhysicalGpuHandle handle, int sensorIndex, ref NvThermalSettings settings);

    public enum NvStatus
    {
        OK = 0
    }

    public enum NvThermalTarget
    {
        None = 0,
        Gpu = 1,
        Memory = 2,
        PowerSupply = 4,
        Board = 8,
        VisualComputingBoard = 9,
        VisualComputingInlet = 10,
        VisualComputingOutlet = 11,
        All = 15,
        Unknown = -1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvThermalSensors
    {
        public uint Version;
        public uint Mask;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public int[] Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public int[] Temperatures;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvSensor
    {
        public int Controller;
        public uint DefaultMinTemp;
        public uint DefaultMaxTemp;
        public uint CurrentTemp;
        public NvThermalTarget Target;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NvThermalSettings
    {
        public uint Version;
        public uint Count;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public NvSensor[] Sensor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NvPhysicalGpuHandle
    {
        private readonly IntPtr _ptr;
    }

    public static NvThermalSensors CreateThermalSensors(uint mask)
    {
        return new NvThermalSensors
        {
            Version = (uint)(Marshal.SizeOf<NvThermalSensors>() | (2 << 16)),
            Mask = mask,
            Reserved = new int[8],
            Temperatures = new int[32]
        };
    }

    public static NvThermalSettings CreateThermalSettings()
    {
        return new NvThermalSettings
        {
            Version = (uint)(Marshal.SizeOf<NvThermalSettings>() | (2 << 16)),
            Count = 3,
            Sensor = new NvSensor[3]
        };
    }

    private static class Native
    {
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr NvApiQueryInterface(uint id);
    }
}
