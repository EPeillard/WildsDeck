namespace WildsDeck.Memory;

public sealed record PointerPath(string Symbol, IReadOnlyList<int> Offsets)
{
    public static PointerPath FromMap(AddressMap map, string symbol) => new(symbol, map.GetOffsets(symbol));
}

