using AwesomeAssertions;

namespace Pmad.CodeCondenser.Cli.Tests;

public class SubMethodCliTests : IDisposable
{
    private readonly string _testDirectory;

    public SubMethodCliTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "SubMethodCliTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task RunAsync_WithSubMethodClones_ToleranceExceeded_ExitsWithCodeTwo()
    {
        var file1 = Path.Combine(_testDirectory, "ServiceA.cs");
        var file2 = Path.Combine(_testDirectory, "ServiceB.cs");

        var code1 = @"
namespace App;
public class ServiceA
{
    public void ProcessA(int id)
    {
        int a = 1;
        string b = ""str"";
        bool c = true;
        double d = 3.14;
        char e = 'x';
        long f = 100L;
        short g = 2;
        byte h = 3;
        float i = 4.5f;
        decimal j = 5.5m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void ProcessB(int id)
    {
        throw new System.InvalidOperationException();
        int a = 10;
        string b = ""other"";
        bool c = false;
        double d = 2.71;
        char e = 'y';
        long f = 200L;
        short g = 4;
        byte h = 6;
        float i = 9.9f;
        decimal j = 7.7m;
    }
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--sub-method-clones", "--threshold-submethod-statements", "10", "--duplication-tolerance", "0", "--no-color", "--no-comments"],
            output,
            error);

        exitCode.Should().Be(2);
        var stdOut = output.ToString();
        stdOut.Should().Contain("[AI AGENT ACTION REQUIRED]");
        stdOut.Should().Contain("[Sub-Method, 10 consecutive statements]");
    }

    [Fact]
    public async Task RunAsync_WithSubMethodClones_ToleranceNotExceeded_ExitsZero()
    {
        var file1 = Path.Combine(_testDirectory, "ServiceA.cs");
        var file2 = Path.Combine(_testDirectory, "ServiceB.cs");

        var code1 = @"
namespace App;
public class ServiceA
{
    public void ProcessA(int id)
    {
        int a = 1;
        string b = ""str"";
        bool c = true;
        double d = 3.14;
        char e = 'x';
        long f = 100L;
        short g = 2;
        byte h = 3;
        float i = 4.5f;
        decimal j = 5.5m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void ProcessB(int id)
    {
        throw new System.InvalidOperationException();
        int a = 10;
        string b = ""other"";
        bool c = false;
        double d = 2.71;
        char e = 'y';
        long f = 200L;
        short g = 4;
        byte h = 6;
        float i = 9.9f;
        decimal j = 7.7m;
    }
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--sub-method-clones", "--threshold-submethod-statements", "10", "--duplication-tolerance", "50", "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        var stdOut = output.ToString();
        stdOut.Should().Contain("Found 1 Sub-Method Duplication Cluster(s):");
        stdOut.Should().NotContain("Tolerance check failed");
    }

    [Fact]
    public async Task RunAsync_WithoutSubMethodClonesFlag_DoesNotRunSubMethodDetection()
    {
        var file1 = Path.Combine(_testDirectory, "ServiceA.cs");
        var file2 = Path.Combine(_testDirectory, "ServiceB.cs");

        var code1 = @"
namespace App;
public class ServiceA
{
    public void ProcessA(int id)
    {
        int a = 1;
        string b = ""str"";
        bool c = true;
        double d = 3.14;
        char e = 'x';
        long f = 100L;
        short g = 2;
        byte h = 3;
        float i = 4.5f;
        decimal j = 5.5m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void ProcessB(int id)
    {
        throw new System.InvalidOperationException();
        int a = 10;
        string b = ""other"";
        bool c = false;
        double d = 2.71;
        char e = 'y';
        long f = 200L;
        short g = 4;
        byte h = 6;
        float i = 9.9f;
        decimal j = 7.7m;
    }
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--duplication-tolerance", "0", "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        var stdOut = output.ToString();
        stdOut.Should().NotContain("Sub-Method Duplication Cluster");
    }
}
