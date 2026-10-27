using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Cli.Tests;

public class CliSmokeTests
{
    [Fact]
    public void CliCanReferenceCoreAndInfrastructure()
    {
        var settings = new CodeCondenserSettings();
        settings.Should().NotBeNull();
        settings.CommentLengthThreshold.Should().Be(100);
    }
}
