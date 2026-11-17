using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HamsterSigns.Core;

namespace HamsterSigns.Desktop;

public sealed class EditorWindow : Window
{
    private readonly SignEditorSession editor = new(SignDocument.Example());
    private SignDocument Document => editor.Document;
    private readonly Canvas canvas = new() { Width=1200, Height=850, Background=Brushes.LightGray, Focusable=true };
    private readonly TextBlock status = new() { Text="Ready" };
    private readonly TextBox title = new() { Watermark="Sign title" };
    private readonly TextBox width = new() { Watermark="Width (mm)" };
    private readonly TextBox height = new() { Watermark="Height (mm)" };
    private readonly TextBox gridInput = new() { Text="25", Watermark="Grid (mm)" };
    private readonly CheckBox gridEnabled = new() { Content="Snap to grid", IsChecked=true };
    private readonly ComboBox colour = new() { ItemsSource=Enum.GetValues<SignColour>() };
    private readonly TextBox textEditor = new() { Watermark="Selected text" };
    private Guid? selectedId;
    private double zoom=1;
    private double offsetX=25, offsetY=25;
    private double baseScale=1;
    private Point dragStart;
    private double originX, originY, originX2, originY2;
    private bool dragging, panning, changed;
    private const double MinZoom=0.2, MaxZoom=8;

