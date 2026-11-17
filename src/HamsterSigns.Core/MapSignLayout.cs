namespace HamsterSigns.Core;

/// <summary>
/// Provisional map-type direction-sign composition with separated legend zones.
/// Each route label and its patch share a calculated row box. This is a layout
/// editor, not a certified TSRGD Schedule 12 map-type sign generator.
/// </summary>
public static class MapSignLayout
{
    public sealed record LegendBox(Guid ArmId, DirectionArrow Direction,
        double X, double Y, double Width, double Height);

    private static double TextWidth(string value,double x) =>
        TransportLetterMetrics.Measure(value,x).WidthMm;

    public static SignDocument Build(DirectionSignSpecification spec,SignTemplateKind kind,
        string title,string drawingNumber)
    {
        var errors=DirectionSignLayout.Validate(spec);
        if(errors.Count!=0) throw new ArgumentException(string.Join("; ",errors),nameof(spec));
        if(spec.Destinations.Count>6) throw new ArgumentException("Map layout supports up to six rows.",nameof(spec));

        var x=spec.XHeightMm;
        var sw=TransportLetterMetrics.StrokeWidth(x);
        var border=DirectionLayoutRules.MainBorderWidth(x);
        var padding=border+DirectionLayoutRules.OuterTileClearance(x);
        var gap=DirectionLayoutRules.LegendPatchGap(x);
        var patchInset=DirectionLayoutRules.RoutePatchInset(x);
        var tileH=TransportLetterMetrics.TileHeight(x);
        var rowH=tileH+2*patchInset;
        var pitch=rowH+3*sw;
        var leftRows=spec.Destinations.Where(d=>d.Arrow==DirectionArrow.Left).ToList();
        var rightRows=spec.Destinations.Where(d=>d.Arrow==DirectionArrow.Right).ToList();
        var aheadRows=spec.Destinations.Where(d=>d.Arrow==DirectionArrow.Ahead).ToList();
        double ItemWidth(DirectionDestination d)
        {
            var width=TextWidth(d.Place,x);
            if(!string.IsNullOrWhiteSpace(d.Route))
                width+=gap+TextWidth(d.Route,x)+(d.RouteStyle==RoutePanel.None ? 0 : 2*patchInset);
            return width;
        }
        var leftW=leftRows.Count==0 ? 0 : leftRows.Max(ItemWidth);
        var rightW=rightRows.Count==0 ? 0 : rightRows.Max(ItemWidth);
        var aheadW=aheadRows.Count==0 ? 0 : aheadRows.Max(ItemWidth);
        var leftZone=Math.Max(4*x,leftW);
        var rightZone=Math.Max(4*x,Math.Max(rightW,aheadW));
        var centreX=padding+leftZone+4.5*x;
        var rightX=centreX+4.5*x;
        var rawW=rightX+rightZone+padding;
        var width=DirectionLayoutRules.RoundUp(rawW,x,spec.RoundDimensions);
        // Distribute additional width to the left/right border clearances.
        var extraX=(width-rawW)/2;
        centreX+=extraX;
        rightX+=extraX;

        var upperRows=Math.Max(1,aheadRows.Count);
        var sideRows=Math.Max(1,Math.Max(leftRows.Count,rightRows.Count));
        var centreY=Math.Max(padding+upperRows*pitch+2*x,
            padding+rowH/2+(sideRows-1)*pitch/2);
        var sideStackHeight=(sideRows-1)*pitch+rowH;
        var rawH=centreY+Math.Max(2.5*x,sideStackHeight/2)+3*x+padding;
        var height=DirectionLayoutRules.RoundUp(rawH,x,spec.RoundDimensions);
        var extraY=(height-rawH)/2;
        centreY+=extraY;

        var foreground=SignPalette.Foreground(spec.Background);
        var normalFont=spec.Background is SignColour.NonPrimaryWhite or SignColour.TemporaryYellow
            ? SignFontRole.TransportHeavy : SignFontRole.TransportMedium;
        var doc=new SignDocument {
            Title=title,DrawingNumber=drawingNumber,WidthMm=width,HeightMm=height,
            Background=spec.Background,DirectionDesign=spec,Template=kind,
            Elements=[new SignElement {
                Kind=ElementKind.Panel, X=border/2,Y=border/2,
                Width=width-border,Height=height-border,
                CornerRadiusMm=border,Fill="none",Stroke=foreground,StrokeWidthMm=border
            }]
        };

        // The network and its legends remain independent editable objects;
        // stable arm references persist within each destination specification.
        var diagram=JunctionGeometry.Roundabout(centreX,centreY,2.5*sw,Math.Max(10,sw));
        diagram.Stroke=foreground;
        diagram.Junction!.Arms.Clear();
        var labels=new List<SignElement>();
        var patches=new List<SignElement>();
        var leftIndex=0;
        var rightIndex=0;
        var aheadIndex=0;

        foreach(var destination in spec.Destinations)
        {
            double labelX;
            double labelTop;
            double endX;
            double endY;
            double bendX;
            double bendY;
            var sideIndex=destination.Arrow switch {
                DirectionArrow.Left=>leftIndex++,
                DirectionArrow.Right=>rightIndex++,
                _=>aheadIndex++
            };
            if(destination.Arrow==DirectionArrow.Ahead)
            {
                labelX=centreX+1.8*x;
                labelTop=padding+sideIndex*pitch+extraY;
                endX=centreX;
                endY=labelTop+rowH/2;
                bendX=centreX;
                bendY=centreY-2.1*x;
            }
            else
            {
                var count=destination.Arrow==DirectionArrow.Left ? leftRows.Count : rightRows.Count;
                labelTop=centreY+(sideIndex-(count-1)/2.0)*pitch-rowH/2;
                labelX=destination.Arrow==DirectionArrow.Left
                    ? centreX-4.5*x-ItemWidth(destination)
                    : rightX;
                endX=destination.Arrow==DirectionArrow.Left ? centreX-1.8*x : centreX+1.8*x;
                endY=labelTop+rowH/2;
                bendX=centreX+(destination.Arrow==DirectionArrow.Left ? -2.5*x : 2.5*x);
                bendY=centreY;
            }

            var arm=new JunctionArm {
                AngleDegrees=destination.Arrow switch {
                    DirectionArrow.Left=>270,DirectionArrow.Right=>90,_=>0
                },
                LengthMm=3*x,
                ShowArrow=true,Smooth=true,
                EndOffsetX=endX-centreX,EndOffsetY=endY-centreY,
                Bends=[new JunctionPoint {X=bendX-centreX,Y=bendY-centreY}]
            };
            diagram.Junction.Arms.Add(arm);
            destination.LinkedMapArmId=arm.Id;

            var placeY=labelTop+(rowH-tileH)/2;
            var placeElement=new SignElement {
                Kind=ElementKind.Text,X=labelX,Y=placeY,Text=destination.Place,
                FontSizeMm=x,TextColor=foreground,FontRole=normalFont
            };
            labels.Add(placeElement);
            destination.MapPlaceElementId=placeElement.Id;
            if(!string.IsNullOrWhiteSpace(destination.Route))
            {
                var routeX=labelX+TextWidth(destination.Place,x)+gap;
                var hasPatch=destination.RouteStyle!=RoutePanel.None;
                var routeTextW=TextWidth(destination.Route,x);
                var textInset=hasPatch ? patchInset : 0;
                var routeColor=foreground;
                if(spec.Background==SignColour.PrimaryGreen && !hasPatch)
                    routeColor="#f7df32";
                if(hasPatch)
                {
                    var (fill,text)=destination.RouteStyle switch {
                        RoutePanel.Motorway=>("#174c9a","#ffffff"),
                        RoutePanel.Primary=>("#006b45","#f7df32"),
                        RoutePanel.NonPrimary=>("#ffffff","#111111"),
                        RoutePanel.Tourist=>("#754b31","#ffffff"),
                        _=>throw new InvalidOperationException()
                    };
                    var bordered=DirectionLayoutRules.NeedsWhitePatchBorder(destination.RouteStyle,spec.Background);
                    var patchElement=new SignElement {
                        Kind=ElementKind.Panel,X=routeX,Y=labelTop,
                        Width=routeTextW+2*patchInset,Height=rowH,
                        CornerRadiusMm=DirectionLayoutRules.PatchCornerRadius(x),Fill=fill,
                        Stroke=bordered ? "#ffffff" : "none",
                        StrokeWidthMm=bordered ? DirectionLayoutRules.DarkPatchBorderWidth(x) : 0
                    };
                    patches.Add(patchElement);
                    destination.MapPatchElementId=patchElement.Id;
                    routeColor=text;
                }
                var routeElement=new SignElement {
                    Kind=ElementKind.Text,X=routeX+textInset,Y=placeY,Text=destination.Route,
                    FontSizeMm=x,TextColor=routeColor,
                    FontRole=destination.RouteStyle==RoutePanel.Motorway
                        ? SignFontRole.MotorwayPermanent
                        : destination.RouteStyle==RoutePanel.NonPrimary
                            ? SignFontRole.TransportHeavy : normalFont
                };
                labels.Add(routeElement);
                destination.MapRouteElementId=routeElement.Id;
            }
        }
        diagram.Junction.Arms.Add(new JunctionArm {
            AngleDegrees=180,LengthMm=3*x,ShowArrow=false,
            EndOffsetX=0,EndOffsetY=height-padding-sw-centreY
        });
        // Render junction before patches and legends so labels always remain legible.
        doc.Elements.Add(diagram);
        doc.Elements.AddRange(patches);
        doc.Elements.AddRange(labels);
        return doc;
    }

