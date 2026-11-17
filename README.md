# Hamster Signs

An open-source, professional-grade UK traffic-sign design application project.

**Status:** Early foundation; not suitable for producing signs for installation on public roads.

## Technology
- .NET 8 and C#
- Avalonia UI for Windows and cross-platform desktop support
- Vector-first geometry model with millimetre units
- JSON-based native project format
- Standards-aware rules engine planned separately from UI

## Roadmap
1. CAD editor: selection, pan/zoom, snapping, layers, undo/redo, Bézier geometry
2. UK sign lettering: Transport and Motorway alphabets, x-height and spacing rules
3. Direction signs: junction diagrams, route numbers, patches and automatic layout
4. Warning, regulatory, tourist, temporary and motorway sign libraries
5. Engineering sheets: dimensions, approval/title blocks, SVG/PDF/DXF exports
6. Versioned TSRGD compliance checks with traceable references and tests
7. Manufacturing and print QA, installer and documented extension API

## Regulatory references
- [TSRGD 2016, as amended](https://www.legislation.gov.uk/uksi/2016/362/contents)
- [Traffic Signs Manual](https://www.gov.uk/government/publications/traffic-signs-manual)

UK guidance and legislation must be distinguished. Northern Ireland requires a separate standards profile.

## Licensing
This repository uses the GNU Affero General Public License v3. All contributions should comply with it. Do not add third-party proprietary fonts, vendor artwork or protected technical drawings without the necessary rights.
