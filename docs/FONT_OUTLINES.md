# v0.8 — Local Transport-font outline rendering

## What the editor now does

Hamster Signs does **not** bundle Transport or Motorway font files. The user can provide locally stored TTF or OTF fonts for **Transport Medium**, **Transport Heavy**, and **Motorway Permanent**, subject to their own licence rights.

1. Start the desktop application.
2. In the left sidebar under **Font outlines**, choose **Load Transport Medium font**, **Load Transport Heavy font**, and **Load Motorway Permanent font**, as needed.
3. Pick the matching font file on your computer. Files are loaded for the current session only and never uploaded, copied into the repository, or embedded in project JSON.
4. Generate a direction sign or select a text element and choose its font role.
5. The on-screen sign and exported SVG use the **same glyph outline geometry** when that role's font file is available.
6. If no font is loaded for a role, both formats fall back to ordinary system-font text. The sidebar reports which roles are missing.

Native JSON files preserve the **font role**, not the proprietary font bytes or filesystem path.

## Important limitations

- Real glyph contours are used only when a local font is explicitly loaded. This does not itself certify compliance with the UK working drawings.
- The native font's lowercase-x contour is used to scale glyph height to the specified x-height.
- **Transport Medium** advances use the TM1–TM3 tile widths in v0.7. **Transport Heavy** and **Motorway Permanent** currently advance using the supplied font's own metrics until their official tile spacing systems are verified.
- This version positions codepoints independently and does not implement full complex shaping, OpenType substitution, kerning, fractions, or alternate prescribed tile forms. Supplementary Unicode codepoints are rejected by the outline provider.
- Missing characters in loaded fonts raise an error instead of silently drawing a replacement box.
- SVG paths are filled vector contours, so recipients need not install the font for **outlined text**.
- A user selecting a font must hold the rights required to use it for their project and any distributed SVG.
- The application's sign layout, road symbols, patches, arrows and borders still require standards validation. **Not for public road installation or manufacture without professional review.**

Font licensing reminder: community-distributed free Transport implementations may be limited to non-commercial use or have insufficiently accurate spacing for approved sign design. Check the specific font's terms.

## Build and verify

```powershell
git switch feat/roadsigncad-foundation
git pull origin feat/roadsigncad-foundation
dotnet run --project src/HamsterSigns.Desktop
dotnet run --project tests/HamsterSigns.Core.Tests
```

SVG export will emit editable vector paths for loaded font roles and system-font `<text>` elements for unconfigured roles.
