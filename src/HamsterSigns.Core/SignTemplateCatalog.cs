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

        // Reserve a separate vector diagram column and maintain destination,
        // border and patch alignment. All elements remain editable.
        var additional=8*spec.XHeightMm;
        var oldWidth=doc.WidthMm;
        var newWidth=DirectionLayoutRules.RoundUp(oldWidth+additional,spec.XHeightMm,spec.RoundDimensions);
        doc.WidthMm=newWidth;
        var rightShift=newWidth-oldWidth;

        // Generated arrow strokes are removed in favour of a map-type route sketch.
        doc.Elements.RemoveAll(e=>e.Kind==ElementKind.Line);
        var border=doc.Elements[0];
        border.Width+=rightShift;
        foreach(var element in doc.Elements.Skip(1))
            element.X+=rightShift;

        var sw=TransportLetterMetrics.StrokeWidth(spec.XHeightMm);
        var radius=2.1*sw;
        var diagramX=4*spec.XHeightMm;
        var diagramY=doc.HeightMm/2;
        var map=JunctionGeometry.Roundabout(diagramX,diagramY,radius,Math.Max(6,0.65*sw));
        map.Stroke=SignPalette.Foreground(spec.Background);
        map.Junction!.Arms.Clear();
        foreach(var direction in spec.Destinations.Select(d=>d.Arrow).Distinct())
            map.Junction.Arms.Add(new JunctionArm {
                AngleDegrees=direction switch {
                    DirectionArrow.Left=>270, DirectionArrow.Right=>90, _=>0
                },
                LengthMm=2.3*spec.XHeightMm
            });
        // Incoming arm is always shown on the lower side of the diagram.
        map.Junction.Arms.Add(new JunctionArm {AngleDegrees=180,LengthMm=2.5*spec.XHeightMm});
        doc.Elements.Insert(1,map);
        return doc;
    }
}
