using System.Text.Json;
using System.Text.Json.Serialization;

namespace HamsterSigns.Core;

public enum SignColour { PrimaryGreen, MotorwayBlue, NonPrimaryWhite, TouristBrown, TemporaryYellow }
public enum ElementKind { Text, Panel, Line, Ellipse, CubicBezier, Junction }
public enum SignFontRole { Automatic, TransportMedium, TransportHeavy, MotorwayPermanent }

public sealed class SignDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Title { get; set; } = "Untitled sign";
    public string DrawingNumber { get; set; } = "HS-001";
    public double WidthMm { get; set; } = 2665;
    public double HeightMm { get; set; } = 2095;
    public SignColour Background { get; set; } = SignColour.PrimaryGreen;
    public List<SignElement> Elements { get; set; } = [];
    public DirectionSignSpecification? DirectionDesign { get; set; }

    public static SignDocument Example() => new()
    {
        Title = "Example primary-route direction sign",
        Elements =
        [
            new() { Kind=ElementKind.Text, X=800, Y=170, Text="Kendal (S)", FontSizeMm=140 },
            new() { Kind=ElementKind.Text, X=800, Y=365, Text="A6", FontSizeMm=140 },
            new() { Kind=ElementKind.Text, X=1400, Y=1260, Text="Lancaster", FontSizeMm=145 },
            new() { Kind=ElementKind.Text, X=1400, Y=1460, Text="A591", FontSizeMm=130 },
            new() { Kind=ElementKind.Ellipse, X=380, Y=580, Width=600, Height=600, StrokeWidthMm=75 },
            new() { Kind=ElementKind.Line, X=650, Y=1100, X2=650, Y2=1900, StrokeWidthMm=90 },
            new() { Kind=ElementKind.Line, X=950, Y=920, X2=1900, Y2=920, StrokeWidthMm=85 }
        ]
    };
}
public sealed class SignElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ElementKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Control1X { get; set; }
    public double Control1Y { get; set; }
    public double Control2X { get; set; }
    public double Control2Y { get; set; }
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 150;
    public string Text { get; set; } = "Text";
    public double FontSizeMm { get; set; } = 100;
    public string Fill { get; set; } = "none";
    public string? TextColor { get; set; }
    public SignFontRole FontRole { get; set; } = SignFontRole.Automatic;
    public string Stroke { get; set; } = "#ffffff";
    public double StrokeWidthMm { get; set; } = 5;
    public double CornerRadiusMm { get; set; }
    public JunctionDiagram? Junction { get; set; }
}
public static class SignFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented=true, Converters={ new JsonStringEnumConverter() } };
    public static string Serialize(SignDocument doc) => JsonSerializer.Serialize(doc, Options);
    public static SignDocument Deserialize(string json) => JsonSerializer.Deserialize<SignDocument>(json, Options) ?? throw new InvalidDataException("Empty sign document");
}
