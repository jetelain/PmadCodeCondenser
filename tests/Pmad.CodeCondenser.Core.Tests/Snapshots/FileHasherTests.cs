using System.Text;
using FluentAssertions;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Snapshots;

public class FileHasherTests : IDisposable
{
    private readonly FileHasher _hasher;
    private readonly string _tempDir;

    public FileHasherTests()
    {
        _hasher = new FileHasher();
        _tempDir = Path.Combine(Path.GetTempPath(), "FileHasherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ComputeHash_String_ReturnsExpectedSha256Hex()
    {
        var input = "Hello, World!";
        // Known SHA-256 for "Hello, World!"
        var expected = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f";

        var actual = _hasher.ComputeHash(input);

        actual.Should().Be(expected);
    }

    [Fact]
    public void ComputeHash_Bytes_MatchesStringHash()
    {
        var input = "Testing byte hash parity";
        var bytes = Encoding.UTF8.GetBytes(input);

        var stringHash = _hasher.ComputeHash(input);
        var bytesHash = _hasher.ComputeHash(bytes);

        bytesHash.Should().Be(stringHash);
    }

    [Fact]
    public void ComputeHash_Stream_MatchesStringHash()
    {
        var input = "Testing stream hash parity";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var stringHash = _hasher.ComputeHash(input);
        var streamHash = _hasher.ComputeHash(stream);

        streamHash.Should().Be(stringHash);
    }

    [Fact]
    public void ComputeFileHash_ReadsFileFromDiskCorrectly()
    {
        var content = "File content on disk";
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, content);

        var expected = _hasher.ComputeHash(content);
        var actual = _hasher.ComputeFileHash(filePath);

        actual.Should().Be(expected);
    }

    [Fact]
    public async Task ComputeFileHashAsync_ReadsFileFromDiskCorrectly()
    {
        var content = "Async file content on disk";
        var filePath = Path.Combine(_tempDir, "test_async.txt");
        await File.WriteAllTextAsync(filePath, content);

        var expected = _hasher.ComputeHash(content);
        var actual = await _hasher.ComputeFileHashAsync(filePath);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("C:/project/root", "C:/project/root/src/Foo.cs", "src/Foo.cs")]
    [InlineData("C:\\project\\root", "C:\\project\\root\\src\\Foo.cs", "src/Foo.cs")]
    [InlineData("C:\\project\\root", "src\\Foo.cs", "src/Foo.cs")]
    [InlineData("C:\\project\\root", "./src/Foo.cs", "src/Foo.cs")]
    [InlineData("C:\\project\\root", ".\\src\\Foo.cs", "src/Foo.cs")]
    public void NormalizeRelativePath_NormalizesSeparatorsAndRelativePrefix(string basePath, string inputPath, string expected)
    {
        var normalized = _hasher.NormalizeRelativePath(basePath, inputPath);

        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeRelativePath_WhenInvalidInput_ThrowsArgumentException(string? invalidPath)
    {
        var act1 = () => _hasher.NormalizeRelativePath("C:/root", invalidPath!);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => _hasher.NormalizeRelativePath(invalidPath!, "src/Foo.cs");
        act2.Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
