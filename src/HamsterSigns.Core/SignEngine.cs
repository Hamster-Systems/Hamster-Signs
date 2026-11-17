using System.Globalization;
using System.Security;
using System.Text;

namespace HamsterSigns.Core;

public static class SignPalette
{
    public static string Background(SignColour colour) => colour switch
    {
        SignColour.PrimaryGreen => "#006b45",
        SignColour.MotorwayBlue => "#174c9a",
        SignColour.NonPrimaryWhite => "#ffffff",
        SignColour.TouristBrown => "#754b31",
        SignColour.TemporaryYellow => "#f7da26",
        _ => "#006b45"
    };
    public static string Foreground(SignColour colour) =>
        colour is SignColour.NonPrimaryWhite or SignColour.TemporaryYellow ? "#111111" : "#ffffff";
}
public record SignDiagnostic(string Code, string Message, bool IsError);
public static class SignValidator
{
    public static IReadOnlyList<SignDiagnostic> Check(SignDocument doc)
    {
        var result = new List<SignDiagnostic>();
        if (!double.IsFinite(doc.WidthMm) || !double.IsFinite(doc.HeightMm) || doc.WidthMm <= 0 || doc.HeightMm <= 0)
            result.Add(new("SIZE", "Sign dimensions must be finite positive millimetre values.", true));
        foreach (var e in doc.Elements)
        {
            double[] values = [e.X, e.Y, e.X2, e.Y2, e.Control1X, e.Control1Y, e.Control2X, e.Control2Y, e.Width, e.Height, e.FontSizeMm, e.StrokeWidthMm, e.CornerRadiusMm];
            if (values.Any(v => !double.IsFinite(v)))
                result.Add(new("COORD", $"Element {e.Id} contains a non-finite value.", true));
            if (e.Kind == ElementKind.Text && (e.FontSizeMm <= 0 || string.IsNullOrWhiteSpace(e.Text)))
                result.Add(new("TEXT", $"Text element {e.Id} has invalid content.", true));
            if (e.Kind==ElementKind.Panel && (e.CornerRadiusMm<0 || e.CornerRadiusMm>Math.Min(e.Width,e.Height)/2))
                result.Add(new("RADIUS", $"Panel {e.Id} has an invalid corner radius.", true));
            if (e.Kind is ElementKind.Panel or ElementKind.Ellipse && (e.Width <= 0 || e.Height <= 0))
                result.Add(new("GEOMETRY", $"Shape {e.Id} has invalid dimensions.", true));
            if (e.Kind==ElementKind.Text && e.TextColor is not null && !System.Text.RegularExpressions.Regex.IsMatch(e.TextColor, "^#[0-9a-fA-F]{6}$"))
                result.Add(new("COLOR", $"Element {e.Id} has an invalid text colour.", true));
            if(e.Kind==ElementKind.Symbol && !SignSymbolLibrary.Valid(e))
                result.Add(new("SYMBOL",$"Element {e.Id} has invalid symbol properties.",true));
            if (e.Kind==ElementKind.Junction)
                foreach(var error in JunctionGeometry.Validate(e))
                    result.Add(new("JUNCTION", $"Element {e.Id}: {error}", true));
        }
        if(doc.DirectionDesign is not null)
            foreach(var error in DirectionSignLayout.Validate(doc.DirectionDesign))
                result.Add(new("DIRECTION",error,true));
        result.Add(new("STANDARDS", "TSRGD compliance checks are not implemented. Professional verification is required.", false));
        return result;
    }
}
public static class SvgExporter
{
    private static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string input) => SecurityElement.Escape(input) ?? "";
    /// <summary>Optional glyph outline provider returns an SVG path in document mm coordinates.</summary>
    public static string Export(SignDocument doc, Func<SignElement, string?>? outlineProvider=null)
    {
        if (SignValidator.Check(doc).Any(x => x.IsError))
            throw new InvalidDataException("Invalid sign geometry.");
        var b = new StringBuilder();
        b.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(doc.WidthMm)}mm\" height=\"{F(doc.HeightMm)}mm\" viewBox=\"0 0 {F(doc.WidthMm)} {F(doc.HeightMm)}\">");
        b.AppendLine($"<title>{E(doc.Title)}</title>");
        b.AppendLine($"<rect width=\"{F(doc.WidthMm)}\" height=\"{F(doc.HeightMm)}\" fill=\"{SignPalette.Background(doc.Background)}\"/>");
        foreach (var e in doc.Elements)
        {
            switch (e.Kind)
            {
                case ElementKind.Text:
                    var outline=outlineProvider?.Invoke(e);
                    if(!string.IsNullOrWhiteSpace(outline))
                        b.AppendLine($"<path d=\"{E(outline)}\" fill=\"{E(e.TextColor ?? SignPalette.Foreground(doc.Background))}\" fill-rule=\"nonzero\"/>");
                    else
                        b.AppendLine($"<text x=\"{F(e.X)}\" y=\"{F(e.Y+e.FontSizeMm)}\" fill=\"{E(e.TextColor ?? SignPalette.Foreground(doc.Background))}\" font-family=\"sans-serif\" font-size=\"{F(e.FontSizeMm)}\">{E(e.Text)}</text>");
                    break;
                case ElementKind.Panel:
                    b.AppendLine($"<rect x=\"{F(e.X)}\" y=\"{F(e.Y)}\" width=\"{F(e.Width)}\" height=\"{F(e.Height)}\" rx=\"{F(e.CornerRadiusMm)}\" ry=\"{F(e.CornerRadiusMm)}\" fill=\"{E(e.Fill)}\" stroke=\"{E(e.Stroke)}\" stroke-width=\"{F(e.StrokeWidthMm)}\"/>");
                    break;
                case ElementKind.Ellipse:
                    b.AppendLine($"<ellipse cx=\"{F(e.X+e.Width/2)}\" cy=\"{F(e.Y+e.Height/2)}\" rx=\"{F(e.Width/2)}\" ry=\"{F(e.Height/2)}\" fill=\"{E(e.Fill)}\" stroke=\"{E(e.Stroke)}\" stroke-width=\"{F(e.StrokeWidthMm)}\"/>");
                    break;
                case ElementKind.Line:
                    b.AppendLine($"<line x1=\"{F(e.X)}\" y1=\"{F(e.Y)}\" x2=\"{F(e.X2)}\" y2=\"{F(e.Y2)}\" stroke=\"{E(e.Stroke)}\" stroke-width=\"{F(e.StrokeWidthMm)}\"/>");
                    break;
                case ElementKind.CubicBezier:
                    b.AppendLine($"<path d=\"M {F(e.X)} {F(e.Y)} C {F(e.Control1X)} {F(e.Control1Y)}, {F(e.Control2X)} {F(e.Control2Y)}, {F(e.X2)} {F(e.Y2)}\" fill=\"none\" stroke=\"{E(e.Stroke)}\" stroke-width=\"{F(e.StrokeWidthMm)}\"/>");
                    break;
                case ElementKind.Symbol:
                    b.AppendLine($"<path d=\"{SignSymbolLibrary.PathData(e)}\" transform=\"{SignSymbolLibrary.Transform(e)}\" fill=\"none\" stroke=\"{E(e.Stroke)}\" stroke-width=\"4\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
                    break;
                case ElementKind.Junction:
                    b.AppendLine($"<path d=\"{JunctionGeometry.PathData(e)}\" fill=\"none\" stroke=\"{E(e.Stroke)}\" stroke-width=\"{F(e.StrokeWidthMm)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
                    var arrows=JunctionGeometry.ArrowPathData(e);
                    if(arrows.Length>0)
                        b.AppendLine($"<path d=\"{arrows}\" fill=\"{E(e.Stroke)}\" stroke=\"none\"/>");
                    break;
            }
        }
        return b.AppendLine("</svg>").ToString();
    }
}
