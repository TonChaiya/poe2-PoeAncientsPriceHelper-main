# Contributing

Thanks for helping improve Poe Ancients Price Helper! A few ground rules so
contributions are safe to accept and sustainable to maintain.

## Source only — no binaries

Contributions must be **source code or data files**, submitted as a pull
request. Prebuilt DLLs, EXEs, or other binaries will not be reviewed or
merged — the maintainer only ships artifacts rebuilt from reviewed source.
This is a security floor (the app captures the screen and makes network
calls), not a judgment of intent.

## Pull requests, not issue attachments

Code changes go through a PR so they can be reviewed, built, and run through
CI. Pasting a patch or linking an external fork in an issue is a fine way to
*start* a conversation, but the change itself lands as a PR.

## Localization & region support

Non-English / region-specific support (OCR name mapping, price tables) is
**very welcome — as opt-in data files**, not changes to the core engine.

- The core app stays language-agnostic. Language-specific item-name tables
  (e.g. Cyrillic display name → canonical English key) ship as **optional
  data files** loaded defensively — the same pattern as `custom_prices.json`.
  A missing or malformed file is ignored, never fatal.
- Such files are **community-maintained and best-effort**. They are owned by
  the contributors who add them, not the maintainer. Because item names change
  every league, these tables need ongoing upkeep; the maintainer can't verify
  or support a language they don't read, and won't carry that table in core.
- This keeps region support available to players who need it, without turning
  it into an open-ended support obligation on the project.

If you're adding a new language, open an issue first so we can agree on the
file format and where it loads from before you invest in the mapping.
