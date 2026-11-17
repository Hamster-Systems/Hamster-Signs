# v0.9 — Rectangular direction sign layout rules

Hamster Signs now has an explicit layout-rules layer (`DirectionLayoutRules`) with source annotations. The direction sign composer uses selected dimensional recommendations from the **2018 Traffic Signs Manual, Chapter 7**. These rules are implemented for the **current basic rectangular row-layout prototype**, not for the full range of prescribed traffic signs.

## Implemented dimensional calculations

| Rule | Chapter 7 reference | Current implementation |
| --- | --- | --- |
| Stroke-width unit | 2.2 and 2.5 | One-quarter of the chosen x-height |
| Main border width | 2.5.4 | 1.5 sw for basic rectangular sign |
| Tile clearance from inside edge of border | Figure 2-4 | 2.5 sw |
| Route number patch inset | 3.3.4; Figure 3-9 | 1 sw horizontally; current prototype also uses 1 sw vertically |
| Border for dark patch on dark sign | 3.3.1 and 3.3.4 | White, 0.5 sw |
| Patch corner radius | 3.3.1 | 1 sw |
| Legend-to-route patch gap | Figure 3-11 | At least 2.5 sw |
| Symbol vertical balance | 3.5.1 | Symbol centre shifted upward 0.5 sw |
| Optional rounding | 2.6.2–2.6.3 | Round up to Z = ceil((0.05 × x-height)/5) × 5 mm |

**Horizontal alignment:** place-name columns start at a common X; route-number entries align to the right edge of a shared route column. Font tile widths determine the longest legend and the widest route label, so the resulting layout does not rely on an arbitrary character count. Text rows and adjacent patches share the same text-tile top coordinate.

**Rounding:** enabled by default in the editor as *Round overall dimensions*. The difference between raw and rounded size is added equally to opposite outer clearances. Both settings are saved in the native JSON specification.

**Rounded geometry:** sign borders and coloured patches use a millimetre corner-radius property, shared between Avalonia preview and SVG output.

## Explicitly provisional

- Arrow artwork, row-to-row spacing and the mixed-column layout are placeholders, not validated Chapter 7 figure reproductions.
- Main border treatment varies by sign type; 1.5 sw is not universally applicable.
- Not every colour-coded route is appropriately displayed as an isolated patch, and motorway/primary-route font selection still requires proper checking.
- Transport font outlines require the user's licensed local font. Non-Medium lettering metrics and specialty combinations are not fully verified.
- Traffic Signs Regulations and General Directions 2016, Schedule 12, must be checked for a given sign design and its intended context.
- Northern Ireland has separate legislation; Welsh and Gaelic bilingual layout rules require additional profiles.

**Do not use the current output to manufacture or install signs without professional design verification.**

Primary reference: [DfT Traffic Signs Manual, Chapter 7 (2018)](https://www.gov.uk/government/publications/traffic-signs-manual), sections 2, 3, 4 and 5. The Department's [TSRGD 2016](https://www.legislation.gov.uk/uksi/2016/362/contents) sets mandatory requirements, while the Manual provides guidance.

## How to use

Enter destination lines under **Direction sign layout**, set the x-height, select a background colour, and choose whether to round the overall dimensions. Click **Generate / regenerate sign**, then review the audit displayed in the editor. The layout is one undoable operation.
