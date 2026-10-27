using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Snapshots;

public class MethodKeyHelperTests
{
    [Fact]
    public void BuildKey_FromPathAndSignature_NormalizesSlashesAndCombinesWithSeparator()
    {
        var key = MethodKeyHelper.BuildKey("src\\Services\\OrderService.cs", "void ProcessOrder(int id)");

        key.Should().Be("src/Services/OrderService.cs::void ProcessOrder(int id)");
    }

    [Fact]
    public void BuildKey_FromMethodCloneInfo_UsesSignatureAndFilePath()
    {
        var method = new MethodCloneInfo(
            FilePath: "src\\Controllers\\ItemsController.cs",
            MethodName: "GetItem",
            StartLine: 10,
            EndLine: 25,
            StatementCount: 12,
            NormalizedHash: "abc123hash",
            Signature: "public IActionResult GetItem(int id)");

        var key = MethodKeyHelper.BuildKey(method);

        key.Should().Be("src/Controllers/ItemsController.cs::public IActionResult GetItem(int id)");
    }

    [Fact]
    public void TryParseKey_WhenKeyIsValid_ExtractsFilePathAndMethodIdentifier()
    {
        var key = "src/Services/OrderService.cs::void ProcessOrder()";

        var success = MethodKeyHelper.TryParseKey(key, out var filePath, out var methodId);

        success.Should().BeTrue();
        filePath.Should().Be("src/Services/OrderService.cs");
        methodId.Should().Be("void ProcessOrder()");
    }

    [Fact]
    public void TryParseKey_WhenNoSeparator_ReturnsFalse()
    {
        var key = "src/Services/OrderService.cs";

        var success = MethodKeyHelper.TryParseKey(key, out var filePath, out var methodId);

        success.Should().BeFalse();
        filePath.Should().BeEmpty();
        methodId.Should().BeEmpty();
    }

    [Fact]
    public void GetFilePath_ExtractsNormalizedFilePath()
    {
        var key = "src/Services/OrderService.cs::ProcessOrder";

        var filePath = MethodKeyHelper.GetFilePath(key);

        filePath.Should().Be("src/Services/OrderService.cs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildKey_WhenInvalidArguments_ThrowsArgumentException(string? invalid)
    {
        var act1 = () => MethodKeyHelper.BuildKey(invalid!, "void Foo()");
        act1.Should().Throw<ArgumentException>();

        var act2 = () => MethodKeyHelper.BuildKey("src/Foo.cs", invalid!);
        act2.Should().Throw<ArgumentException>();
    }
}
