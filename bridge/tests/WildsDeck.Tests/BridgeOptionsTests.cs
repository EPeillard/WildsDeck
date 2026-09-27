using WildsDeck.Bridge;

namespace WildsDeck.Tests;

public class BridgeOptionsTests
{
    [Fact]
    public void Load_ParsesParentPid()
    {
        BridgeOptions options = BridgeOptions.Load(["--parent-pid", "4242"]);
        Assert.Equal(4242, options.ParentPid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-pid")]
    public void Load_IgnoresInvalidParentPid(string value)
    {
        BridgeOptions options = BridgeOptions.Load(["--parent-pid", value]);
        Assert.Null(options.ParentPid);
    }
}
