using System.Globalization;

namespace HamsterSigns.Core;

public enum DirectionArrow { Left, Ahead, Right }
public enum RoutePanel { None, Motorway, Primary, NonPrimary, Tourist }

public sealed class DirectionDestination
{
    public string Place { get; set; } = "Destination";
    public string Route { get; set; } = "";
    public DirectionArrow Arrow { get; set; } = DirectionArrow.Ahead;
    public RoutePanel RouteStyle { get; set; } = RoutePanel.None;
}

public sealed class DirectionSignSpecification
{
    public double XHeightMm { get; set; } = 100;
    public SignColour Background { get; set; } = SignColour.PrimaryGreen;
    public List<DirectionDestination> Destinations { get; set; } = [];
}

/// <summary>
/// First-pass layout metrics, deliberately approximate until licensed/verified
/// Transport character tiles and Chapter 7 spacing tables are implemented.
/// </summary>
public static class DirectionSignLayout
{
    private static double Round5(double n) => Math.Ceiling(n / 5) * 5;
    private static double Width(string text,double xHeight) =>
        TransportLetterMetrics.Measure(text,xHeight).WidthMm;

    public static IReadOnlyList<string> TypographyWarnings(DirectionSignSpecification? design)
    {
        if(design?.Destinations is null || !double.IsFinite(design.XHeightMm) || design.XHeightMm<=0)
            return ["Typography cannot be assessed until a valid sign specification is supplied."];
        var warnings=new List<string>();
        foreach(var row in design.Destinations)
        {
            if(row is null) continue;
            foreach(var text in new[] {row.Place,row.Route})
            {
                if(string.IsNullOrEmpty(text)) continue;
                var result=TransportLetterMetrics.Measure(text,design.XHeightMm);
                if(!result.FullySpecified)
                    warnings.Add($"Unverified Transport tile widths for {string.Concat(result.UnverifiedCharacters)} in '{text}'.");
            }
        }
        warnings.Add("Rendered glyph outlines use a system font, not a verified Transport typeface.");
        warnings.Add("Sign borders, symbols, route patches and element spacing still require standards verification.");
        return warnings.Distinct().ToArray();
    }

    public static IReadOnlyList<string> Validate(DirectionSignSpecification? design)
    {
        var problems=new List<string>();
        if(design is null) {problems.Add("Direction sign specification missing");return problems;}
        if(!double.IsFinite(design.XHeightMm) || design.XHeightMm<25 || design.XHeightMm>400)
            problems.Add("x-height must be between 25 and 400 mm");
        if(design.Destinations is null || design.Destinations.Count is < 1 or > 20)
        {
            problems.Add("Specify between 1 and 20 destinations");
            return problems;
        }
        foreach(var row in design.Destinations)
        {
            if(row is null) { problems.Add("Null destination");continue; }
            if(string.IsNullOrWhiteSpace(row.Place) || row.Place.Length>90 || row.Place.Contains('\n') || row.Place.Contains('\r'))
                problems.Add("Destination must have 1–90 characters on one line");
            if(row.Route is null || row.Route.Length>20 || row.Route.Contains('\n') || row.Route.Contains('\r'))
                problems.Add("Route number must have at most 20 characters on one line");
            if(!Enum.IsDefined(row.Arrow) || !Enum.IsDefined(row.RouteStyle))
                problems.Add("Unrecognised arrow or route-panel style");
        }
        return problems;
    }

    private static (string Fill,string Foreground) PanelColors(RoutePanel style,SignColour sign) => style switch
    {
        RoutePanel.Motorway => ("#174c9a","#ffffff"),
        RoutePanel.Primary => ("#006b45","#ffffff"),
        RoutePanel.NonPrimary => ("#ffffff","#111111"),
        RoutePanel.Tourist => ("#754b31","#ffffff"),
        _ => (SignPalette.Background(sign),SignPalette.Foreground(sign))
    };

    private static SignElement Text(string value,double x,double y,double size,string? color=null,
        SignFontRole role=SignFontRole.Automatic) =>
        new() {Kind=ElementKind.Text,Text=value,X=x,Y=y,FontSizeMm=size,TextColor=color,FontRole=role};
    private static SignElement Line(double x,double y,double x2,double y2,double sw,string color) =>
        new() {Kind=ElementKind.Line,X=x,Y=y,X2=x2,Y2=y2,StrokeWidthMm=sw,Stroke=color};

