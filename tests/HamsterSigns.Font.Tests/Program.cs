using HamsterSigns.Core;
using HamsterSigns.Desktop;

var sampleFiles=new[]
{
    @"C:\Windows\Fonts\arial.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf"
};
var fontPath=sampleFiles.FirstOrDefault(File.Exists);
if(fontPath is null)
{
    Console.WriteLine("SKIP font-outline smoke test: no known system font found.");
    return;
}
var renderer=new FontOutlineRenderer();
renderer.SetFont(SignFontRole.TransportMedium,fontPath);
var document=new SignDocument
{
    Elements=[new SignElement {
        Kind=ElementKind.Text,Text="Kendal A6",X=80,Y=120,FontSizeMm=100,
        FontRole=SignFontRole.TransportMedium,TextColor="#ffffff"
    }]
};
var text=document.Elements[0];
var path=renderer.BuildPath(text,document.Background);
if(string.IsNullOrWhiteSpace(path) || !path.Contains('M'))
    throw new Exception("No glyph contours returned.");
var screenPath=renderer.BuildPath(text,document.Background,0.5,20,30);
if(string.IsNullOrWhiteSpace(screenPath) || screenPath==path)
    throw new Exception("Canvas transform did not change glyph geometry.");
var svg=SvgExporter.Export(document,e=>renderer.BuildPath(e,document.Background));
if(!svg.Contains("<path") || svg.Contains("<text"))
    throw new Exception("SVG must contain font-independent outlines.");
if(!renderer.CanOutline(text,document.Background))
    throw new Exception("Loaded font role not detected.");
Console.WriteLine($"PASS font contour preview and SVG export using {Path.GetFileName(fontPath)}.");
