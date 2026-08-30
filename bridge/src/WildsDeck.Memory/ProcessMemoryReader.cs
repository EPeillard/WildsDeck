using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WildsDeck.Memory;

public sealed class ProcessMemoryReader : IDisposable
{
    [Flags]
    private enum ProcessAccess : uint
    {
        VmRead = 0x0010,
        QueryInformation = 0x0400
    }

    private readonly nint _handle;
    private bool _disposed;

    private ProcessMemoryReader(nint handle) => _handle = handle;

    public static ProcessMemoryReader Open(int processId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Monster Hunter Wilds process memory is available on Windows only.");

        nint handle = OpenProcess(ProcessAccess.VmRead | ProcessAccess.QueryInformation, false, processId);
        if (handle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the game process for read-only telemetry.");

        return new ProcessMemoryReader(handle);
    }

    public byte[] ReadBytes(nint address, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!MemoryAddressResolver.IsValidPointer(address) || count < 0)
            throw new InvalidDataException($"Invalid memory read at 0x{address:X} ({count} bytes).");

        byte[] buffer = new byte[count];
        if (!ReadProcessMemory(_handle, address, buffer, count, out nint read) || read != count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"ReadProcessMemory failed at 0x{address:X} ({read}/{count} bytes).");

        return buffer;
    }

    public T Read<T>(nint address) where T : unmanaged
    {
        byte[] buffer = ReadBytes(address, Marshal.SizeOf<T>());
        return MemoryMarshal.Read<T>(buffer);
    }

    public T[] ReadArray<T>(nint address, int count) where T : unmanaged
    {
        if (count <= 0)
            return [];

        int elementSize = Marshal.SizeOf<T>();
        byte[] buffer = ReadBytes(address, checked(elementSize * count));
        T[] result = new T[count];
        MemoryMarshal.Cast<byte, T>(buffer).CopyTo(result);
        return result;
    }

    public string ReadWildsString(nint address, int maximumCharacters = 64)
    {
        int length = Math.Clamp(Read<int>(address + 0x10), 0, maximumCharacters);
        if (length == 0)
            return string.Empty;

        return Encoding.Unicode.GetString(ReadBytes(address + 0x14, length * 2)).TrimEnd('\0');
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(ProcessAccess desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint baseAddress, [Out] byte[] buffer, int size, out nint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

