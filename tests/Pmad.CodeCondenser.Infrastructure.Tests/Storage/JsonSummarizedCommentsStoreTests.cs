using AwesomeAssertions;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Storage;

public class JsonSummarizedCommentsStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _commentsPath;
    private readonly JsonSummarizedCommentsStore _store;

    public JsonSummarizedCommentsStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CodeCondenserTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".codecondenser"));
        _commentsPath = Path.Combine(_tempDirectory, ".codecondenser", "summarized-comments.json");
        _store = new JsonSummarizedCommentsStore();
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyDictionary()
    {
        var result = await _store.LoadAsync(_commentsPath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_Succeeds()
    {
        var comments = new Dictionary<string, string>
        {
            ["hash1"] = "Summary for method 1",
            ["hash2"] = "/// <summary>Summary for method 2</summary>"
        };

        await _store.SaveAsync(_commentsPath, comments);
        var loaded = await _store.LoadAsync(_commentsPath);

        loaded.Should().HaveCount(2);
        loaded["hash1"].Should().Be("Summary for method 1");
        loaded["hash2"].Should().Be("/// <summary>Summary for method 2</summary>");
    }

    [Fact]
    public async Task Save_OrdersEntriesDeterministicallyByHash()
    {
        var comments = new Dictionary<string, string>
        {
            ["z_hash"] = "Z summary",
            ["a_hash"] = "A summary",
            ["m_hash"] = "M summary"
        };

        await _store.SaveAsync(_commentsPath, comments);
        var fileText = await File.ReadAllTextAsync(_commentsPath);

        int aIndex = fileText.IndexOf("a_hash");
        int mIndex = fileText.IndexOf("m_hash");
        int zIndex = fileText.IndexOf("z_hash");

        aIndex.Should().BeLessThan(mIndex);
        mIndex.Should().BeLessThan(zIndex);
    }

    [Fact]
    public async Task LoadAsync_SupportsFlatDictionaryFormat()
    {
        var flatJson = """
            {
                "hashA": "Text A",
                "hashB": "Text B"
            }
            """;
        await File.WriteAllTextAsync(_commentsPath, flatJson);

        var loaded = await _store.LoadAsync(_commentsPath);

        loaded.Should().HaveCount(2);
        loaded["hashA"].Should().Be("Text A");
        loaded["hashB"].Should().Be("Text B");
    }

    [Fact]
    public async Task LoadAsync_WhenCorrupted_ReturnsEmptyDictionary()
    {
        await File.WriteAllTextAsync(_commentsPath, "{ corrupted json !");

        var loaded = await _store.LoadAsync(_commentsPath);

        loaded.Should().BeEmpty();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch { }
    }
}
