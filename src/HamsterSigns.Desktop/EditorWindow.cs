using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using HamsterSigns.Core;

namespace HamsterSigns.Desktop;

public sealed class EditorWindow : Window
{
    private SignDocument document = SignDocument.Example();
    private readonly Canvas canvas = new() { Width = 900, Height = 650, Background = Brushes.White };
    private readonly TextBlock status = new() { Text = "Ready" };
    private readonly TextBox title = new() { Watermark = "Sign title" };
    private readonly TextBox width = new() { Watermark = "Width in mm" };
    private readonly TextBox height = new() { Watermark = "Height in mm" };
    private readonly ComboBox colour = new() { ItemsSource = Enum.GetValues<SignColour>() };
    private SignElement? selected;
    private readonly TextBox textEditor = new() { Watermark = "Selected element text" };

    public EditorWindow()
    {
        Title = "Hamster Signs — early CAD prototype (NOT TSRGD certified)";
        Width = 1390; Height = 850; MinWidth = 900; MinHeight = 600;
        var tools = new StackPanel { Margin = new Thickness(16), Spacing = 9 };
        tools.Children.Add(new TextBlock { Text = "HAMSTER SIGNS", FontSize = 23, FontWeight = FontWeight.Bold });
        tools.Children.Add(new TextBlock { Text = "Properties" });
        title.Text = document.Title; width.Text = document.WidthMm.ToString(); height.Text = document.HeightMm.ToString();
        colour.SelectedItem = document.Background;
        tools.Children.Add(title); tools.Children.Add(width); tools.Children.Add(height); tools.Children.Add(colour);
        tools.Children.Add(Button("Apply properties", Apply));
        tools.Children.Add(new Separator());
        tools.Children.Add(Button("Add text", () => Add(new() { Kind=ElementKind.Text, X=300, Y=250, FontSizeMm=135, Text="Destination" })));
        tools.Children.Add(Button("Add panel", () => Add(new() { Kind=ElementKind.Panel, X=200, Y=200, Width=400, Height=175, Fill="#174c9a" })));
        tools.Children.Add(Button("Add line", () => Add(new() { Kind=ElementKind.Line, X=200, Y=500, X2=900, Y2=500, StrokeWidthMm=55 })));
        tools.Children.Add(Button("Add roundabout", () => Add(new() { Kind=ElementKind.Ellipse, X=300, Y=500, Width=520, Height=520, StrokeWidthMm=60 })));
        tools.Children.Add(Button("Delete selected", () => { if (selected is not null) document.Elements.Remove(selected); selected=null; Draw(); }));
        tools.Children.Add(textEditor);
        tools.Children.Add(Button("Update selected text", () => { if (selected?.Kind == ElementKind.Text) selected.Text=textEditor.Text ?? ""; Draw(); }));
        tools.Children.Add(new Separator());
        tools.Children.Add(Button("Open project", Open));
        tools.Children.Add(Button("Save project", Save));
        tools.Children.Add(Button("Export SVG", Export));
        tools.Children.Add(Button("Validate geometry", () => status.Text=string.Join(" | ", SignValidator.Check(document).Select(d=>d.Message))));
        tools.Children.Add(new TextBlock { Text="Illustrative artwork only. Not for manufacturing or installing signs.", TextWrapping=TextWrapping.Wrap, Foreground=Brushes.DarkOrange });
        var sidebar = new ScrollViewer { Width=270, Content=tools };
        var work = new StackPanel { Margin=new Thickness(15), Spacing=8 };
        work.Children.Add(new TextBlock { Text="Sign face — click to select an object", FontSize=17 });
        work.Children.Add(canvas); work.Children.Add(status);
        var layout = new DockPanel();
        DockPanel.SetDock(sidebar, Dock.Left); layout.Children.Add(sidebar);
        layout.Children.Add(new ScrollViewer { Content=work });
        Content=layout;
        Draw();
    }

