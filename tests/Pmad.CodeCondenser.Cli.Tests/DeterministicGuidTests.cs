using FluentAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Sarif;

namespace Pmad.CodeCondenser.Cli.Tests;

public class DeterministicGuidTests
{
    [Fact]
    public void GenerateCorrelationGuid_SameInput_ProducesIdenticalGuid()
    {
        var guid1 = DeterministicGuid.GenerateCorrelationGuid("ast_hash_abc_123");
        var guid2 = DeterministicGuid.GenerateCorrelationGuid("ast_hash_abc_123");

        guid1.Should().Be(guid2);
    }

    [Fact]
    public void GenerateCorrelationGuid_DifferentInput_ProducesDifferentGuid()
    {
        var guid1 = DeterministicGuid.GenerateCorrelationGuid("hash_alpha");
        var guid2 = DeterministicGuid.GenerateCorrelationGuid("hash_beta");

        guid1.Should().NotBe(guid2);
    }

    [Theory]
    [InlineData("7e111353a183b92f")]
    [InlineData("another_ast_hash")]
    [InlineData("")]
    public void GenerateCorrelationGuid_FollowsRfc4122Version5Specification(string input)
    {
        var guid = DeterministicGuid.GenerateCorrelationGuid(input);
        var formatted = guid.ToString("D");

        // Format is: 8-4-4-4-12
        var parts = formatted.Split('-');
        parts.Should().HaveCount(5);

        // Version 5 check (high nibble of 3rd component)
        parts[2][0].Should().Be('5');

        // Variant 1 check (high 2 bits of 4th component must be 10xx -> 8, 9, a, or b)
        char variantChar = char.ToLowerInvariant(parts[3][0]);
        new[] { '8', '9', 'a', 'b' }.Should().Contain(variantChar);
    }
}
