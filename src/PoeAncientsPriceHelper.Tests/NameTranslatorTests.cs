using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class NameTranslatorTests
{
    // Real German names from issue #29's debug log (verified against poe2db.tw). The OCR'd,
    // normalized German name must resolve to the English price key.
    private static NameTranslator German() => NameTranslator.FromPairs(
    [
        ("Chaos Orb", "Chaossphäre"),
        ("Divine Orb", "Göttliche Sphäre"),
        ("Exalted Orb", "Erhabene Sphäre"),
        ("Greater Jeweller's Orb", "Große Sphäre des Goldschmieds"),
        ("Orb of Alchemy", "Sphäre der Alchemie"),
    ]);

    [Theory]
    [InlineData("chaossphäre", "chaos orb")]
    [InlineData("göttliche sphäre", "divine orb")]
    [InlineData("große sphäre des goldschmieds", "greater jeweller s orb")] // the #29 reporter's item
    public void Translate_ExactLocalizedName_ReturnsEnglishKey(string normalizedLocalized, string expectedKey)
    {
        Assert.Equal(expectedKey, German().Translate(normalizedLocalized));
    }

    // OCR commonly drops or mangles umlauts on the stylised panel font; diacritic folding must still
    // resolve the name (e.g. "chaossphare" with no umlaut → chaos orb).
    [Theory]
    [InlineData("chaossphare")]              // ä read as a
    [InlineData("gottliche sphare")]         // ö, ä both flattened
    [InlineData("grosse sphare des goldschmieds")] // ß→ss + ä→a
    public void Translate_DiacriticDroppedByOcr_StillResolves(string normalizedLocalized)
    {
        var en = German().Translate(normalizedLocalized);
        Assert.NotEqual(normalizedLocalized, en); // it translated to *something* English
    }

    // An English client reads English names directly — there must be no mapping that mangles them.
    [Theory]
    [InlineData("chaos orb")]
    [InlineData("greater vision rune")]
    public void Translate_AlreadyEnglish_PassesThroughUnchanged(string english)
    {
        Assert.Equal(english, German().Translate(english));
    }

    // An unknown name (not in any locale file) is returned verbatim so the English matcher can still
    // try its fuzzy chain on it.
    [Fact]
    public void Translate_UnknownName_ReturnsInputUnchanged()
    {
        Assert.Equal("völlig unbekannt", German().Translate("völlig unbekannt"));
    }

    // The empty translator (English client / no locale files) is a pure pass-through.
    [Fact]
    public void Empty_IsIdentity()
    {
        Assert.False(NameTranslator.Empty.HasEntries);
        Assert.Equal("chaossphäre", NameTranslator.Empty.Translate("chaossphäre"));
    }

    // Later locales override earlier ones key-by-key (a user file correcting a bundled entry).
    [Fact]
    public void FromPairs_LastWriterWins_OnLocalizedCollision()
    {
        var t = NameTranslator.FromPairs(
        [
            ("Wrong Item", "Testsphäre"),
            ("Chaos Orb", "Testsphäre"),
        ]);
        Assert.Equal("chaos orb", t.Translate("testsphäre"));
    }

    // Localized files for different languages merge into one translator (a Russian Cyrillic name and a
    // German name both resolve through the same instance).
    [Fact]
    public void FromPairs_MergesMultipleLanguages()
    {
        var t = NameTranslator.FromPairs(
        [
            ("Chaos Orb", "Chaossphäre"),     // de
            ("Chaos Orb", "Сфера хаоса"),     // ru (Cyrillic, untouched by folding)
        ]);
        Assert.Equal("chaos orb", t.Translate("chaossphäre"));
        Assert.Equal("chaos orb", t.Translate("сфера хаоса"));
    }
}
