using System.Globalization;
using System.Text;

namespace HamsterSigns.Core;

public enum JunctionType { Roundabout, Intersection }

public sealed class JunctionArm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Clockwise degrees, zero pointing upward on the drawing.</summary>
    public double AngleDegrees { get; set; }
    public double LengthMm { get; set; } = 400;
}

public sealed class JunctionDiagram
{
    public JunctionType Type { get; set; } = JunctionType.Roundabout;
    public double RadiusMm { get; set; } = 260;
    public List<JunctionArm> Arms { get; set; } = [];
}

/// <summary>
/// Parametric diagram geometry shared by SVG export and the on-screen preview.
/// Coordinates are real drawing millimetres. The output is illustrative, not TSRGD validated.
/// </summary>
public static class JunctionGeometry
{
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    public static SignElement Roundabout(double centreX, double centreY, double radius, double stroke=65) =>
        new()
        {
            Kind=ElementKind.Junction, X=centreX, Y=centreY, StrokeWidthMm=stroke,
            Junction=new JunctionDiagram
            {
                Type=JunctionType.Roundabout, RadiusMm=radius,
                Arms=[
                    new() { AngleDegrees=0, LengthMm=450 },
                    new() { AngleDegrees=90, LengthMm=540 },
                    new() { AngleDegrees=180, LengthMm=500 },
                    new() { AngleDegrees=270, LengthMm=400 }
                ]
            }
        };

    public static SignElement TJunction(double x, double y, double stroke=65) =>
        new()
        {
            Kind=ElementKind.Junction, X=x, Y=y, StrokeWidthMm=stroke,
            Junction=new JunctionDiagram
            {
                Type=JunctionType.Intersection, RadiusMm=0,
                Arms=[
                    new() { AngleDegrees=90, LengthMm=500 },
                    new() { AngleDegrees=180, LengthMm=500 },
                    new() { AngleDegrees=270, LengthMm=500 }
                ]
            }
        };

    public static SignElement Crossroads(double x, double y, double stroke=65)
    {
        var junction=TJunction(x,y,stroke);
        junction.Junction!.Arms.Insert(0,new JunctionArm { AngleDegrees=0,LengthMm=500 });
        return junction;
    }

    public static (double X, double Y) Endpoint(SignElement element, JunctionArm arm)
    {
        var radius=element.Junction?.Type==JunctionType.Roundabout ? element.Junction.RadiusMm : 0;
        var angle=arm.AngleDegrees*Math.PI/180;
        var d=radius+arm.LengthMm;
        return (element.X + Math.Sin(angle)*d, element.Y - Math.Cos(angle)*d);
    }

    public static void MoveEndpoint(SignElement element, JunctionArm arm, double x, double y)
    {
        var dx=x-element.X;
        var dy=y-element.Y;
        var r=element.Junction?.Type==JunctionType.Roundabout ? element.Junction.RadiusMm : 0;
        arm.AngleDegrees=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;
        arm.LengthMm=Math.Max(1,Math.Sqrt(dx*dx+dy*dy)-r);
    }

    public static string PathData(SignElement element, double scale=1, double tx=0, double ty=0)
    {
        var junction=element.Junction ?? throw new ArgumentException("Missing junction data",nameof(element));
        var cx=element.X*scale+tx;
        var cy=element.Y*scale+ty;
        var b=new StringBuilder();
        if(junction.Type==JunctionType.Roundabout)
        {
            var r=junction.RadiusMm*scale;
            b.Append($"M {F(cx+r)} {F(cy)} A {F(r)} {F(r)} 0 1 0 {F(cx-r)} {F(cy)} A {F(r)} {F(r)} 0 1 0 {F(cx+r)} {F(cy)} ");
        }
        foreach(var arm in junction.Arms)
        {
            var a=arm.AngleDegrees*Math.PI/180;
            var inner=junction.Type==JunctionType.Roundabout ? junction.RadiusMm : 0;
            var x0=cx+Math.Sin(a)*inner*scale;
            var y0=cy-Math.Cos(a)*inner*scale;
            var (xe,ye)=Endpoint(element,arm);
            b.Append($"M {F(x0)} {F(y0)} L {F(xe*scale+tx)} {F(ye*scale+ty)} ");
        }
        return b.ToString().TrimEnd();
    }

    public static IEnumerable<string> Validate(SignElement element)
    {
        var j=element.Junction;
        if(j is null) { yield return "Junction data missing"; yield break; }
        if(j.Arms is null || j.Arms.Count is < 1 or > 16) { yield return "Junction must have between 1 and 16 arms"; yield break; }
        if(j.Type==JunctionType.Roundabout && (!double.IsFinite(j.RadiusMm) || j.RadiusMm<=0))
            yield return "Roundabout radius must be positive and finite";
        if(j.Type==JunctionType.Intersection && (!double.IsFinite(j.RadiusMm) || j.RadiusMm<0))
            yield return "Invalid intersection radius";
        if(!double.IsFinite(element.StrokeWidthMm) || element.StrokeWidthMm<=0)
            yield return "Junction stroke width must be positive";
        if(j.Arms.Select(a=>a.Id).Distinct().Count()!=j.Arms.Count)
            yield return "Junction arms must have unique IDs";
        foreach(var arm in j.Arms)
        {
            if(!double.IsFinite(arm.AngleDegrees) || !double.IsFinite(arm.LengthMm) || arm.LengthMm<=0)
                yield return "Arm angle and length must be finite and length positive";
        }
    }
}
