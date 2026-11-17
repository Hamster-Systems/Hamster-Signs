# Hamster Signs architecture

The desktop UI must not contain regulatory design rules. Separate these concerns:

- **Core**: physical-unit (mm) document model, geometry, history, and serialization.
- **Typography**: sign-specific glyph shaping and layout.
- **Standards**: jurisdiction-, date- and regulation-specific rule packs with traceable references.
- **Rendering**: vector scene to screen, SVG, raster and print formats.
- **Desktop**: Avalonia editor, project browser, properties and commands.
- **Tests**: geometry invariants, serialisation round-trips, standards fixtures and rendering regressions.

The early prototype does not prove compliance. Standards verification must use official working drawings and human review before production use.
