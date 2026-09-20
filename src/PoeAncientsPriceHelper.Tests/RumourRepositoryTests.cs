using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class RumourRepositoryTests
{
    // The bundled rumours.json is mirrored into the test output (see the test csproj), so this
    // exercises the real shipped data through the real load + resolve path.
    private static readonly RumourRepository Bundled = RumourRepository.LoadBundled();

    [Fact]
    public void LoadBundled_LoadsTheShippedSnapshot()
    {
        Assert.True(Bundled.Count >= 20, $"expected the bundled snapshot to load, got {Bundled.Count} entries");
    }

    // Exact, clean names resolve straight off the dictionary.
    [Theory]
    [InlineData("Sulphite!", "Scorched Cay")]
    [InlineData("Fallen Stars", "Moor")]
    [InlineData("Unknown Ruins", "Exhumed Ruins")]
    public void Resolve_ExactName_ReturnsEntry(string ocr, string expectedMapType)
    {
        var entry = Bundled.Resolve(ocr);
        Assert.NotNull(entry);
        Assert.Equal(expectedMapType, entry!.MapType);
    }

    // The in-game panel truncates long names with an ellipsis; the (truncated) OCR text is a prefix
    // of the full key, so prefix matching must recover it. NameNormalizer turns "…"/"..." into nothing.
    [Theory]
    [InlineData("Cold as...", "Cold as ice")]
    [InlineData("Cold as ice...", "Cold as ice")]
    [InlineData("Origin of the…", "Origin of the Fall")]
    [InlineData("All that Glitters...", "All that Glitters")]
    public void Resolve_TruncatedName_RecoversViaPrefix(string ocr, string expectedRumour)
    {
        var entry = Bundled.Resolve(ocr);
        Assert.NotNull(entry);
        Assert.Equal(expectedRumour, entry!.Rumor);
    }

    // Casual spelling / dropped characters from OCR are absorbed by the fuzzy step.
    [Theory]
    [InlineData("Somethin' fishy", "Something Fishy")]   // missing 'g', apostrophe
    [InlineData("Endless Cliffs", "Endless Cliffs")]      // single-character slip
    public void Resolve_CasualOrMisreadName_AbsorbedByFuzzy(string ocr, string expectedRumour)
    {
        var entry = Bundled.Resolve(ocr);
        Assert.NotNull(entry);
        Assert.Equal(expectedRumour, entry!.Rumor);
    }

    // Apostrophe normalization: "It's Warm" → "it s warm" both for the OCR text and the stored key.
    [Fact]
    public void Resolve_NameWithApostrophe_Matches()
    {
        var entry = Bundled.Resolve("It's Warm");
        Assert.NotNull(entry);
        Assert.Equal("Lush Island", entry!.MapType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Totally Not A Rumour")]
    public void Resolve_Unknown_ReturnsNull(string ocr)
    {
        Assert.Null(Bundled.Resolve(ocr));
    }

    // Windows OCR systematically mangles the stylised rumour-panel font (n/m/u→w, r→v, the→doe). These
    // are real reads from the game that plain fuzzy can't absorb; the confusion-aware skeleton fallback
    // recovers them. (Strings captured from the OCR probe on the rumour screenshots.)
    [Theory]
    [InlineData("Uwkwww miws", "Unknown Ruins")]
    [InlineData("Uhkwww miws", "Unknown Ruins")]
    [InlineData("Ovi4iw of doe fall", "Origin of the Fall")]
    [InlineData("Sow10viw' fishzo", "Something Fishy")]   // catastrophic garble, but a clear winner
    public void Resolve_FontConfusion_RecoveredBySkeleton(string ocr, string expected)
    {
        var entry = Bundled.Resolve(ocr);
        Assert.NotNull(entry);
        Assert.Equal(expected, entry!.Rumor);
    }

    // The skeleton fallback must NOT manufacture matches: a rumour genuinely absent from the data, and a
    // stray non-name line, both stay unmatched (measured well below the threshold).
    [Theory]
    [InlineData("Waww but riskv")]        // an in-game rumour with no sheet entry under this name
    [InlineData("WatL but Visklê")]       // ditto — must stay unmatched even with the low-confidence tier
    [InlineData("Rarity Rogue Exiles")]   // a mods line that leaked into the read
    public void Resolve_SkeletonDoesNotFalseMatch(string ocr)
    {
        Assert.Null(Bundled.Resolve(ocr));
    }

    // Rating tiers (and their parenthetical notes) are display strings preserved verbatim.
    [Theory]
    [InlineData("Unknown Ruins", "B (see notes)")]
    [InlineData("Bleak and Awful", "F(see notes)")]
    [InlineData("Aldurs", "S+(is a gamble)")]
    public void Resolve_RatingSuffix_PreservedVerbatim(string ocr, string expectedRating)
    {
        var entry = Bundled.Resolve(ocr);
        Assert.NotNull(entry);
        Assert.Equal(expectedRating, entry!.Rating);
    }

    // An entry with an empty Map Type (Aldurs) round-trips as an empty string, not null.
    [Fact]
    public void Resolve_EmptyMapType_IsEmptyString()
    {
        var entry = Bundled.Resolve("Aldurs");
        Assert.NotNull(entry);
        Assert.Equal("", entry!.MapType);
        Assert.Equal("Buffs expeditions", entry.Mods);
    }

    // Prefix tie-break: the SHORTEST key that starts with the OCR text wins, so a short truncation
    // doesn't lock onto a longer unrelated key.
    [Fact]
    public void Resolve_PrefixPrefersShortestKey()
    {
        var repo = RumourRepository.FromJson("""
            [
              { "rumor": "Cold as ice", "mapType": "Frigid Bluffs", "mods": "Old Expedition", "rating": "A+" },
              { "rumor": "Cold as ice and snow", "mapType": "Elsewhere", "mods": "X", "rating": "D" }
            ]
            """);
        var entry = repo.Resolve("Cold as ice");   // exact wins over the longer prefix
        Assert.NotNull(entry);
        Assert.Equal("Frigid Bluffs", entry!.MapType);
    }
}
