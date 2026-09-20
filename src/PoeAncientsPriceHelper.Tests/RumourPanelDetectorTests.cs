using System.Drawing;
using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class RumourPanelDetectorTests
{
    private static OcrTextLine Line(string text, int x, int y, int w = 180, int h = 24) =>
        new(text, new Rectangle(x, y, w, h));

    // A full-frame OCR result modelled on the in-game panel (rumors 1-3 / rumour panel screenshots):
    // map noise scattered around, the panel signature, three rumour names, and the footer.
    private static List<OcrTextLine> PanelLines() =>
    [
        Line("Act 3", 50, 10, 70, 20),
        Line("WORLD", 1100, 8, 90, 22),
        Line("Mistwood", 300, 500),                 // far-away map node — must be ignored
        Line("UNCHARTED WATERS", 1000, 100, 220, 30),
        Line("Use a logbook to chart the area", 1000, 134, 220, 18),
        Line("ISLAND RUMOURS", 1010, 160, 200, 28),
        Line("Somethin' fishy...", 1015, 200),
        Line("Sulphite!", 1015, 232),
        Line("Unknown ruins...", 1015, 264),
        Line("Requires:", 1015, 304),
        Line("Expedition Logbook", 1015, 330),
    ];

    [Fact]
    public void Detect_ExtractsRumourNamesInPanelOrder()
    {
        var panel = RumourPanelDetector.Detect(PanelLines());
        Assert.NotNull(panel);
        Assert.Equal(
            new[] { "Somethin' fishy...", "Sulphite!", "Unknown ruins..." },
            panel!.RumourLines.Select(l => l.Text).ToArray());
    }

    [Fact]
    public void Detect_ExcludesSignatureBoilerplateAndFarNoise()
    {
        var panel = RumourPanelDetector.Detect(PanelLines());
        Assert.NotNull(panel);
        var texts = panel!.RumourLines.Select(l => l.Text).ToList();
        Assert.DoesNotContain("ISLAND RUMOURS", texts);
        Assert.DoesNotContain("Use a logbook to chart the area", texts);
        Assert.DoesNotContain("Expedition Logbook", texts);
        Assert.DoesNotContain("Mistwood", texts);   // outside the panel's horizontal band
        Assert.DoesNotContain("WORLD", texts);
    }

    [Fact]
    public void Detect_GarbledTitle_NotTreatedAsRumourRow()
    {
        // "UNCHARTED WATERS" is recognised as the header, but the "ISLAND RUMOURS" title is OCR-garbled
        // below the fuzzy signature threshold so it isn't caught as a header — it must still be excluded
        // from the rumour rows (it used to surface as a stray "unknown rumour").
        List<OcrTextLine> lines =
        [
            Line("UNCHARTED WATERS", 1000, 100, 220, 30),
            Line("Islawd Rnnonrs", 1010, 150, 200, 26),   // garbled "ISLAND RUMOURS" in the name band
            Line("Endless cliffs...", 1015, 190),
            Line("Unknown ruins...", 1015, 222),
            Line("Requires:", 1015, 262),
            Line("Expedition Logbook", 1015, 288),
        ];

        var panel = RumourPanelDetector.Detect(lines);
        Assert.NotNull(panel);
        Assert.Equal(
            new[] { "Endless cliffs...", "Unknown ruins..." },
            panel!.RumourLines.Select(l => l.Text).ToArray());
    }

    [Fact]
    public void Detect_NoSignature_ReturnsNull()
    {
        List<OcrTextLine> lines =
        [
            Line("Mistwood", 300, 500),
            Line("Some Random Node", 600, 400),
            Line("WORLD", 1100, 8, 90, 22),
        ];
        Assert.Null(RumourPanelDetector.Detect(lines));
    }

    [Fact]
    public void Detect_SignatureButNoRumourRows_ReturnsNull()
    {
        List<OcrTextLine> lines =
        [
            Line("UNCHARTED WATERS", 1000, 100, 220, 30),
            Line("ISLAND RUMOURS", 1010, 160, 200, 28),
            Line("Requires:", 1015, 190),   // footer immediately after header: nothing between
            Line("Expedition Logbook", 1015, 216),
        ];
        Assert.Null(RumourPanelDetector.Detect(lines));
    }

    // End-to-end resolve: the detected names match the bundled data (fuzzy/truncated), and an unknown
    // rumour comes back unmatched so the overlay can show "?".
    [Fact]
    public void BuildResult_ResolvesDetectedNames_AndFlagsUnknown()
    {
        var lines = PanelLines();
        lines.Add(Line("Glorptak the Unmade", 1015, 288));   // not in the data; above the footer

        var result = RumourScanner.BuildResult(lines, RumourRepository.LoadBundled());
        Assert.NotNull(result);

        var byName = result!.Rows.ToDictionary(r => r.OcrName);
        Assert.Equal("Something Fishy", byName["Somethin' fishy..."].Entry!.Rumor);
        Assert.Equal("Scorched Cay", byName["Sulphite!"].Entry!.MapType);
        Assert.Equal("Unknown Ruins", byName["Unknown ruins..."].Entry!.Rumor);
        Assert.False(byName["Glorptak the Unmade"].Matched);
        Assert.Null(byName["Glorptak the Unmade"].Entry);
    }
}
