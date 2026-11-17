# v0.10 — Sign templates and vector symbol palette

## Templates in the desktop editor

Choose a template under **Direction sign layout**, enter destinations in the existing four-field format, then press **Generate / regenerate sign**:

- **StackAdvance**: editable stacked destination rows and route numbers. Restricted to three destination rows in the initial template. Traffic Signs Manual Chapter 7 §4.1.1 recommends stack-type advance direction signs for simple junctions, with no more than three directions.
- **MapAdvance**: reserves an additional diagram column and places an editable, four-direction-capable junction sketch alongside the destination list.
- **MotorwayMap**: a motorway-blue map-style layout preset, with a separate diagram column and normal font role assignment for its text/route-number components.

These templates are **design starting points**, not reproductions or approvals of any particular prescribed diagram. Map-type presets currently use a simplified roundabout-style junction. Their road shapes, destination placement and arrow artwork require further development against Chapter 7 sections 4, 5 and 10.

All templates use the established millimetre model, support undo/redo of regeneration, and retain the chosen template in native JSON.

## Symbol palette

Select **Parking**, **Information**, **Hospital**, **Railway**, or **Aircraft** from the drafting-symbol dropdown and press **Insert selected symbol**. Symbols are editable objects: select, drag, delete, undo, save, and export like other CAD elements.

Symbol geometry is an **original simplified drawing** stored as normalized SVG vectors. It is deliberately not claimed to match the official Department for Transport working drawings. The editable symbol's position and dimensions are stored in millimetres. Export uses transforms, not rasterisation.

## Screenshot-driven improvements (v0.10.1)

The original map preview showed a small crossroads/roundabout beside a stack of otherwise unrelated destinations. The map template now uses a larger diagram and generates **one curved, directional road arm per destination**, plus a separate incoming road. Each outgoing arm stores its own endpoint and a stable `LinkedMapArmId` in the destination specification. The arm endpoint is an editable handle; moving it does not force the road's origin tangent to swing around the junction. Click an orange endpoint to see the corresponding destination in the left sidebar.

Stack-mode directional arrows have also been enlarged, and **Fit sign** centres the artwork in the drawing workspace, reducing the lopsided grey margin seen in earlier previews.

The generated routes remain illustrative: the current map composition is not a certified replacement for Traffic Signs Manual Chapter 7 map-type sign artwork. Manual adjustments, approved symbols and design verification are still required.

## Next work

1. Dedicated map-junction layout solver (symbol/legend avoidance, road stroke rules).
2. Genuine sign-diagram artwork and permitted symbol placements from official working drawings, with rights review.
3. Full template suite: flag-type, route-confirmatory, tourist, services, Welsh bilingual, motorway gantry, speed/regulatory and warning signs.
4. Template-specific legal validation, issue/revision sheets and production testing.

**Not for public-road manufacture or installation without professional checking.**

Reference: [Traffic Signs Manual Chapter 7 (2018)](https://www.gov.uk/government/publications/traffic-signs-manual), chapters 4 (stack advance), 5 (map advance) and 10 (motorway signs).
