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

## v0.7 — Tile metrics and typography audit

- The layout engine now uses the **Transport Medium tile-width tables** from DfT Schedule 17 drawings TM1 (capitals), TM2 (lowercase and punctuation) and TM3 (digits).
- It applies the TM1 contextual wider-W rule before T, V, X, Y or Z and contextual T/V/Y overlap ahead of selected lowercase characters.
- Stroke width is x-height / 4, tile height is 2 × x-height, and normal inter-word spacing is 2.5 stroke widths, matching Traffic Signs Manual Chapter 7 sections 2.2–2.3.
- Characters outside those tables (including accents, fractions, other alphabets and symbols) use a **clearly flagged fallback estimate**.
- The desktop layout sidebar shows an audit of unverified characters and renderer/standards limitations.
- Sign widths and row heights are recomputed using these metrics.

Primary references:
- [DfT Schedule 17 TM1, Transport Medium capitals](https://assets.publishing.service.gov.uk/media/5eb2bdafd3bf7f5d3e24af82/traffic-sign-drawing-schedule-17-part-01-tm1.pdf)
- [DfT Schedule 17 TM2, Transport Medium lower-case and punctuation](https://assets.publishing.service.gov.uk/media/5eb2bdb886650c278fc64b2e/traffic-sign-drawing-schedule-17-part-01-tm2.pdf)
- [DfT Schedule 17 TM3, Transport Medium numerals](https://assets.publishing.service.gov.uk/media/5eb2bdc286650c278a0d1734/traffic-sign-drawing-schedule-17-part-01-tm3.pdf)
- [DfT Traffic Signs Manual Chapter 7 (2018)](https://assets.publishing.service.gov.uk/government/uploads/system/uploads/attachment_data/file/782725/traffic-signs-manual-chapter-07.pdf)

## IMPORTANT — standards and typographic limitations

**Not compliant / not for road manufacture.**

This version uses TM1–TM3 tile-width tables for ordinary Transport Medium letters, digits and common punctuation. Other alphabets and unsupported glyphs remain approximations. It does not ship proprietary Transport/ Motorway fonts, does not calculate letter tile widths, kerning or the Chapter 7 stroke-width tables, and has not verified panel dimensions or sign sizes against official designs. The current borders and arrows are draft vector shapes, not prescribed diagram artwork.

The x-height value is an authoring parameter. Although the engine uses millimetres and estimates widths and margins based on it, the generated geometry **must not** be treated as conforming to TSRGD or the Traffic Signs Manual.

For future implementation and validation refer to:
- [Traffic Signs Manual Chapter 7](https://www.gov.uk/government/publications/traffic-signs-manual)
- [TSRGD 2016 as amended](https://www.legislation.gov.uk/uksi/2016/362/contents)

Next technical milestone: true Transport tile-width and stroke-width metric engine; licensed/legally distributable letterforms; reference-fixture testing; sign-specific spacing profiles; panel rule tables; Welsh bilingual and jurisdiction profiles.
