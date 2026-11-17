namespace HamsterSigns.Core;

/// <summary>
/// Selected geometric rules from Traffic Signs Manual Chapter 7 (2018).
/// These are not a complete Schedule 12 sign-design profile.
/// </summary>
public static class DirectionLayoutRules
{
    public const string Reference = "Traffic Signs Manual Chapter 7 (2018), sections 2.5, 2.6, 3.3 and 3.5";
    public static double StrokeWidth(double xHeightMm) => TransportLetterMetrics.StrokeWidth(xHeightMm);
    // Section 2.5.4: standard main-sign border for most prescribed rectangular signs.
    public static double MainBorderWidth(double xHeightMm) => 1.5*StrokeWidth(xHeightMm);
    // Figure 2-4 and section 3.3.1: separation from the inside edge of border to tile edge.
    public static double OuterTileClearance(double xHeightMm) => 2.5*StrokeWidth(xHeightMm);
    // Section 3.3.4 / figure 3-9: route number patch inset, each side.
    public static double RoutePatchInset(double xHeightMm) => StrokeWidth(xHeightMm);
    public static double PatchCornerRadius(double xHeightMm) => StrokeWidth(xHeightMm);
    // Section 3.3.1: white border on dark patch over dark background.
    public static double DarkPatchBorderWidth(double xHeightMm) => 0.5*StrokeWidth(xHeightMm);
    public static double LegendPatchGap(double xHeightMm) => 2.5*StrokeWidth(xHeightMm);

    // Section 2.6.2: optional rounding to Z = ceil(5% of x-height to next 5mm).
    public static double RoundingIncrement(double xHeightMm)
    {
        StrokeWidth(xHeightMm);
        return 5*Math.Ceiling((0.05*xHeightMm)/5);
    }
    public static double RoundUp(double lengthMm,double xHeightMm,bool enabled=true)
    {
        if(!double.IsFinite(lengthMm) || lengthMm<0)
            throw new ArgumentOutOfRangeException(nameof(lengthMm));
        if(!enabled) return lengthMm;
        var increment=RoundingIncrement(xHeightMm);
        return Math.Ceiling((lengthMm-0.00000001)/increment)*increment;
    }

    public static bool NeedsWhitePatchBorder(RoutePanel style,SignColour background) =>
        style is RoutePanel.Motorway or RoutePanel.Primary or RoutePanel.Tourist &&
        background is SignColour.MotorwayBlue or SignColour.PrimaryGreen or SignColour.TouristBrown;

    public static IReadOnlyList<string> Audit(DirectionSignSpecification spec)
    {
        var result=new List<string>();
        if(DirectionSignLayout.Validate(spec).Count>0)
            return ["Invalid direction sign specification."];
        result.Add("Main border width 1.5 sw: Chapter 7 section 2.5.4 (most, not all, rectangular signs).");
        result.Add("Route patch inset and corner rounding: Chapter 7 sections 3.3.1–3.3.5, Figure 3-9.");
        if(spec.RoundDimensions)
            result.Add("Optional dimensions rounded using Chapter 7 section 2.6.2.");
        result.Add("Arrow shapes, row-to-row clearance and this mixed-column arrangement are provisional; Chapter 7 sign-type templates remain to be implemented.");
        result.Add("Typography, legal appropriateness of panel/patch colours and TSRGD Schedule 12 conformity require professional checking.");
        return result;
    }
}