    public EditorWindow()
    {
        Title="Hamster Signs — CAD Editor (Prototype, NOT TSRGD compliant)";
        Width=1450; Height=900; MinWidth=920; MinHeight=650;
        var tools=new StackPanel { Margin=new Thickness(14), Spacing=8 };
        tools.Children.Add(new TextBlock { Text="HAMSTER SIGNS", FontWeight=FontWeight.Bold, FontSize=22 });
        tools.Children.Add(new TextBlock { Text="Sign properties", FontWeight=FontWeight.SemiBold });
        title.Text=Document.Title; width.Text=Format(Document.WidthMm); height.Text=Format(Document.HeightMm); colour.SelectedItem=Document.Background;
        tools.Children.Add(title); tools.Children.Add(width); tools.Children.Add(height); tools.Children.Add(colour);
        tools.Children.Add(Button("Apply properties", Apply));
        tools.Children.Add(new Separator());
        tools.Children.Add(new TextBlock { Text="Drawing", FontWeight=FontWeight.SemiBold });
        tools.Children.Add(Button("Add text", () => Add(new() { Kind=ElementKind.Text, X=300, Y=250, Text="Destination", FontSizeMm=135 })));
        tools.Children.Add(Button("Add panel", () => Add(new() { Kind=ElementKind.Panel, X=300, Y=350, Width=400, Height=175, Fill="#174c9a" })));
        tools.Children.Add(Button("Add line", () => Add(new() { Kind=ElementKind.Line, X=200, Y=500, X2=900, Y2=500, StrokeWidthMm=55 })));
        tools.Children.Add(Button("Add circle / roundabout ring", () => Add(new() { Kind=ElementKind.Ellipse, X=300, Y=500, Width=520, Height=520, StrokeWidthMm=60 })));
        tools.Children.Add(Button("Delete selected", Delete));
        tools.Children.Add(textEditor);
        tools.Children.Add(Button("Update text", UpdateText));
        tools.Children.Add(new Separator());
        tools.Children.Add(new TextBlock { Text="Navigation & history", FontWeight=FontWeight.SemiBold });
        tools.Children.Add(gridEnabled);
        tools.Children.Add(gridInput);
        tools.Children.Add(Button("Zoom in", () => Zoom(1.25, new Point(500,340))));
        tools.Children.Add(Button("Zoom out", () => Zoom(0.8, new Point(500,340))));
        tools.Children.Add(Button("Fit sign", Fit));
        tools.Children.Add(Button("Undo   Ctrl+Z", Undo));
        tools.Children.Add(Button("Redo   Ctrl+Y", Redo));
        tools.Children.Add(new TextBlock { Text="Drag objects to move. Middle-button drag pans. Mouse wheel zooms around the cursor. Delete removes selection.", TextWrapping=TextWrapping.Wrap });
        tools.Children.Add(new Separator());
        tools.Children.Add(Button("Open project", Open));
        tools.Children.Add(Button("Save project", Save));
        tools.Children.Add(Button("Export SVG", Export));
        tools.Children.Add(Button("Validate geometry", () => status.Text=string.Join(" | ", SignValidator.Check(Document).Select(d=>d.Message))));
        tools.Children.Add(new TextBlock { Text="Visual prototype only. Sign-specific dimensions, fonts and TSRGD checks are not yet implemented.", TextWrapping=TextWrapping.Wrap, Foreground=Brushes.DarkOrange });
        var sidebar=new ScrollViewer { Width=275, Content=tools };
        var workspace=new DockPanel();
        var toolbar=new TextBlock { Text="Sign canvas — click to select · drag to move · scroll to zoom · middle-drag to pan", Margin=new Thickness(14), FontSize=15 };
        DockPanel.SetDock(toolbar,Dock.Top);
        DockPanel.SetDock(status,Dock.Bottom);
        workspace.Children.Add(toolbar);
        workspace.Children.Add(status);
        workspace.Children.Add(new ScrollViewer { Content=canvas, HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        var root=new DockPanel();
        DockPanel.SetDock(sidebar,Dock.Left);
        root.Children.Add(sidebar); root.Children.Add(workspace);
        Content=root;

        canvas.PointerPressed += CanvasPressed;
        canvas.PointerMoved += CanvasMoved;
        canvas.PointerReleased += CanvasReleased;
        canvas.PointerCaptureLost += (_,_) => EndDrag(false);
        canvas.PointerWheelChanged += (_,e) => { Zoom(e.Delta.Y>0 ? 1.12 : 1/1.12, e.GetPosition(canvas)); e.Handled=true; };
        KeyDown += OnKeyDown;
        Fit();
    }

    private static string Format(double value) => value.ToString("0.##",CultureInfo.InvariantCulture);
    private static double ReadNumber(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result) ? result : fallback;
    private double Grid => gridEnabled.IsChecked==true ? Math.Max(0,ReadNumber(gridInput.Text,25)) : 0;
    private SignElement? Selected => Document.Elements.FirstOrDefault(e=>e.Id==selectedId);
    private static Button Button(string label, Action action)
    {
        var b=new Button { Content=label, HorizontalAlignment=HorizontalAlignment.Stretch };
        b.Click += (_,_)=>action();
        return b;
    }
    private static IBrush Brush(string value)
    {
        if(value=="none") return Brushes.Transparent;
        try { return new SolidColorBrush(Color.Parse(value)); } catch { return Brushes.White; }
    }
    private void Add(SignElement element)
    {
        editor.Edit(d=>d.Elements.Add(element));
        selectedId=element.Id;
        textEditor.Text=element.Kind==ElementKind.Text ? element.Text : "";
        Draw();
    }
    private void Delete()
    {
        if(selectedId is null) return;
        editor.Edit(d=>d.Elements.RemoveAll(e=>e.Id==selectedId));
        selectedId=null; textEditor.Text=""; Draw();
    }
    private void UpdateText()
    {
        if(Selected is not { Kind: ElementKind.Text }) return;
        editor.Edit(d=>d.Elements.First(e=>e.Id==selectedId).Text=textEditor.Text ?? "");
        Draw();
    }
    private void Apply()
    {
        var w=ReadNumber(width.Text,0);
        var h=ReadNumber(height.Text,0);
        if(w<=0 || h<=0) { status.Text="Width and height must be positive millimetre values"; return; }
        editor.Edit(d=> { d.WidthMm=w; d.HeightMm=h; d.Title=title.Text ?? "Untitled"; if(colour.SelectedItem is SignColour c) d.Background=c; });
        Draw();
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if((e.KeyModifiers & KeyModifiers.Control)!=0 && e.Key==Key.Z) { Undo(); e.Handled=true; }
        else if((e.KeyModifiers & KeyModifiers.Control)!=0 && e.Key==Key.Y) { Redo(); e.Handled=true; }
        else if(e.Key==Key.Delete && e.Source is not TextBox) { Delete(); e.Handled=true; }
    }
    private void Undo()
    {
        if(editor.Undo()) { selectedId=null; SynchronizeProperties(); Draw(); }
    }
    private void Redo()
    {
        if(editor.Redo()) { selectedId=null; SynchronizeProperties(); Draw(); }
    }
    private void SynchronizeProperties()
    {
        title.Text=Document.Title; width.Text=Format(Document.WidthMm); height.Text=Format(Document.HeightMm); colour.SelectedItem=Document.Background;
    }
    private void Fit()
    {
        if(Document.WidthMm<=0 || Document.HeightMm<=0) return;
        baseScale=Math.Min(1040/Document.WidthMm,740/Document.HeightMm);
        zoom=1; offsetX=45; offsetY=45; Draw();
    }
    private double Scale => baseScale*zoom;
    private void Zoom(double factor, Point pivot)
    {
        var old=Scale;
        zoom=Math.Clamp(zoom*factor,MinZoom,MaxZoom);
        var next=Scale;
        offsetX=pivot.X-(pivot.X-offsetX)*next/old;
        offsetY=pivot.Y-(pivot.Y-offsetY)*next/old;
        Draw();
    }
    private void CanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if(e.GetCurrentPoint(canvas).Properties.IsMiddleButtonPressed)
        {
            panning=true; dragStart=e.GetPosition(canvas); e.Pointer.Capture(canvas); e.Handled=true; return;
        }
        if(!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;
        // Selection is assigned by the visual's own pointer handler, which fires before bubbling to canvas.
        if(Selected is not null && ReferenceEquals(e.Source, canvas)) { selectedId=null; Draw(); }
    }
    private void StartDrag(SignElement element, PointerPressedEventArgs e)
    {
        if(!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;
        selectedId=element.Id; textEditor.Text=element.Kind==ElementKind.Text ? element.Text : "";
        dragStart=e.GetPosition(canvas);
        originX=element.X; originY=element.Y; originX2=element.X2; originY2=element.Y2;
        editor.Begin(); dragging=true; changed=false;
        e.Pointer.Capture(canvas);
        e.Handled=true;
        Draw();
    }
    private void CanvasMoved(object? sender, PointerEventArgs e)
    {
        if(!dragging && !panning) return;
        var point=e.GetPosition(canvas);
        if(panning)
        {
            offsetX+=point.X-dragStart.X; offsetY+=point.Y-dragStart.Y;
            dragStart=point; Draw(); return;
        }
        var element=Selected;
        if(element is null) return;
        var dx=(point.X-dragStart.X)/Scale, dy=(point.Y-dragStart.Y)/Scale;
        element.X=SignEditorSession.Snap(originX+dx,Grid);
        element.Y=SignEditorSession.Snap(originY+dy,Grid);
        if(element.Kind==ElementKind.Line)
        {
            element.X2=originX2+(element.X-originX);
            element.Y2=originY2+(element.Y-originY);
        }
        changed=true; Draw();
    }
    private void CanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if(dragging || panning) { EndDrag(true); e.Pointer.Capture(null); }
    }
    private void EndDrag(bool commit)
    {
        if(dragging)
        {
            if(commit) editor.Commit(); else editor.Cancel();
            if(!commit) selectedId=null;
        }
        dragging=false; panning=false; changed=false; Draw();
    }
    private async void Save()
    {
        try
        {
            var file=await StorageProvider.SaveFilePickerAsync(new() { Title="Save Hamster Signs project", SuggestedFileName="sign.roadsign.json" });
            if(file is null) return;
            await using var stream=await file.OpenWriteAsync();
            using var writer=new StreamWriter(stream);
            await writer.WriteAsync(SignFile.Serialize(Document));
            status.Text="Project saved";
        }
        catch(Exception ex) { status.Text=$"Save failed: {ex.Message}"; }
    }
    private async void Open()
    {
        try
        {
            var files=await StorageProvider.OpenFilePickerAsync(new() { Title="Open Hamster Signs project", AllowMultiple=false });
            if(files.Count==0) return;
            await using var stream=await files[0].OpenReadAsync();
            using var reader=new StreamReader(stream);
            var document=SignFile.Deserialize(await reader.ReadToEndAsync());
            if(SignValidator.Check(document).Any(x=>x.IsError)) throw new InvalidDataException("Project has invalid geometry.");
            editor.ReplaceDocument(document);
            selectedId=null; SynchronizeProperties(); Fit();
        }
        catch(Exception ex) { status.Text=$"Open failed: {ex.Message}"; }
    }
    private async void Export()
    {
        try
        {
            var svg=SvgExporter.Export(Document);
            var file=await StorageProvider.SaveFilePickerAsync(new() { Title="Export vector artwork", SuggestedFileName="sign.svg" });
            if(file is null) return;
            await using var stream=await file.OpenWriteAsync();
            using var writer=new StreamWriter(stream);
            await writer.WriteAsync(svg);
            status.Text="SVG exported";
        }
        catch(Exception ex) { status.Text=$"Export failed: {ex.Message}"; }
    }
    private void Draw()
    {
        canvas.Children.Clear();
        if(Document.WidthMm<=0 || Document.HeightMm<=0) return;
        var s=Scale;
        var face=new Rectangle { Width=Document.WidthMm*s, Height=Document.HeightMm*s, Fill=Brush(SignPalette.Background(Document.Background)) };
        Canvas.SetLeft(face,offsetX); Canvas.SetTop(face,offsetY); canvas.Children.Add(face);
        foreach(var element in Document.Elements)
        {
            Control? visual=element.Kind switch
            {
                ElementKind.Text => new TextBlock { Text=element.Text, FontSize=element.FontSizeMm*s, Foreground=Brush(SignPalette.Foreground(Document.Background)) },
                ElementKind.Panel => new Rectangle { Width=element.Width*s, Height=element.Height*s, Fill=Brush(element.Fill), Stroke=Brush(element.Stroke), StrokeThickness=element.StrokeWidthMm*s },
                ElementKind.Ellipse => new Ellipse { Width=element.Width*s, Height=element.Height*s, Stroke=Brush(element.Stroke), StrokeThickness=element.StrokeWidthMm*s, Fill=Brush(element.Fill) },
                ElementKind.Line => new Line { StartPoint=new Point(0,0), EndPoint=new Point((element.X2-element.X)*s,(element.Y2-element.Y)*s), Stroke=Brush(element.Stroke), StrokeThickness=Math.Max(element.StrokeWidthMm*s,2) },
                _=>null
            };
            if(visual is null) continue;
            Canvas.SetLeft(visual,offsetX+element.X*s); Canvas.SetTop(visual,offsetY+element.Y*s);
            visual.PointerPressed += (_,e)=>StartDrag(element,e);
            canvas.Children.Add(visual);
            if(selectedId==element.Id && element.Kind is ElementKind.Panel or ElementKind.Ellipse)
            {
                var outline=new Rectangle { Width=element.Width*s+12, Height=element.Height*s+12, Stroke=Brushes.Orange, StrokeThickness=2, IsHitTestVisible=false };
                Canvas.SetLeft(outline,offsetX+element.X*s-6); Canvas.SetTop(outline,offsetY+element.Y*s-6);
                canvas.Children.Add(outline);
            }
        }
        status.Text=$"{Document.WidthMm:0} × {Document.HeightMm:0} mm · {Document.Elements.Count} objects · zoom {zoom*100:0}% · Undo {editor.CanUndo} / Redo {editor.CanRedo}";
    }
}
