namespace HamsterSigns.Core;

/// <summary>Reusable layout starting points, not prescribed sign diagrams.</summary>
public static class SignTemplateCatalog
{
    public static IReadOnlyList<SignTemplateKind> Available { get; } =
        [SignTemplateKind.StackAdvance,SignTemplateKind.MapAdvance,SignTemplateKind.MotorwayMap];

    public static SignDocument Build(SignTemplateKind kind,DirectionSignSpecification input,
        string title="Advance direction sign",string drawingNumber="HS-001")
    {
        if(kind==SignTemplateKind.Freeform || !Available.Contains(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if(DirectionSignLayout.Validate(input).Count>0)
            throw new ArgumentException("Invalid destination specification.",nameof(input));
        if(kind==SignTemplateKind.StackAdvance &&
           input.Destinations.Select(d=>d.Arrow).Distinct().Count()>3)
            throw new ArgumentException("Stack-type template supports at most three directions.");
        if(kind==SignTemplateKind.StackAdvance && input.Destinations.Count>3)
            throw new ArgumentException("This initial stack template supports up to three destination rows.");
        if(kind!=SignTemplateKind.StackAdvance && input.Destinations.Count>6)
            throw new ArgumentException("Map templates currently support up to six destination rows.");

        // Copy to avoid modifying the form's original input when selecting a motorway preset.
        var spec=new DirectionSignSpecification {
            XHeightMm=input.XHeightMm,
            Background=kind==SignTemplateKind.MotorwayMap ? SignColour.MotorwayBlue : input.Background,
            RoundDimensions=input.RoundDimensions,
            Destinations=input.Destinations.Select(d=>new DirectionDestination {
                Place=d.Place, Route=d.Route, Arrow=d.Arrow,RouteStyle=d.RouteStyle
            }).ToList()
        };
        var doc=DirectionSignLayout.Build(spec,title,drawingNumber);
        doc.Template=kind;
        if(kind==SignTemplateKind.StackAdvance) return doc;

        // The map occupies its own geometric region. The destination list
        // remains aligned to the right, but each row is now linked to an
        // independent, editable route arm with an explicit endpoint.
        var x=spec.XHeightMm;
        var sw=TransportLetterMetrics.StrokeWidth(x);
        var reserved=9*x;
        var oldWidth=doc.WidthMm;
        var newWidth=DirectionLayoutRules.RoundUp(oldWidth+reserved,x,spec.RoundDimensions);
        var shifted=newWidth-oldWidth;
        doc.WidthMm=newWidth;
        doc.Elements.RemoveAll(e=>e.Kind==ElementKind.Line);
        var border=doc.Elements[0];
        border.Width+=shifted;
        foreach(var element in doc.Elements.Skip(1))
            element.X+=shifted;

        var padding=DirectionLayoutRules.MainBorderWidth(x)+DirectionLayoutRules.OuterTileClearance(x);
        var centreX=padding+reserved*.48;
        var centreY=doc.HeightMm*.53;
        var ringRadius=Math.Min(3.3*sw,Math.Min(reserved*.12,doc.HeightMm*.12));
        var roadStroke=Math.Max(10,1.0*sw);
        var map=JunctionGeometry.Roundabout(centreX,centreY,ringRadius,roadStroke);
        map.Stroke=SignPalette.Foreground(spec.Background);
        map.Junction!.Arms.Clear();

        // Collect the generated destination text objects in their original
        // row order. Route text follows place text in each row.
        var textObjects=doc.Elements.Where(e=>e.Kind==ElementKind.Text).ToArray();
        var cursor=0;
        for(var index=0;index<spec.Destinations.Count;index++)
        {
            var destination=spec.Destinations[index];
            var place=textObjects[cursor++];
            if(!string.IsNullOrWhiteSpace(destination.Route)) cursor++;
            var targetY=Math.Clamp(place.Y+x*.8,padding+sw,doc.HeightMm-padding-sw);
            var arm=new JunctionArm { AngleDegrees=destination.Arrow switch {
                DirectionArrow.Left=>270,DirectionArrow.Right=>90,_=>0
            }, LengthMm=3*x,ShowArrow=true,Smooth=true };
            double targetX, bendX, bendY;
            switch(destination.Arrow)
            {
                case DirectionArrow.Left:
                    targetX=padding+sw;
                    bendX=centreX-3.2*ringRadius;
                    bendY=centreY;
                    break;
                case DirectionArrow.Right:
                    targetX=Math.Min(place.X-2.5*sw,centreX+reserved*.56);
                    bendX=centreX+3.2*ringRadius;
                    bendY=centreY;
                    break;
                default:
                    targetX=centreX;
                    targetY=padding+sw;
                    bendX=centreX;
                    bendY=centreY-2.5*ringRadius;
                    break;
            }
            arm.EndOffsetX=targetX-centreX;
            arm.EndOffsetY=targetY-centreY;
            arm.Bends.Add(new JunctionPoint { X=bendX-centreX,Y=bendY-centreY });
            destination.LinkedMapArmId=arm.Id;
            map.Junction.Arms.Add(arm);
        }
        var incoming=new JunctionArm {
            AngleDegrees=180, LengthMm=3*x, ShowArrow=false,
            EndOffsetX=0, EndOffsetY=doc.HeightMm-padding-sw-centreY
        };
        map.Junction.Arms.Add(incoming);
        doc.Elements.Insert(1,map);
        return doc;
    }
}
