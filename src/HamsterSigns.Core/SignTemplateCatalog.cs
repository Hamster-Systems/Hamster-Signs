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
        if(kind!=SignTemplateKind.StackAdvance)
            return MapSignLayout.Build(spec,kind,title,drawingNumber);

        var doc=DirectionSignLayout.Build(spec,title,drawingNumber);
        doc.Template=kind;
        return doc;
    }
}
