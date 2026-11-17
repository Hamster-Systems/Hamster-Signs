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
    // Populated by map-type templates. Links the legend to an editable road arm.
    public Guid? LinkedMapArmId { get; set; }
}

public sealed class DirectionSignSpecification
{
    public double XHeightMm { get; set; } = 100;
    public SignColour Background { get; set; } = SignColour.PrimaryGreen;
    public bool RoundDimensions { get; set; } = true;
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
        warnings.AddRange(DirectionLayoutRules.Audit(design));
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
        RoutePanel.Primary => ("#006b45","#f7df32"),
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
        var border=DirectionLayoutRules.MainBorderWidth(x);
        var padding=border+DirectionLayoutRules.OuterTileClearance(x);
        var horizontalGap=DirectionLayoutRules.LegendPatchGap(x);
        // Provisional row and arrow style; no claim of a sign-type approval.
        var arrowSlot=6*sw;
        var tileHeight=TransportLetterMetrics.TileHeight(x);
        var patchHeight=tileHeight+2*DirectionLayoutRules.RoutePatchInset(x);
        var rowHeight=patchHeight;
        var rowGap=2*sw;
        var maxPlace=spec.Destinations.Max(row=>Width(row.Place,x));
        var hasRoute=spec.Destinations.Any(row=>!string.IsNullOrWhiteSpace(row.Route));
        double RouteItemWidth(DirectionDestination row)
        {
            if(string.IsNullOrWhiteSpace(row.Route)) return 0;
            var inset=row.RouteStyle==RoutePanel.None ? 0 : DirectionLayoutRules.RoutePatchInset(x);
            return Width(row.Route,x)+2*inset;
        }
        var maxRoute=hasRoute ? spec.Destinations.Max(RouteItemWidth) : 0d;
        var rawWidth=2*padding+arrowSlot+horizontalGap+maxPlace+
            (hasRoute ? horizontalGap+maxRoute : 0);
        var rawHeight=2*padding+spec.Destinations.Count*rowHeight+
            (spec.Destinations.Count-1)*rowGap;
        var width=DirectionLayoutRules.RoundUp(Math.Max(720,rawWidth),x,spec.RoundDimensions);
        var height=DirectionLayoutRules.RoundUp(rawHeight,x,spec.RoundDimensions);
        // Chapter 7 section 2.6.3: distribute any optional rounding equally.
        var left=padding+(width-Math.Max(720,rawWidth))/2;
        var top=padding+(height-rawHeight)/2;
        var foreground=SignPalette.Foreground(spec.Background);
        var document=new SignDocument {
            Title=title,DrawingNumber=drawingNumber,Background=spec.Background,
            WidthMm=width,HeightMm=height,DirectionDesign=spec,Elements=[]
        };
        // Border centreline is inset by half its width; outer edge lies on sign face.
        document.Elements.Add(new SignElement {
            Kind=ElementKind.Panel,X=border/2,Y=border/2,
            Width=width-border,Height=height-border,
            CornerRadiusMm=border,Fill="none",Stroke=foreground,StrokeWidthMm=border
        });

        var textX=left+arrowSlot+horizontalGap;
        for(var i=0;i<spec.Destinations.Count;i++)
        {
            var row=spec.Destinations[i];
            var rowTop=top+i*(rowHeight+rowGap);
            var tileTop=rowTop+(rowHeight-tileHeight)/2;
            var middle=rowTop+rowHeight/2;
            // Symbol centres are offset upwards by 0.5 sw (Chapter 7, 3.5.1).
            var symbolMiddle=middle-sw*.5;
            var tip=left+arrowSlot/2;
            var arrowSize=5.5*sw;
            var arrowStroke=Math.Max(5,sw*.7);
            switch(row.Arrow)
            {
                case DirectionArrow.Ahead:
                    document.Elements.Add(Line(tip,symbolMiddle+arrowSize*.35,tip,symbolMiddle-arrowSize*.35,arrowStroke,foreground));
                    document.Elements.Add(Line(tip,symbolMiddle-arrowSize*.35,tip-arrowSize*.25,symbolMiddle-arrowSize*.08,arrowStroke,foreground));
                    document.Elements.Add(Line(tip,symbolMiddle-arrowSize*.35,tip+arrowSize*.25,symbolMiddle-arrowSize*.08,arrowStroke,foreground));
                    break;
                case DirectionArrow.Left:
                    document.Elements.Add(Line(tip+arrowSize*.36,symbolMiddle,tip-arrowSize*.36,symbolMiddle,arrowStroke,foreground));
                    document.Elements.Add(Line(tip-arrowSize*.36,symbolMiddle,tip-arrowSize*.07,symbolMiddle-arrowSize*.26,arrowStroke,foreground));
                    document.Elements.Add(Line(tip-arrowSize*.36,symbolMiddle,tip-arrowSize*.07,symbolMiddle+arrowSize*.26,arrowStroke,foreground));
                    break;
                case DirectionArrow.Right:
                    document.Elements.Add(Line(tip-arrowSize*.36,symbolMiddle,tip+arrowSize*.36,symbolMiddle,arrowStroke,foreground));
                    document.Elements.Add(Line(tip+arrowSize*.36,symbolMiddle,tip+arrowSize*.07,symbolMiddle-arrowSize*.26,arrowStroke,foreground));
                    document.Elements.Add(Line(tip+arrowSize*.36,symbolMiddle,tip+arrowSize*.07,symbolMiddle+arrowSize*.26,arrowStroke,foreground));
                    break;
            }

            var normalFont=spec.Background is SignColour.NonPrimaryWhite or SignColour.TemporaryYellow
                ? SignFontRole.TransportHeavy : SignFontRole.TransportMedium;
            // Text uses its existing Y-origin convention; tile top is an authored
            // layout position, not a claim that the loaded glyph contour starts there.
            document.Elements.Add(Text(row.Place,textX,tileTop,x,foreground,normalFont));

            if(!string.IsNullOrWhiteSpace(row.Route))
            {
                var itemWidth=RouteItemWidth(row);
                var itemX=width-left-itemWidth;
                var (fill,patchForeground)=PanelColors(row.RouteStyle,spec.Background);
                var routeColor=row.RouteStyle==RoutePanel.None
                    ? (spec.Background==SignColour.PrimaryGreen ? "#f7df32" : foreground)
                    : patchForeground;
                var textInset=0d;
                if(row.RouteStyle!=RoutePanel.None)
                {
                    textInset=DirectionLayoutRules.RoutePatchInset(x);
                    var needsBorder=DirectionLayoutRules.NeedsWhitePatchBorder(row.RouteStyle,spec.Background);
                    document.Elements.Add(new SignElement {
                        Kind=ElementKind.Panel,X=itemX,Y=rowTop,
                        Width=itemWidth,Height=patchHeight,
                        CornerRadiusMm=DirectionLayoutRules.PatchCornerRadius(x),
                        Fill=fill,Stroke=needsBorder ? "#ffffff" : "none",
                        StrokeWidthMm=needsBorder ? DirectionLayoutRules.DarkPatchBorderWidth(x) : 0
                    });
                }
                var routeFont=row.RouteStyle==RoutePanel.Motorway ? SignFontRole.MotorwayPermanent :
                    row.RouteStyle==RoutePanel.NonPrimary ? SignFontRole.TransportHeavy : normalFont;
                document.Elements.Add(Text(row.Route,itemX+textInset,tileTop,x,routeColor,routeFont));
            }
        }
        return document;
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