    private static Button Button(string label, Action action)
    {
        var b = new Button { Content=label, HorizontalAlignment=HorizontalAlignment.Stretch };
        b.Click += (_,_)=>action(); return b;
    }
    private static IBrush Brush(string value)
    {
        if (value == "none") return Brushes.Transparent;
        try { return new SolidColorBrush(Color.Parse(value)); } catch { return Brushes.White; }
    }
    private void Add(SignElement e) { document.Elements.Add(e); selected=e; Draw(); }
    private void Apply()
    {
        if (!double.TryParse(width.Text, out var w) || !double.TryParse(height.Text, out var h) || !double.IsFinite(w) || !double.IsFinite(h) || w<=0 || h<=0)
        { status.Text="Invalid sign dimensions"; return; }
        document.WidthMm=w; document.HeightMm=h; document.Title=title.Text ?? "Untitled";
        if(colour.SelectedItem is SignColour c) document.Background=c;
        Draw();
    }
    private async void Save()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title="Save project", SuggestedFileName="sign.roadsign.json" });
        if(file is null) return;
        await using var stream=await file.OpenWriteAsync();
        using var writer=new StreamWriter(stream);
        await writer.WriteAsync(SignFile.Serialize(document));
        status.Text="Project saved";
    }
    private async void Open()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new() { Title="Open project", AllowMultiple=false });
        if(files.Count==0) return;
        try
        {
            await using var stream=await files[0].OpenReadAsync();
            using var reader=new StreamReader(stream);
            document=SignFile.Deserialize(await reader.ReadToEndAsync());
            title.Text=document.Title; width.Text=document.WidthMm.ToString(); height.Text=document.HeightMm.ToString();
            colour.SelectedItem=document.Background; selected=null; Draw();
        }
        catch(Exception e) { status.Text=e.Message; }
    }
    private async void Export()
    {
        try
        {
            var svg=SvgExporter.Export(document);
            var file=await StorageProvider.SaveFilePickerAsync(new() { Title="Export sign", SuggestedFileName="sign.svg" });
            if(file is null) return;
            await using var stream=await file.OpenWriteAsync();
            using var writer=new StreamWriter(stream);
            await writer.WriteAsync(svg);
            status.Text="SVG exported";
        }
        catch(Exception e) { status.Text=e.Message; }
    }
    private void Draw()
    {
        canvas.Children.Clear();
        if(document.WidthMm<=0 || document.HeightMm<=0) return;
        double scale=Math.Min(850/document.WidthMm, 600/document.HeightMm);
        var face=new Rectangle { Width=document.WidthMm*scale, Height=document.HeightMm*scale, Fill=Brush(SignPalette.Background(document.Background)) };
        Canvas.SetLeft(face,15); Canvas.SetTop(face,15); canvas.Children.Add(face);
        foreach(var element in document.Elements)
        {
            Control? visual=element.Kind switch
            {
                ElementKind.Text => new TextBlock { Text=element.Text, FontSize=element.FontSizeMm*scale, Foreground=Brush(SignPalette.Foreground(document.Background)) },
                ElementKind.Panel => new Rectangle { Width=element.Width*scale, Height=element.Height*scale, Fill=Brush(element.Fill), Stroke=Brush(element.Stroke), StrokeThickness=element.StrokeWidthMm*scale },
                ElementKind.Ellipse => new Ellipse { Width=element.Width*scale, Height=element.Height*scale, Stroke=Brush(element.Stroke), StrokeThickness=element.StrokeWidthMm*scale, Fill=Brush(element.Fill) },
                ElementKind.Line => new Line { StartPoint=new Point(0,0), EndPoint=new Point((element.X2-element.X)*scale,(element.Y2-element.Y)*scale), Stroke=Brush(element.Stroke), StrokeThickness=element.StrokeWidthMm*scale },
                _=>null
            };
            if(visual is null) continue;
            Canvas.SetLeft(visual,15+element.X*scale); Canvas.SetTop(visual,15+element.Y*scale);
            visual.PointerPressed += (_,_) => { selected=element; textEditor.Text=element.Kind==ElementKind.Text ? element.Text : ""; status.Text=$"Selected {element.Kind}"; };
            canvas.Children.Add(visual);
        }
        status.Text=$"{document.Title} — {document.WidthMm} × {document.HeightMm} mm; {document.Elements.Count} objects";
    }
}
