namespace WildsDeck.Memory;

public sealed class MemoryAddressResolver(ProcessMemoryReader memory, AddressMap map, nint moduleBase)
{
    public nint ModuleAddress(string addressSymbol) => checked(moduleBase + (nint)map.GetAddress(addressSymbol));

    public nint ResolveIndirect(string addressSymbol, string offsetSymbol) =>
        ResolveIndirect(ModuleAddress(addressSymbol), map.GetOffsets(offsetSymbol));

    public nint ResolveIndirect(nint address, IReadOnlyList<int> offsets)
    {
        foreach (int offset in offsets)
        {
            address = memory.Read<nint>(address);
            EnsureValid(address);
            address = checked(address + offset);
        }

        return address;
    }

    public nint ResolvePointerPath(nint address, string offsetSymbol) =>
        ResolvePointerPath(address, map.GetOffsets(offsetSymbol));

    public nint ResolvePointerPath(nint address, IReadOnlyList<int> offsets)
    {
        foreach (int offset in offsets)
        {
            address = memory.Read<nint>(checked(address + offset));
            EnsureValid(address);
        }

        return address;
    }

    public static bool IsValidPointer(nint pointer)
    {
        long value = pointer;
        return value >= 0x10000 && value <= 0x0000_7FFF_FFFF_FFFF;
    }

    public static void EnsureValid(nint pointer)
    {
        if (!IsValidPointer(pointer))
            throw new InvalidDataException($"Null or invalid pointer: 0x{pointer:X}.");
    }
}

