# v0.6 — Structured direction sign layout (preliminary)

The direction-sign composer is available in the left panel of the desktop app.

1. Choose the sign background in **Sign properties**.
2. Enter the legend x-height in millimetres.
3. Enter one destination per line using the format
   `Destination | Route number | ahead/left/right | none/motorway/primary/nonprimary/tourist`
4. Click **Generate / regenerate sign**.

Example:

```text
Kendal | A6 | ahead | none
Lancaster | A591 | left | none
The SOUTH | (M6) | right | motorway
```

The generator computes a first-pass sign width and height, row spacing, approximate text positions, panels, borders, and direction arrows. Generated elements remain editable using the existing CAD tools. Regenerating replaces the previous elements within one undoable transaction. The source specification is saved within the JSON project for future re-generation.

## IMPORTANT — standards and typographic limitations

**Not compliant / not for road manufacture.**

The current version estimates glyph widths using approximate per-character metrics. It does not ship proprietary Transport/ Motorway fonts, does not calculate letter tile widths, kerning or the Chapter 7 stroke-width tables, and has not verified panel dimensions or sign sizes against official designs. The current borders and arrows are draft vector shapes, not prescribed diagram artwork.

The x-height value is an authoring parameter. Although the engine uses millimetres and estimates widths and margins based on it, the generated geometry **must not** be treated as conforming to TSRGD or the Traffic Signs Manual.

For future implementation and validation refer to:
- [Traffic Signs Manual Chapter 7](https://www.gov.uk/government/publications/traffic-signs-manual)
- [TSRGD 2016 as amended](https://www.legislation.gov.uk/uksi/2016/362/contents)

Next technical milestone: true Transport tile-width and stroke-width metric engine; licensed/legally distributable letterforms; reference-fixture testing; sign-specific spacing profiles; panel rule tables; Welsh bilingual and jurisdiction profiles.
