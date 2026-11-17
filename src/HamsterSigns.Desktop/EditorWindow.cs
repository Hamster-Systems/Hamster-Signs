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
    private double originC1X, originC1Y, originC2X, originC2Y;
    private int activeHandle;
    private double originHandleX, originHandleY;
    private int selectedArmIndex=0;
    private double originRingRadius;
    private bool dragging, panning;
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
        tools.Children.Add(new TextBlock { Text="Junction constructor", FontWeight=FontWeight.SemiBold });
        tools.Children.Add(Button("Roundabout (4 arms)", () => Add(JunctionGeometry.Roundabout(1100,1000,260))));
        tools.Children.Add(Button("T-junction", () => Add(JunctionGeometry.TJunction(1100,1000))));
        tools.Children.Add(Button("Irregular roundabout", () => Add(JunctionGeometry.IrregularRoundabout(1100,1000))));
        tools.Children.Add(Button("Crossroads", () => Add(JunctionGeometry.Crossroads(1100,1000))));
        tools.Children.Add(Button("Add junction arm", AddJunctionArm));
        tools.Children.Add(Button("Remove last arm", RemoveJunctionArm));
        tools.Children.Add(Button("Add bend to selected arm", AddArmBend));
        tools.Children.Add(Button("Remove last bend", RemoveArmBend));
        tools.Children.Add(Button("Toggle curved/straight arm", ToggleArmSmooth));
        tools.Children.Add(Button("Toggle arrow at road end", ToggleArmArrow));
        tools.Children.Add(Button("Increase roundabout radius", () => ChangeRadius(25)));
        tools.Children.Add(Button("Decrease roundabout radius", () => ChangeRadius(-25)));
        tools.Children.Add(Button("Add curved road", () => Add(new() { Kind=ElementKind.CubicBezier, X=300, Y=500, Control1X=450, Control1Y=350, Control2X=700, Control2Y=750, X2=1000, Y2=570, StrokeWidthMm=65 })));
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
    private void AddJunctionArm()
    {
        var junction=Selected?.Junction;
        if(junction is null || junction.Arms.Count>=16) return;
        var id=selectedId;
        editor.Edit(d=>
        {
            var j=d.Elements.First(e=>e.Id==id).Junction!;
            j.Arms.Add(new JunctionArm { AngleDegrees=(j.Arms.Last().AngleDegrees+45)%360, LengthMm=450 });
        });
        Draw();
    }
    private void RemoveJunctionArm()
    {
        var junction=Selected?.Junction;
        if(junction is null || junction.Arms.Count<=1) return;
        var id=selectedId;
        editor.Edit(d=>d.Elements.First(e=>e.Id==id).Junction!.Arms.RemoveAt(junction.Arms.Count-1));
        Draw();
    }
    private void ChangeRadius(double delta)
    {
        var junction=Selected?.Junction;
        if(junction is null || junction.Type!=JunctionType.Roundabout) return;
        var id=selectedId;
        editor.Edit(d=>
        {
            var j=d.Elements.First(e=>e.Id==id).Junction!;
            j.RadiusMm=Math.Max(25,j.RadiusMm+delta);
        });
        Draw();
    }
    private void EditSelectedArm(Action<JunctionArm> operation)
    {
        var j=Selected?.Junction;
        if(j is null || selectedArmIndex<0 || selectedArmIndex>=j.Arms.Count) { status.Text="Select a junction arm endpoint first"; return; }
        var id=selectedId;
        var armId=j.Arms[selectedArmIndex].Id;
        editor.Edit(d=>operation(d.Elements.First(e=>e.Id==id).Junction!.Arms.First(a=>a.Id==armId)));
        Draw();
    }
    private void AddArmBend()
    {
        var element=Selected;
        if(element?.Junction is null || selectedArmIndex>=element.Junction.Arms.Count) return;
        var arm=element.Junction.Arms[selectedArmIndex];
        if(arm.Bends.Count>=32) return;
        var start=arm.Bends.Count==0 ? JunctionGeometry.ArmStart(element,arm) :
            (element.X+arm.Bends[^1].X,element.Y+arm.Bends[^1].Y);
        var end=JunctionGeometry.Endpoint(element,arm);
        EditSelectedArm(a=>a.Bends.Add(new JunctionPoint {
            X=(start.Item1+end.X)/2-element.X,
            Y=(start.Item2+end.Y)/2-element.Y
        }));
    }
    private void RemoveArmBend() => EditSelectedArm(a=> { if(a.Bends.Count>0) a.Bends.RemoveAt(a.Bends.Count-1); });
    private void ToggleArmSmooth() => EditSelectedArm(a=>a.Smooth=!a.Smooth);
    private void ToggleArmArrow() => EditSelectedArm(a=>a.ShowArrow=!a.ShowArrow);
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
        originC1X=element.Control1X; originC1Y=element.Control1Y; originC2X=element.Control2X; originC2Y=element.Control2Y;
        activeHandle=0;
        editor.Begin(); dragging=true;
        e.Pointer.Capture(canvas);
        e.Handled=true;
        Draw();
    }
    private void StartHandle(SignElement element, int handle, PointerPressedEventArgs e)
    {
        if(!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;
        selectedId=element.Id;
        dragStart=e.GetPosition(canvas);
        originX=element.X; originY=element.Y; originX2=element.X2; originY2=element.Y2;
        originC1X=element.Control1X; originC1Y=element.Control1Y;
        originC2X=element.Control2X; originC2Y=element.Control2Y;
        activeHandle=handle;
        if(element.Kind==ElementKind.Junction && element.Junction is not null)
        {
            if(handle>=100 && handle<200)
            {
                selectedArmIndex=handle-100;
                var endpoint=JunctionGeometry.Endpoint(element,element.Junction.Arms[selectedArmIndex]);
                originHandleX=endpoint.X; originHandleY=endpoint.Y;
            }
            else if(handle>=1000 && handle<2000)
            {
                var arm=element.Junction.Arms[(handle-1000)/32];
                selectedArmIndex=(handle-1000)/32;
                var bend=arm.Bends[(handle-1000)%32];
                originHandleX=element.X+bend.X;
                originHandleY=element.Y+bend.Y;
            }
            else if(handle>=2000)
            {
                var vertex=element.Junction.RingVertices[handle-2000];
                originRingRadius=vertex.RadiusMm;
            }
        }
        editor.Begin(); dragging=true;
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
        double dx=(point.X-dragStart.X)/Scale, dy=(point.Y-dragStart.Y)/Scale;
        if(element.Kind==ElementKind.Junction && activeHandle>=100)
        {
            if(element.Junction is not null && activeHandle<1000)
            {
                var index=activeHandle-100;
                if(index<element.Junction.Arms.Count)
                    JunctionGeometry.MoveEndpoint(element, element.Junction.Arms[index],
                        SignEditorSession.Snap(originHandleX+dx,Grid),
                        SignEditorSession.Snap(originHandleY+dy,Grid));
            }
            else if(element.Junction is not null && activeHandle<2000)
            {
                var index=(activeHandle-1000)/32;
                var bendIndex=(activeHandle-1000)%32;
                if(index<element.Junction.Arms.Count && bendIndex<element.Junction.Arms[index].Bends.Count)
                {
                    var bend=element.Junction.Arms[index].Bends[bendIndex];
                    bend.X=SignEditorSession.Snap(originHandleX+dx,Grid)-element.X;
                    bend.Y=SignEditorSession.Snap(originHandleY+dy,Grid)-element.Y;
                }
            }
            else if(element.Junction is not null && activeHandle>=2000 && activeHandle-2000<element.Junction.RingVertices.Count)
            {
                var vertex=element.Junction.RingVertices[activeHandle-2000];
                var rad=vertex.AngleDegrees*Math.PI/180;
                var delta=dx*Math.Sin(rad)-dy*Math.Cos(rad);
                vertex.RadiusMm=Math.Max(25,SignEditorSession.Snap(originRingRadius+delta,Grid));
            }
        }
        else if(element.Kind==ElementKind.CubicBezier && activeHandle!=0)
        {
            var x=SignEditorSession.Snap(dx+(activeHandle switch { 1=>originX, 2=>originC1X, 3=>originC2X, _=>originX2 }),Grid);
            var y=SignEditorSession.Snap(dy+(activeHandle switch { 1=>originY, 2=>originC1Y, 3=>originC2Y, _=>originY2 }),Grid);
            switch(activeHandle)
            {
                case 1: element.X=x; element.Y=y; break;
                case 2: element.Control1X=x; element.Control1Y=y; break;
                case 3: element.Control2X=x; element.Control2Y=y; break;
                case 4: element.X2=x; element.Y2=y; break;
            }
        }
        else
        {
            element.X=SignEditorSession.Snap(originX+dx,Grid);
            element.Y=SignEditorSession.Snap(originY+dy,Grid);
            var moveX=element.X-originX; var moveY=element.Y-originY;
            if(element.Kind is ElementKind.Line or ElementKind.CubicBezier)
            {
                element.X2=originX2+moveX;
                element.Y2=originY2+moveY;
            }
            if(element.Kind==ElementKind.CubicBezier)
            {
                element.Control1X=originC1X+moveX; element.Control1Y=originC1Y+moveY;
                element.Control2X=originC2X+moveX; element.Control2Y=originC2Y+moveY;
            }
        }
        Draw();
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
        dragging=false; panning=false; Draw();
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
    private Avalonia.Controls.Shapes.Path JunctionVisual(SignElement element,double scale)
    {
        return new Avalonia.Controls.Shapes.Path {
            Data=Geometry.Parse(JunctionGeometry.PathData(element,scale,offsetX,offsetY)),
            Stroke=Brush(element.Stroke),
            StrokeThickness=Math.Max(element.StrokeWidthMm*scale,4),
            Fill=Brushes.Transparent
        };
    }
    private void DrawJunctionHandles(SignElement element, double scale)
    {
        if(element.Junction is null) return;
        foreach(var arm in element.Junction.Arms.Select((value,index)=>(value,index)))
        {
            var end=JunctionGeometry.Endpoint(element,arm.value);
            var handle=new Ellipse { Width=14, Height=14, Fill=Brushes.Orange, Stroke=Brushes.White, StrokeThickness=2 };
            Canvas.SetLeft(handle,offsetX+end.X*scale-7);
            Canvas.SetTop(handle,offsetY+end.Y*scale-7);
            var index=arm.index;
            handle.PointerPressed+=(_,e)=>StartHandle(element,100+index,e);
            canvas.Children.Add(handle);
            for(var bendIndex=0;bendIndex<arm.value.Bends.Count;bendIndex++)
            {
                var bend=arm.value.Bends[bendIndex];
                var point=new Rectangle { Width=12,Height=12,Fill=Brushes.Cyan,Stroke=Brushes.Black,StrokeThickness=1 };
                Canvas.SetLeft(point,offsetX+(element.X+bend.X)*scale-6);
                Canvas.SetTop(point,offsetY+(element.Y+bend.Y)*scale-6);
                var handleIndex=1000+index*32+bendIndex;
                point.PointerPressed+=(_,e)=>StartHandle(element,handleIndex,e);
                canvas.Children.Add(point);
            }
        }
        for(var i=0;i<element.Junction.RingVertices.Count;i++)
        {
            var vertex=element.Junction.RingVertices[i];
            var rad=vertex.AngleDegrees*Math.PI/180;
            var point=new Rectangle { Width=12,Height=12,Fill=Brushes.Yellow,Stroke=Brushes.Black,StrokeThickness=1 };
            Canvas.SetLeft(point,offsetX+(element.X+Math.Sin(rad)*vertex.RadiusMm)*scale-6);
            Canvas.SetTop(point,offsetY+(element.Y-Math.Cos(rad)*vertex.RadiusMm)*scale-6);
            var handleIndex=2000+i;
            point.PointerPressed+=(_,e)=>StartHandle(element,handleIndex,e);
            canvas.Children.Add(point);
        }
        var centre=new Ellipse { Width=10,Height=10,Fill=Brushes.White,Stroke=Brushes.Orange,StrokeThickness=2,IsHitTestVisible=false };
        Canvas.SetLeft(centre,offsetX+element.X*scale-5);
        Canvas.SetTop(centre,offsetY+element.Y*scale-5);
        canvas.Children.Add(centre);
    }
    private void DrawCurveHandles(SignElement element, double scale)
    {
        void Guide(double ax,double ay,double bx,double by)
        {
            var guide=new Line {
                StartPoint=new Point(offsetX+ax*scale,offsetY+ay*scale),
                EndPoint=new Point(offsetX+bx*scale,offsetY+by*scale),
                Stroke=Brushes.Orange, StrokeThickness=1, IsHitTestVisible=false
            };
            canvas.Children.Add(guide);
        }
        Guide(element.X,element.Y,element.Control1X,element.Control1Y);
        Guide(element.X2,element.Y2,element.Control2X,element.Control2Y);
        void Handle(int index,double x,double y)
        {
            var point=new Ellipse {
                Width=13,Height=13,
                Fill=index is 2 or 3 ? Brushes.Orange : Brushes.White,
                Stroke=Brushes.DarkOrange,StrokeThickness=2
            };
            Canvas.SetLeft(point,offsetX+x*scale-6.5);
            Canvas.SetTop(point,offsetY+y*scale-6.5);
            point.PointerPressed+=(_,e)=>StartHandle(element,index,e);
            canvas.Children.Add(point);
        }
        Handle(1,element.X,element.Y);
        Handle(2,element.Control1X,element.Control1Y);
        Handle(3,element.Control2X,element.Control2Y);
        Handle(4,element.X2,element.Y2);
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
                ElementKind.Junction => JunctionVisual(element,s),
                ElementKind.CubicBezier => new Avalonia.Controls.Shapes.Path {
                    Data=Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
                        "M {0} {1} C {2} {3} {4} {5} {6} {7}",
                        offsetX+element.X*s,offsetY+element.Y*s,
                        offsetX+element.Control1X*s,offsetY+element.Control1Y*s,
                        offsetX+element.Control2X*s,offsetY+element.Control2Y*s,
                        offsetX+element.X2*s,offsetY+element.Y2*s)),
                    Stroke=Brush(element.Stroke), StrokeThickness=Math.Max(element.StrokeWidthMm*s,4),
                    Fill=Brushes.Transparent
                },
                _=>null
            };
            if(visual is null) continue;
            Canvas.SetLeft(visual,(element.Kind is ElementKind.CubicBezier or ElementKind.Junction) ? 0 : offsetX+element.X*s);
            Canvas.SetTop(visual,(element.Kind is ElementKind.CubicBezier or ElementKind.Junction) ? 0 : offsetY+element.Y*s);
            visual.PointerPressed += (_,e)=>StartDrag(element,e);
            canvas.Children.Add(visual);
            if(element.Kind==ElementKind.Junction)
            {
                var arrowPath=JunctionGeometry.ArrowPathData(element,s,offsetX,offsetY);
                if(arrowPath.Length>0)
                {
                    var arrows=new Avalonia.Controls.Shapes.Path {
                        Data=Geometry.Parse(arrowPath),
                        Fill=Brush(element.Stroke), StrokeThickness=0, IsHitTestVisible=false
                    };
                    canvas.Children.Add(arrows);
                }
            }
            if(selectedId==element.Id && element.Kind==ElementKind.CubicBezier)
                DrawCurveHandles(element,s);
            if(selectedId==element.Id && element.Kind==ElementKind.Junction)
                DrawJunctionHandles(element,s);
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