    public static IReadOnlyList<string> Audit(SignDocument doc)
    {
        if(doc.Template is not (SignTemplateKind.MapAdvance or SignTemplateKind.MotorwayMap))
            return [];
        var warnings=new List<string>();
        if(doc.DirectionDesign is null) return ["Map sign has no editable destination specification."];
        var network=doc.Elements.FirstOrDefault(e=>e.Kind==ElementKind.Junction);
        if(network?.Junction is null) return ["Map sign has no editable road network."];
        var ids=network.Junction.Arms.Select(a=>a.Id).ToHashSet();
        foreach(var row in doc.DirectionDesign.Destinations)
            if(!row.LinkedMapArmId.HasValue || !ids.Contains(row.LinkedMapArmId.Value))
                warnings.Add($"Destination '{row.Place}' has no matching road arm.");

        // Check rectangular text and patch tiles for overlap (not actual glyph
        // outlines). The final road/legend clearance requires manual review.
        var elements=doc.Elements.Where(e=>e.Kind==ElementKind.Text ||
            (e.Kind==ElementKind.Panel && e.Fill!="none")).ToList();
        for(var i=0;i<elements.Count;i++)
            for(var j=i+1;j<elements.Count;j++)
            {
                var a=elements[i];var b=elements[j];
                // Containment of text inside its own patch is intentional.
                if(a.Kind!=b.Kind) continue;
                double W(SignElement e) => e.Kind==ElementKind.Text
                    ? TextWidth(e.Text,e.FontSizeMm) : e.Width;
                double H(SignElement e) => e.Kind==ElementKind.Text
                    ? TransportLetterMetrics.TileHeight(e.FontSizeMm) : e.Height;
                if(a.X<b.X+W(b) && a.X+W(a)>b.X &&
                   a.Y<b.Y+H(b) && a.Y+H(a)>b.Y)
                    warnings.Add($"Potential overlap between {a.Kind} elements {a.Id} and {b.Id}.");
            }
        return warnings;
    }
}
