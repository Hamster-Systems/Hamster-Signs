using System.Globalization;

namespace HamsterSigns.Core;

/// <summary>Original, simplified drafting symbols. These are not copies of prescribed TSRGD drawings.</summary>
public enum SignSymbolKind { Parking, Information, Hospital, Railway, Aircraft }

public static class SignSymbolLibrary
{
    public static SignElement Create(SignSymbolKind kind,double x,double y,double size=200,string color="#ffffff")
    {
        if(!Enum.IsDefined(kind) || !double.IsFinite(size) || size<=0)
            throw new ArgumentOutOfRangeException(nameof(size));
        return new SignElement {
            Kind=ElementKind.Symbol,SymbolKind=kind,X=x,Y=y,
            Width=size,Height=size,Fill=color,Stroke=color,StrokeWidthMm=0
        };
    }

    // Paths use a normalized 100x100 tile. This library deliberately contains
    // original approximations rather than unlicensed working-drawing artwork.
    private static string Normalized(SignSymbolKind kind) => kind switch
    {
        SignSymbolKind.Parking => "M 26 82 L 26 18 L 55 18 C 89 18 89 61 55 61 L 26 61",
        SignSymbolKind.Information => "M 50 42 L 50 80 M 50 18 L 50 25",
        SignSymbolKind.Hospital => "M 50 15 L 50 85 M 15 50 L 85 50",
        SignSymbolKind.Railway => "M 28 22 L 72 22 L 72 72 L 28 72 Z M 31 84 L 46 70 M 69 84 L 54 70 M 34 35 L 66 35 M 34 55 L 66 55",
        SignSymbolKind.Aircraft => "M 49 10 L 55 43 L 87 62 L 87 72 L 54 60 L 54 79 L 66 88 L 66 94 L 50 89 L 34 94 L 34 88 L 46 79 L 46 60 L 13 72 L 13 62 L 45 43 Z",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string PathData(SignElement element,double scale=1,double tx=0,double ty=0)
    {
        if(element.Kind!=ElementKind.Symbol) throw new ArgumentException("Not a symbol",nameof(element));
        var x=element.X*scale+tx;
        var y=element.Y*scale+ty;
        // SVG transform is applied to normalized geometry in the exact same way
        // in desktop preview and export. Avoid duplicating or distorting commands.
        string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        return Normalized(element.SymbolKind);
    }

    public static string Transform(SignElement element,double scale=1,double tx=0,double ty=0)
    {
        string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        return $"translate({F(element.X*scale+tx)} {F(element.Y*scale+ty)}) scale({F(element.Width*scale/100)} {F(element.Height*scale/100)})";
    }

    public static bool Valid(SignElement element) =>
        element.Kind==ElementKind.Symbol && Enum.IsDefined(element.SymbolKind) &&
        double.IsFinite(element.Width) && double.IsFinite(element.Height) &&
        element.Width>0 && element.Height>0;
}
