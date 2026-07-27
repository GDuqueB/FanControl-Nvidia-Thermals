using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FanControlNvidiaThermals;

internal static class PciAddressResolver
{
    private const uint DIGCF_PRESENT = 0x2;
    private const uint SPDRP_HARDWAREID = 0x1;
    private const uint SPDRP_LOCATION_INFORMATION = 0xD;
    private const uint SPDRP_BUSNUMBER = 0x15;
    private const uint SPDRP_ADDRESS = 0x1C;
    private static readonly Guid DisplayClassGuid = new("{4d36e968-e325-11ce-bfc1-08002be10318}");
    private static readonly DevPropKey DEVPKEY_Device_BusNumber = new(
        new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"),
        23);
    private static readonly DevPropKey DEVPKEY_Device_Address = new(
        new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"),
        30);

    public static bool TryResolveNvidiaDisplayAdapter(uint busId, out uint deviceId, out uint functionId)
    {
        deviceId = 0;
        functionId = 0;

        Guid displayClassGuid = DisplayClassGuid;
        IntPtr deviceInfoSet = SetupDiGetClassDevs(ref displayClassGuid, null, IntPtr.Zero, DIGCF_PRESENT);
        if (deviceInfoSet == InvalidHandleValue)
            return false;

        try
        {
            SP_DEVINFO_DATA deviceInfo = new()
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            for (uint index = 0; SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo); index++)
            {
                if (!IsNvidiaDevice(deviceInfoSet, ref deviceInfo))
                    continue;

                if (TryGetUintProperty(deviceInfoSet, ref deviceInfo, DEVPKEY_Device_BusNumber, out uint candidateBus)
                    && candidateBus == busId
                    && TryGetUintProperty(deviceInfoSet, ref deviceInfo, DEVPKEY_Device_Address, out uint address))
                {
                    DecodePciSlotAddress(address, out deviceId, out functionId);
                    return true;
                }

                // The legacy SetupAPI registry properties are still populated
                // by NVIDIA's drivers even when the newer DEVPROPKEY calls are
                // unavailable to a process.
                if (TryGetRegistryPropertyUint(deviceInfoSet, ref deviceInfo, SPDRP_BUSNUMBER, out candidateBus)
                    && candidateBus == busId
                    && TryGetRegistryPropertyUint(deviceInfoSet, ref deviceInfo, SPDRP_ADDRESS, out address))
                {
                    DecodePciSlotAddress(address, out deviceId, out functionId);
                    return true;
                }

                // Some drivers do not publish the newer DEVPROPKEY values above,
                // but Windows still exposes the standard Device Manager location
                // string: "PCI bus X, device Y, function Z".
                if (TryGetPciLocation(deviceInfoSet, ref deviceInfo, out candidateBus, out uint candidateDevice, out uint candidateFunction)
                    && candidateBus == busId)
                {
                    deviceId = candidateDevice;
                    functionId = candidateFunction;
                    return true;
                }
            }

            return false;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static bool IsNvidiaDevice(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo)
    {
        string? hardwareIds = GetRegistryPropertyString(deviceInfoSet, ref deviceInfo, SPDRP_HARDWAREID);
        return hardwareIds?.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TryGetPciLocation(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        out uint busId,
        out uint deviceId,
        out uint functionId)
    {
        busId = 0;
        deviceId = 0;
        functionId = 0;

        string? location = GetRegistryPropertyString(deviceInfoSet, ref deviceInfo, SPDRP_LOCATION_INFORMATION);
        if (string.IsNullOrWhiteSpace(location))
            return false;

        Match match = Regex.Match(
            location,
            @"PCI\s+bus\s+(\d+),\s*device\s+(\d+),\s*function\s+(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success
            || !uint.TryParse(match.Groups[1].Value, out busId)
            || !uint.TryParse(match.Groups[2].Value, out deviceId)
            || !uint.TryParse(match.Groups[3].Value, out functionId))
        {
            return false;
        }

        return true;
    }

    private static void DecodePciSlotAddress(uint address, out uint deviceId, out uint functionId)
    {
        // Windows represents a PCI device address as PCI_SLOT_NUMBER:
        // 5 bits of device number followed by 3 bits of function number.
        deviceId = (address >> 3) & 0x1F;
        functionId = address & 0x07;
    }

    private static bool TryGetUintProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, DevPropKey key, out uint value)
    {
        value = 0;
        uint propertyType = 0;
        uint requiredSize = 0;

        if (!SetupDiGetDevicePropertyW(deviceInfoSet, ref deviceInfo, ref key, out propertyType, null, 0, out requiredSize, 0))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorInsufficientBuffer || requiredSize < sizeof(uint))
                return false;
        }

        byte[] buffer = new byte[requiredSize];
        if (!SetupDiGetDevicePropertyW(deviceInfoSet, ref deviceInfo, ref key, out propertyType, buffer, (uint)buffer.Length, out requiredSize, 0))
            return false;

        if (buffer.Length < sizeof(uint))
            return false;

        value = BitConverter.ToUInt32(buffer, 0);
        return true;
    }

    private static bool TryGetRegistryPropertyUint(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, uint property, out uint value)
    {
        value = 0;
        uint propertyType = 0;
        uint requiredSize = 0;

        if (!SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, property, out propertyType, null, 0, out requiredSize))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorInsufficientBuffer || requiredSize < sizeof(uint))
                return false;
        }

        byte[] buffer = new byte[requiredSize];
        if (!SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, property, out propertyType, buffer, (uint)buffer.Length, out requiredSize))
            return false;

        if (buffer.Length < sizeof(uint))
            return false;

        value = BitConverter.ToUInt32(buffer, 0);
        return true;
    }

    private static string? GetRegistryPropertyString(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, uint property)
    {
        uint propertyType = 0;
        uint requiredSize = 0;

        if (!SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, property, out propertyType, null, 0, out requiredSize))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorInsufficientBuffer || requiredSize == 0)
                return null;
        }

        byte[] buffer = new byte[requiredSize];
        if (!SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, property, out propertyType, buffer, (uint)buffer.Length, out requiredSize))
            return null;

        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDevicePropertyW(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        ref DevPropKey propertyKey,
        out uint propertyType,
        [Out] byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        [Out] byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public Guid Fmtid;
        public uint Pid;

        public DevPropKey(Guid fmtid, uint pid)
        {
            Fmtid = fmtid;
            Pid = pid;
        }
    }

    private static readonly IntPtr InvalidHandleValue = new(-1);
    private const int ErrorInsufficientBuffer = 122;
}
