using System.Reflection;
using System.Runtime.InteropServices;

namespace FanControlNvidiaThermals;

internal sealed class PawnIoHotspotReader : IDisposable
{
    private const int ValidSampleBit = 1 << 30;
    private const string ModuleName = "Nvidia";
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "FanControl.NvidiaThermals.log");

    private IntPtr _handle;
    private bool _available;
    private readonly ulong[] _pciAddress;

    public PawnIoHotspotReader(uint busId)
    {
        _handle = IntPtr.Zero;
        if (!PciAddressResolver.TryResolveNvidiaDisplayAdapter(busId, out uint deviceId, out uint functionId))
        {
            // Fall back to the desktop default when Windows does not expose a
            // matching display adapter. This preserves compatibility with the
            // original implementation while keeping the resolved path preferred.
            deviceId = 0;
            functionId = 0;
            Log($"PCI resolver fallback for bus {busId:X2}; assuming device 00 function 0");
        }
        else
        {
            Log($"PCI resolver found PCI {busId:X2}:{deviceId:X2}.{functionId}");
        }

        _pciAddress = [busId, deviceId, functionId];
        _available = TryOpen();
    }

    public float? ReadTemperature()
    {
        if (!_available || _handle == IntPtr.Zero)
            return null;

        ulong[] raw = new ulong[6];
        int status = PawnIo.Execute(
            _handle,
            "ioctl_read_thermal_registers",
            _pciAddress,
            (nuint)_pciAddress.Length,
            raw,
            (nuint)raw.Length,
            out nuint rawSize);
        if (status < 0 || rawSize == 0)
        {
            Log($"ioctl_read_thermal_registers failed: status=0x{status:X8} size={rawSize} PCI={_pciAddress[0]:X2}:{_pciAddress[1]:X2}.{_pciAddress[2]}");
            return null;
        }

        // HWMonitor 1.65.1's corrected Hot Spot tracks channel 3. Channel 4
        // mirrors it on the tested RTX 5090, but channel 3 is used as the
        // canonical register in the plugin.
        uint sample = (uint)raw[3];
        if ((sample & ValidSampleBit) == 0)
        {
            Log($"ioctl_read_thermal returned invalid sample: 0x{sample:X8}");
            return null;
        }

        return (sample & 0xFFFF) / 256.0f;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            PawnIo.Close(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private bool TryOpen()
    {
        try
        {
            int openStatus = PawnIo.Open(out _handle);
            if (openStatus < 0)
            {
                Log($"pawnio_open failed: status=0x{openStatus:X8}");
                return false;
            }

            (byte[] module, string? modulePath) = LoadModuleBlob();
            if (module.Length == 0)
            {
                Log("module blob not found");
                return false;
            }

            Log($"loading module from '{modulePath}' ({module.Length} bytes)");
            int loadStatus = PawnIo.Load(_handle, module, (nuint)module.Length);
            if (loadStatus < 0)
            {
                Log($"pawnio_load failed: status=0x{loadStatus:X8}");
                return false;
            }

            Log($"module loaded; hotspot reads will target PCI {_pciAddress[0]:X2}:{_pciAddress[1]:X2}.{_pciAddress[2]}");

            return true;
        }
        catch (Exception ex)
        {
            Log($"TryOpen threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static (byte[] Blob, string? Path) LoadModuleBlob()
    {
        foreach (string candidate in CandidatePaths())
        {
            if (File.Exists(candidate))
                return (File.ReadAllBytes(candidate), candidate);
        }

        return ([], null);
    }

    private static IEnumerable<string> CandidatePaths()
    {
        string? env = Environment.GetEnvironmentVariable("FANCONTROL_PAWNIO_MODULE");
        if (!string.IsNullOrWhiteSpace(env))
            yield return env;

        string baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, ModuleName + ".bin");
        yield return Path.Combine(baseDirectory, ModuleName + ".amx");
        yield return Path.Combine(baseDirectory, "modules", ModuleName + ".bin");
        yield return Path.Combine(baseDirectory, "modules", ModuleName + ".amx");

        string? assemblyDirectory = Path.GetDirectoryName(typeof(PawnIoHotspotReader).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield return Path.Combine(assemblyDirectory, ModuleName + ".bin");
            yield return Path.Combine(assemblyDirectory, ModuleName + ".amx");
            yield return Path.Combine(assemblyDirectory, "modules", ModuleName + ".bin");
            yield return Path.Combine(assemblyDirectory, "modules", ModuleName + ".amx");
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string fanControlDir = Path.Combine(programFiles, "FanControl");
        yield return Path.Combine(fanControlDir, ModuleName + ".bin");
        yield return Path.Combine(fanControlDir, ModuleName + ".amx");

        string pawnIoDir = Path.Combine(programFiles, "PawnIO", "Modules");
        yield return Path.Combine(pawnIoDir, ModuleName + ".bin");
        yield return Path.Combine(pawnIoDir, ModuleName + ".amx");
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}

internal static partial class PawnIo
{
    private const string LibraryName = "PawnIOLib.dll";

    static PawnIo()
    {
        NativeLibrary.SetDllImportResolver(typeof(PawnIo).Assembly, Resolve);
    }

    [LibraryImport(LibraryName, EntryPoint = "pawnio_open")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Open(out IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "pawnio_load")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Load(IntPtr handle, [In] byte[] blob, nuint size);

    [LibraryImport(LibraryName, EntryPoint = "pawnio_execute", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Execute(
        IntPtr handle,
        string name,
        [In] ulong[]? input,
        nuint inputSize,
        [Out] ulong[]? output,
        nuint outputSize,
        out nuint returnSize);

    [LibraryImport(LibraryName, EntryPoint = "pawnio_close")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Close(IntPtr handle);

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (string candidate in CandidateLibraryPaths())
        {
            if (File.Exists(candidate))
                return NativeLibrary.Load(candidate);
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> CandidateLibraryPaths()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO", LibraryName);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PawnIO", LibraryName);
        yield return LibraryName;
    }
}
