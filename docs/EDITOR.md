# CAD editor interaction guide (early prototype)

Run with .NET 8 SDK:

```bash
dotnet run --project src/HamsterSigns.Desktop
```

Core smoke checks:

```bash
dotnet run --project tests/HamsterSigns.Core.Tests
```

## v0.3 curved-junction editing
- Add curved road creates a cubic Bézier road stroke
- Click a road curve to select and drag the entire stroke
- On selection, drag the two white endpoint handles or two orange curvature handles
- Each handle drag is tracked as a single undoable edit
- Curves are saved as four millimetre-coordinate points and exported as real SVG `C` paths
- Curve geometry is checked for non-finite coordinates

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
- This iteration does not contain generic resize handles, ruler/grid overlays, line end-point editing, groups or layers.
- Curves are one cubic segment each; multi-segment connected junction networks and arm joins are not yet implemented.
- Text uses a system font in previews and exports; correct UK Transport/Motorway typography is not implemented.
- Canvas zoom and pan are view-only and do not alter physical document dimensions.
- Neither sign-specific layout nor TSRGD compliance is implemented.
- The code has not been compiled in the authoring environment; build verification is required.
