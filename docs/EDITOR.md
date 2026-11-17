# CAD editor interaction guide (early prototype)

Run with .NET 8 SDK:

```bash
dotnet run --project src/HamsterSigns.Desktop
```

Core smoke checks:

```bash
dotnet run --project tests/HamsterSigns.Core.Tests
```

## Implemented
- Click an element to select it
- Drag selected text, panels, circles and line segments on the sign face
- Middle-mouse drag to pan
- Wheel zoom centred around mouse position
- Fit sign and toolbar zoom buttons
- Optional millimetre-grid snapping
- Undo/redo (Ctrl+Z / Ctrl+Y)
- Delete selected (Delete or button)
- Edits tracked as transactions in the core model
- Open/save JSON, SVG export and basic structural validation

## Known limitations
- This iteration does not contain resize/Bezier handles, ruler/grid overlays, line end-point editing, groups or layers.
- Text uses a system font in previews and exports; correct UK Transport/Motorway typography is not implemented.
- Canvas zoom and pan are view-only and do not alter physical document dimensions.
- Neither sign-specific layout nor TSRGD compliance is implemented.
- The code has not been compiled in the authoring environment; build verification is required.