    public static SignDocument Build(DirectionSignSpecification spec,string title="Direction sign",string drawingNumber="HS-001")
    {
        var errors=Validate(spec);
        if(errors.Count>0) throw new ArgumentException(string.Join("; ",errors),nameof(spec));

        var x=spec.XHeightMm;
        var sw=TransportLetterMetrics.StrokeWidth(x);
        var margin=Round5(1.75*x);
        var arrowW=Round5(1.5*x);
        var routePad=Round5(.35*x);
        var rowHeight=TransportLetterMetrics.TileHeight(x);
        var rowGap=Round5(2*sw);
        var width=0d;
        foreach(var row in spec.Destinations)
        {
            var placeWidth=Width(row.Place,x);
            var routeWidth=string.IsNullOrWhiteSpace(row.Route) ? 0 : Width(row.Route,x)+routePad*2;
            var needed=margin*2+arrowW+Round5(2.5*sw)+placeWidth+
                (routeWidth>0 ? Round5(2.5*sw)+routeWidth : 0);
            width=Math.Max(width,needed);
        }
        width=Round5(Math.Max(720,width));
        var height=Round5(margin*2+spec.Destinations.Count*rowHeight+
            (spec.Destinations.Count-1)*rowGap);
        var foreground=SignPalette.Foreground(spec.Background);

        var doc=new SignDocument
        {
            Title=title,
            DrawingNumber=drawingNumber,
            Background=spec.Background,
            WidthMm=width,
            HeightMm=height,
            DirectionDesign=spec,
            Elements=[]
        };
        // Border deliberately treated as editable artwork, not certified TSRGD geometry.
        doc.Elements.Add(new SignElement
        {
            Kind=ElementKind.Panel,X=sw*1.1,Y=sw*1.1,Width=width-sw*2.2,Height=height-sw*2.2,
            Fill="none",Stroke=foreground,StrokeWidthMm=sw*.35
        });

        var textX=margin+arrowW+Round5(2.5*sw);
        for(var i=0;i<spec.Destinations.Count;i++)
        {
            var row=spec.Destinations[i];
            var top=margin+i*(rowHeight+rowGap);
            var mid=top+rowHeight/2;
            var tip=margin+arrowW*.55;
            var arrowSize=arrowW*.75;
            var arrowStroke=Math.Max(5,sw*.45);
            // Stroked arrows with separate diagonal tips, no text glyph dependency.
            switch(row.Arrow)
            {
                case DirectionArrow.Ahead:
                    doc.Elements.Add(Line(tip,mid+arrowSize*.35,tip,mid-arrowSize*.35,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip,mid-arrowSize*.35,tip-arrowSize*.25,mid-arrowSize*.08,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip,mid-arrowSize*.35,tip+arrowSize*.25,mid-arrowSize*.08,arrowStroke,foreground));
                    break;
                case DirectionArrow.Left:
                    doc.Elements.Add(Line(tip+arrowSize*.36,mid,tip-arrowSize*.36,mid,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip-arrowSize*.36,mid,tip-arrowSize*.07,mid-arrowSize*.26,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip-arrowSize*.36,mid,tip-arrowSize*.07,mid+arrowSize*.26,arrowStroke,foreground));
                    break;
                case DirectionArrow.Right:
                    doc.Elements.Add(Line(tip-arrowSize*.36,mid,tip+arrowSize*.36,mid,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip+arrowSize*.36,mid,tip+arrowSize*.07,mid-arrowSize*.26,arrowStroke,foreground));
                    doc.Elements.Add(Line(tip+arrowSize*.36,mid,tip+arrowSize*.07,mid+arrowSize*.26,arrowStroke,foreground));
                    break;
            }

            var normalFont=spec.Background is SignColour.NonPrimaryWhite or SignColour.TemporaryYellow
                ? SignFontRole.TransportHeavy : SignFontRole.TransportMedium;
            doc.Elements.Add(Text(row.Place,textX,mid-x*.66,x,foreground,normalFont));
            if(!string.IsNullOrWhiteSpace(row.Route))
            {
                var routeW=Width(row.Route,x)+routePad*2;
                var routeX=width-margin-routeW;
                var (fill,panelText)=PanelColors(row.RouteStyle,spec.Background);
                if(row.RouteStyle==RoutePanel.None && spec.Background==SignColour.PrimaryGreen)
                    panelText="#f7df32";
                // For route values without a patch, draw the legend directly on the sign.
                if(row.RouteStyle!=RoutePanel.None)
                    doc.Elements.Add(new SignElement {
                        Kind=ElementKind.Panel,X=routeX,Y=mid-x*.95,Width=routeW,
                        Height=x*1.9,Fill=fill,Stroke=panelText,StrokeWidthMm=sw*.13
                    });
                var routeFont=row.RouteStyle==RoutePanel.Motorway ? SignFontRole.MotorwayPermanent :
                    row.RouteStyle==RoutePanel.NonPrimary ? SignFontRole.TransportHeavy : normalFont;
                doc.Elements.Add(Text(row.Route,routeX+routePad,mid-x*.66,x,panelText,routeFont));
            }
        }
        return doc;
    }

    public static DirectionSignSpecification Example() => new()
    {
        XHeightMm=100,Background=SignColour.PrimaryGreen,
        Destinations=[
            new(){Place="Kendal",Route="A6",Arrow=DirectionArrow.Ahead,RouteStyle=RoutePanel.None},
            new(){Place="Lancaster",Route="A591",Arrow=DirectionArrow.Left,RouteStyle=RoutePanel.None},
            new(){Place="The SOUTH",Route="(M6)",Arrow=DirectionArrow.Right,RouteStyle=RoutePanel.Motorway}
        ]
    };
}
