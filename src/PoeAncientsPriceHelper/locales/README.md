# Localized item-name maps

Path of Exile 2 shows item names in the client's language, but poe.ninja prices are **English-only**.
On a non-English client the OCR reads e.g. `Chaossphäre`, which never matches the English price key
`chaos orb`, so every row comes back unpriced (issue #29). These files bridge that gap: they map
localized names back to their canonical English names.

Pick your client language in the app under **⚙ Settings → Game language** (default **English**, which
loads no map). The dropdown lists every `<code>.json` found here, so a new file shows up automatically.
The choice applies the next time scanning starts.

## File format

One file per language, named `<code>.json`. **Keys are English; values are the localized name** —
English-keyed so you can diff a file against `_reference_en.txt` (the full English item list) and see
at a glance what still needs translating.

```json
{
  "language": "Deutsch (German)",
  "code": "de",
  "source": "poe2db.tw",
  "note": "Auto-generated seed. Community corrections welcome.",
  "entries": {
    "Chaos Orb": "Chaossphäre",
    "Divine Orb": "Göttliche Sphäre",
    "Greater Jeweller's Orb": "Große Sphäre des Goldschmieds"
  }
}
```

Matching is accent-tolerant: the app folds diacritics before comparing (`Chaossphäre` still matches
when OCR drops the umlaut and reads `chaossphare`), so you don't need to add accent-free variants.

## Contributing a language or a fix

**Full step-by-step (incl. the poe2db bulk-scrape method): [`../../../docs/adding-a-language.md`](../../../docs/adding-a-language.md).** Quick version:


1. Copy an existing file (or `_reference_en.txt` for the English item list) as a starting point.
2. Fill in `entries` with the **exact** in-game names from your client — copy them from the game or
   from [poe2db.tw](https://poe2db.tw) (switch the site language). Don't translate literally; use the
   official localized name, which is often not a literal translation.
3. Leave out any item you're unsure of rather than guessing — a wrong entry causes a wrong price,
   which is worse than no price (the app falls back to fuzzy-matching untranslated names).
4. Open a PR, or just drop your file in `%LocalAppData%\PoeAncientsPriceHelper\locales\` to use it
   locally. Files there are loaded on top of the bundled ones and override them key-by-key.

## Current seed coverage

The bundled `de` / `fr` / `pt` / `ru` / `sp` files are auto-generated from poe2db.tw and cover the
currency, runes, expedition and Verisium items that appear in the Verisium Remnant exchange panel.
Coverage is ~227/229 items for most languages; **French is ~151** because poe2db has no French name for
some items yet (those fall through to fuzzy matching until contributed). **Uncut gems are not yet
translated** in any language (they're priced per level and need special handling) — a known gap.

`pt` is Brazilian Portuguese and `sp` is Spanish (matching poe2db.tw's language codes).
