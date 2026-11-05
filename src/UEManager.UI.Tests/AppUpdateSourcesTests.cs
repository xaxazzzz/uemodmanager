using UEManager.UI.Services;
using Xunit;

namespace UEManager.UI.Tests;

public class AppUpdateSourcesTests
{
    [Fact]
    public void Modified_build_cannot_be_overwritten_from_the_official_binary_channel()
    {
        Assert.Empty(AppInfo.LatestReleaseUrls);
    }
}
