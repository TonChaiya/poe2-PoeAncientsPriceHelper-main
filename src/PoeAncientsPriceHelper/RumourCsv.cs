using System.Text;

namespace PoeAncientsPriceHelper;

// Parses the community rumour sheet's CSV export (columns: Rumor, Map Type, Mods, Rating) into entries.
// Tolerant of the sheet's quirks: the header row, blank separator rows, and section-header rows
// ("Sagas,,," / "Unique Maps,,," / "Bosses,,,") are all dropped because a real entry needs both a name
// and a rating. Quoted fields with embedded commas ("Wild,.Roaming Free") are handled, and values
// (including the "(see notes)" / slash-separated mods) are preserved verbatim.
internal static class RumourCsv
{
    public static List<RumourEntry> Parse(string csv)
    {
        var result = new List<RumourEntry>();
        using var reader = new StringReader(csv);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = SplitLine(line);
            if (fields.Count < 4) continue;

            string rumor = fields[0].Trim();
            string mapType = fields[1].Trim();
            string mods = fields[2].Trim();
            string rating = fields[3].Trim();

            if (rumor.Equals("Rumor", StringComparison.OrdinalIgnoreCase)) continue;   // header row
            // A data row needs both a name and a rating; this drops blank separators and the section
            // headers (whose Rating column is empty).
            if (rumor.Length == 0 || rating.Length == 0) continue;

            result.Add(new RumourEntry(rumor, mapType, mods, rating));
        }
        return result;
    }

    // Minimal RFC-4180-style field splitter: double-quoted fields may contain commas, and "" is an
    // escaped quote. (The sheet has no newlines inside fields, so line-by-line reading is safe.)
    private static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
