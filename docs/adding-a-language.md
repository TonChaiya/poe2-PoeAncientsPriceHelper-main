# Cookbook: adding a game language

This app prices items by OCR'ing the Verisium Remnant exchange panel and looking each name up in
poe.ninja's data — which is **English-only**. On a non-English PoE 2 client the OCR reads the localized
name (`Chaossphäre`), which never matches the English key (`chaos orb`), so nothing gets priced
(issue #29). A *locale file* fixes this by mapping localized names back to their English names.

This guide shows two ways to produce one: **by hand** (small fixes / a few items) and **in bulk by
scraping [poe2db.tw](https://poe2db.tw)** (a whole new language). Bulk is how `de` / `pt` / `ru` /
`sp` / `fr` were generated.

> **Golden rule:** use the *official* in-game name, never a literal translation. PoE's localized names
> are often not literal — e.g. German `Große Sphäre des Goldschmieds` is *Greater Jeweller's Orb*, not
> "Greater Goldsmith's Orb". A wrong entry produces a wrong price, which is worse than no entry (an
> untranslated name just falls through to the fuzzy matcher). When unsure, leave it out.

---

## How a locale file works

`src/PoeAncientsPriceHelper/locales/<code>.json` — **English-keyed** (English name → localized name):

```json
{
  "language": "Français (French)",
  "code": "fr",
  "source": "poe2db.tw",
  "note": "Auto-generated seed. Community corrections welcome — see ../../docs/adding-a-language.md.",
  "entries": {
    "Chaos Orb": "Orbe du chaos",
    "Greater Jeweller's Orb": "Grand orbe du joaillier"
  }
}
```

- **`code`** is the language code used both for the filename and the value persisted to
  `AppConfig.GameLanguage`. It also drives the **⚙ Settings → Game language** dropdown, which is built
  from whatever files are present (`NameTranslator.AvailableLocales`).
- English-keyed so you can diff against `_reference_en.txt` (the full English item list) and see what's
  still missing.
- Matching (`NameTranslator`) is **exact → diacritic-folded**, so you do *not* need accent-free
  variants: `Chaossphäre` still matches when OCR drops the umlaut and reads `chaossphare`. Glyph-level
  OCR slips are absorbed afterwards by the English fuzzy matcher.
- Users can drop a file in `%LocalAppData%\PoeAncientsPriceHelper\locales\` to add/override a language
  without rebuilding; it shadows the bundled file of the same code.

To wire a new bundled language into the build, nothing extra is needed — `locales\*.json` is globbed by
both the app and test csproj. Just add the file and the dropdown picks it up.

---

## Path A — by hand (a few items / fixing a wrong entry)

1. Open `locales/<code>.json` (or copy an existing one for a new language; set `language` + `code`).
2. Add/fix entries: English key → the exact in-game name. Get the name from your client, or from
   poe2db.tw with the site switched to your language.
3. Save, build, and select the language in ⚙ Settings → Game language. Done.

---

## Path B — bulk scrape from poe2db.tw (a whole language)

poe2db.tw hosts every PoE 2 item in every client language; the localized name is in the page
`<title>`. The English item name (with spaces → underscores) is the URL slug, so we can fetch each
item's page in the target language and read its localized name off the title.

### B1. poe2db language codes

poe2db uses some **non-standard** codes — verify yours before scraping:

| Language              | poe2db code | Example (`/<code>/Chaos_Orb` title) |
|-----------------------|-------------|-------------------------------------|
| German                | `de`        | Chaossphäre                          |
| French                | `fr`        | Orbe du chaos                        |
| Portuguese (Brazil)   | `pt`        | Orbe do Caos                         |
| Russian               | `ru`        | Сфера хаоса                          |
| Spanish               | `sp`        | Orbe de caos  (**`sp`, not `es`**)   |

Other clients exist on poe2db (Japanese, Korean, Traditional Chinese, Thai) — probe
`https://poe2db.tw/<code>/Chaos_Orb` and check the `<title>` to confirm the code before committing to a
full run. Note our app stores `pt` = Brazilian Portuguese and `sp` = Spanish to match poe2db.

### B2. Get the English reference list

The canonical item names are the keys poe.ninja returns. Either reuse the committed
`locales/_reference_en.txt`, or regenerate it from the live API:

```bash
# from a bash shell (Git Bash). Outputs one English name per line.
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/148 Safari/537.36"
league="Runes of Aldur"; slug=$(echo "$league" | tr -d ' ' | tr A-Z a-z)
: > english_names.txt
for t in Currency Runes Expedition Verisium UncutGems; do
  curl -s -A "$UA" -H "Referer: https://poe.ninja/poe2/economy/$slug/$(echo $t|tr A-Z a-z)" \
    "https://poe.ninja/poe2/api/economy/exchange/current/overview?league=$(echo "$league"|sed 's/ /%20/g')&type=$t" \
  | grep -o '"name":"[^"]*"' | sed 's/"name":"//; s/"$//'
done | sort -u > english_names.txt
wc -l english_names.txt   # ~280
```

(The 5 types are the only ones that appear in the Remnant panel — see `PriceRepository.ExchangeTypes`.)

### B3. Scrape the localized names

```bash
#!/usr/bin/env bash
# usage: scrape.sh <poe2db-code>   e.g. scrape.sh fr   ->  fr.tsv  (english<TAB>localized)
set -u
lang="$1"
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/148 Safari/537.36"
# collapse "(Level N)" variants (gems, flux) to one base page; strip CR/BOM that Windows tools add
mapfile -t names < <(sed -E 's/ \(Level [0-9]+\)$//' english_names.txt | tr -d '\r' | awk 'NF' | sort -u)
: > "$lang.tsv"; ok=0; miss=0
for base in "${names[@]}"; do
  slug="${base// /_}"
  code=$(curl -s -o p.html -w "%{http_code}" -A "$UA" "https://poe2db.tw/$lang/$slug")
  # retry without apostrophes (poe2db accepts both "Jeweller's" and "Jewellers")
  [ "$code" != "200" ] && { s2="${slug//\'/}"; [ "$s2" != "$slug" ] && \
      code=$(curl -s -o p.html -w "%{http_code}" -A "$UA" "https://poe2db.tw/$lang/$s2"); }
  if [ "$code" = "200" ]; then
    loc=$(grep -o '<title>[^<]*</title>' p.html | head -1 \
          | sed -E 's/<title>//; s#</title>##; s/ - PoE2DB.*//' | tr -d '\r' \
          | sed 's/&amp;/\&/g; s/&#39;/'"'"'/g')
    # skip names GGG didn't localize (title == English) — no entry needed
    if [ -n "$loc" ] && [ "$loc" != "$base" ]; then printf '%s\t%s\n' "$base" "$loc" >> "$lang.tsv"; ok=$((ok+1)); else miss=$((miss+1)); fi
  else miss=$((miss+1)); fi
done
echo "$lang: ok=$ok miss=$miss"
```

Run it, then **retry the misses once** (poe2db occasionally 404s a page under load):

```bash
bash scrape.sh fr
comm -23 <(sed -E 's/ \(Level [0-9]+\)$//' english_names.txt|tr -d '\r'|awk 'NF'|sort -u) \
         <(cut -f1 fr.tsv|sort -u) > miss.txt
# (re-run the curl loop in scrape.sh over miss.txt, appending hits to fr.tsv)
```

Expect ~225–228 of 229 base items. The 1–2 stragglers are usually `Verisium` (identical in every
language → correctly skipped) and the per-level flux base (no single page).

### B4. Generate the JSON

```bash
#!/usr/bin/env bash
# usage: gen.sh <code> "<Display Name>"  >  <code>.json     reads <code>.tsv
code="$1"; name="$2"; tsv="$code.tsv"
esc() { sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }
printf '{\n  "language": "%s",\n  "code": "%s",\n  "source": "poe2db.tw",\n' "$name" "$code"
printf '  "note": "Auto-generated seed from poe2db.tw verified in-game names. Community corrections welcome — see ../../docs/adding-a-language.md.",\n'
printf '  "entries": {\n'
awk -F'\t' 'NF==2 && $2!="" && !seen[$1]++' "$tsv" | sort -t$'\t' -k1,1 > .clean.tsv
n=$(wc -l < .clean.tsv); i=0
while IFS=$'\t' read -r en loc; do
  i=$((i+1)); comma=,; [ "$i" -eq "$n" ] && comma=
  printf '    "%s": "%s"%s\n' "$(printf %s "$en"|esc)" "$(printf %s "$loc"|esc)" "$comma"
done < .clean.tsv
printf '  }\n}\n'; rm -f .clean.tsv
```

```bash
bash gen.sh fr "Français (French)" > src/PoeAncientsPriceHelper/locales/fr.json
```

### B5. Validate & ship

```powershell
# JSON parses + entry count (no jq needed)
$j = Get-Content "src\PoeAncientsPriceHelper\locales\fr.json" -Raw -Encoding UTF8 | ConvertFrom-Json
"$($j.code): $(($j.entries.PSObject.Properties|Measure-Object).Count) entries"
```

Then run the test suite — `LocaleFilesTests` loads every shipped file and checks each language's
`Chaos Orb` resolves, so a malformed or mis-coded file fails the build:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test "src\PoeAncientsPriceHelper.Tests\PoeAncientsPriceHelper.Tests.csproj" --nologo
```

If you're adding a brand-new language, also add one `Chaos Orb` assertion for it in
`LocaleFilesTests.BundledLocales_EachLanguageResolvesChaosOrb` and (optionally) its code to
`AvailableLocales_ListsTheFourSeededLanguages`.

---

## Known gaps

- **Uncut gems aren't translated.** They're priced per level and resolved by an English-keyed path
  (`ScanEngine.TryResolveGemKey` looks for the words `skill`/`spirit`/`support` + `gem` + `level`), so
  a localized gem line won't match yet. Tracked as future work.
- **League-specific uniques** (e.g. `Aldur's Saga`) change each league; if names rotate, re-run the
  scrape for the affected items.
