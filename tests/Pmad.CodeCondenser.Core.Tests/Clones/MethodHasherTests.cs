using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Clones;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class MethodHasherTests
{
    private readonly MethodHasher _hasher = new();
    private readonly AstNormalizer _normalizer = new();

    private static MethodDeclarationSyntax ParseMethod(string code)
    {
        var source = code.Contains("class") ? code : $"class Wrapper {{ {code} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    public void ComputeHash_IdenticalNormalizedMethods_ProducesSameSha256()
    {
        var m1 = _normalizer.Normalize(ParseMethod("void A() { int x = 1; }"));
        var m2 = _normalizer.Normalize(ParseMethod("void B() { int y = 2; }"));

        var hash1 = _hasher.ComputeHash(m1);
        var hash2 = _hasher.ComputeHash(m2);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_WhitespaceAndLineEndingsDifferences_ProducesSameHash()
    {
        var m1 = ParseMethod(@"
void Execute()
{
    int a = 1;
    int b = 2;
}");

        var m2 = ParseMethod(@"void Execute() { int a = 1; int b = 2; }");

        var hash1 = _hasher.ComputeHash(m1);
        var hash2 = _hasher.ComputeHash(m2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentLogic_ProducesDifferentHashes()
    {
        var m1 = ParseMethod("void DoWork() { int a = 1; }");
        var m2 = ParseMethod("void DoWork() { int a = 2; }");

        var hash1 = _hasher.ComputeHash(m1);
        var hash2 = _hasher.ComputeHash(m2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_NullArgument_ThrowsArgumentNullException()
    {
        Action act = () => _hasher.ComputeHash(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
