using FluentAssertions;
using Pmad.CodeCondenser.Core.Strings;

namespace Pmad.CodeCondenser.Core.Tests.Strings;

public class ConstantNamingHelperTests
{
    [Fact]
    public void SuggestConstantName_GeneratesCleanPascalCaseIdentifier()
    {
        var name = ConstantNamingHelper.SuggestConstantName("user_profile_updated_successfully");
        name.Should().Be("UserProfileUpdatedSuccessfully");
    }

    [Fact]
    public void SuggestConstantName_SentenceWithPunctuation_RemovesNonAlphanumeric()
    {
        var name = ConstantNamingHelper.SuggestConstantName("The quick brown fox jumps over the lazy dog!");
        name.Should().Be("TheQuickBrownFoxJumpsOver");
    }

    [Fact]
    public void SuggestConstantName_LeadingDigits_PrependsK()
    {
        var name = ConstantNamingHelper.SuggestConstantName("12345 special error message for testing");
        name.Should().StartWith("String_12345");
    }

    [Fact]
    public void SuggestConstantName_EmptyOrWhitespace_ReturnsDefaultName()
    {
        var name = ConstantNamingHelper.SuggestConstantName("    ");
        name.Should().Be("StringConstant");
    }

    [Fact]
    public void SuggestConstantName_VeryLongString_TruncatesGracefully()
    {
        var veryLong = new string('A', 200);
        var name = ConstantNamingHelper.SuggestConstantName(veryLong);
        name.Length.Should().BeLessThanOrEqualTo(40);
    }
}
