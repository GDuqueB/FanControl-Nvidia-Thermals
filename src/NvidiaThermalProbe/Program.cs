using System.ComponentModel;
using System.Runtime.InteropServices;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: NvidiaThermalProbe <signed PawnIO module>");
    return 2;
}

byte[] module = File.ReadAllBytes(args[0]);
IntPtr handle = IntPtr.Zero;

try
{
    ThrowIfFailed(PawnIo.Open(out handle), "pawnio_open");
    ThrowIfFailed(PawnIo.Load(handle, module, (nuint)module.Length), "pawnio_load");

    // PCI address reported by nvidia-smi: 0000:01:00.0.
    ulong[] bdf = [0x0001_0000];
    ThrowIfFailed(PawnIo.Execute(handle, "ioctl_init", bdf, 1, null, 0, out _), "ioctl_init");

    ulong[] identity = new ulong[4];
    ThrowIfFailed(PawnIo.Execute(handle, "ioctl_identity", null, 0, identity, 4, out nuint identitySize),
        "ioctl_identity");
    if (identitySize != 4 || (uint)identity[0] != 0x2B85_10DE || (uint)identity[1] != 0x0001_0000)
        throw new InvalidDataException("The PawnIO module did not bind to the expected RTX 5090 at 01:00.0.");

    ulong[] raw = new ulong[6];
    ThrowIfFailed(PawnIo.Execute(handle, "ioctl_read_thermal", null, 0, raw, 6, out nuint rawSize),
        "ioctl_read_thermal");
    if (rawSize != 6)
        throw new InvalidDataException($"Expected six thermal values, received {rawSize}.");

    PrintTemperature("Hot Spot", (uint)raw[0]);
    for (int channel = 1; channel < raw.Length; channel++)
        PrintTemperature($"Thermal #{channel + 1}", (uint)raw[channel]);
    return 0;
}
finally
{
    if (handle != IntPtr.Zero)
        PawnIo.Close(handle);
}

static void PrintTemperature(string name, uint raw)
{
    const uint ValidSample = 1u << 30;
    if ((raw & ValidSample) == 0)
        throw new InvalidDataException($"{name}: invalid raw value 0x{raw:X8}.");

    float temperature = (raw & 0xFFFF) / 256.0f;
    Console.WriteLine($"{name,-12} raw=0x{raw:X8} temperature={temperature:F3} °C");
}

static void ThrowIfFailed(int hresult, string operation)
{
    if (hresult < 0)
        throw new Win32Exception(hresult, $"{operation} failed with HRESULT 0x{hresult:X8}");
}

internal static partial class PawnIo
{
    private const string Library = @"C:\Program Files\PawnIO\PawnIOLib.dll";

    [LibraryImport(Library, EntryPoint = "pawnio_open")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Open(out IntPtr handle);

    [LibraryImport(Library, EntryPoint = "pawnio_load")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Load(IntPtr handle, [In] byte[] blob, nuint size);

    [LibraryImport(Library, EntryPoint = "pawnio_execute", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Execute(
        IntPtr handle,
        string name,
        [In] ulong[]? input,
        nuint inputSize,
        [Out] ulong[]? output,
        nuint outputSize,
        out nuint returnSize);

    [LibraryImport(Library, EntryPoint = "pawnio_close")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static partial int Close(IntPtr handle);
}
