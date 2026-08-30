using WildsDeck.Memory;

namespace WildsDeck.Tests;

public sealed class AddressMapTests
{
    [Fact]
    public void ParsesAddressesAndPointerOffsets()
    {
        AddressMap map = AddressMap.Parse("""
            # comment
            Address Game::QuestManager 0x12345678
            Offset Monster::Health 0x30,0x38,0x10
            """);

        Assert.Equal(0x12345678, map.GetAddress("Game::QuestManager"));
        Assert.Equal([0x30, 0x38, 0x10], map.GetOffsets("Monster::Health"));
    }

    [Theory]
    [InlineData("0x10", 16)]
    [InlineData("FF", 255)]
    [InlineData("0X12345678", 0x12345678)]
    public void ParsesHexadecimalValues(string raw, long expected) => Assert.Equal(expected, AddressMap.ParseHex(raw));

    [Fact]
    public void MissingSymbolThrowsWithItsName()
    {
        AddressMap map = AddressMap.Parse("Address Game::QuestManager 0x1");
        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => map.GetOffsets("Monster::Health"));
        Assert.Contains("Monster::Health", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateSymbolIsRejected()
    {
        Assert.Throws<FormatException>(() => AddressMap.Parse("""
            Address Game::QuestManager 0x1
            Address Game::QuestManager 0x2
            """));
    }
}

