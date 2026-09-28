using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.Search;

namespace Kariyer.Recruiting.Domain.Tests;

public class TurkishCasingTests
{
    [Theory]
    [InlineData("Şimşek", "simsek")]
    [InlineData("SIMSEK", "simsek")]
    [InlineData("İnci", "inci")]
    [InlineData("ılık", "ilik")]
    [InlineData("Öztürk", "ozturk")]
    [InlineData("Çağrı", "cagri")]
    [InlineData("Gülşah ÜNAL", "gulsah unal")]
    public void Folds_turkish_letters_to_their_ascii_form(string input, string expected) =>
        Assert.Equal(expected, TurkishCasing.Normalize(input));

    [Fact]
    public void Simsek_and_Şimşek_are_the_same_search()
    {
        // The whole point: a recruiter without a Turkish keyboard finds the same candidate.
        Assert.Equal(TurkishCasing.Normalize("Şimşek"), TurkishCasing.Normalize("simsek"));
    }

    [Fact]
    public void Handles_null_and_empty() => Assert.Equal(string.Empty, TurkishCasing.Normalize(null));
}

public class NoteRulesTests
{
    [Fact]
    public void Blank_bodies_normalize_to_null_which_means_clear_the_note()
    {
        Assert.Null(NoteRules.Normalize("   "));
        Assert.Null(NoteRules.Normalize(null));
        Assert.Equal("not", NoteRules.Normalize("  not  "));
    }

    [Fact]
    public void Rejects_bodies_over_the_limit() =>
        Assert.Contains("body", NoteRules.Validate(new string('a', NoteRules.MaxLength + 1)).Errors.Keys);

    [Fact]
    public void Accepts_a_body_at_the_limit() =>
        Assert.True(NoteRules.Validate(new string('a', NoteRules.MaxLength)).IsValid);
}
