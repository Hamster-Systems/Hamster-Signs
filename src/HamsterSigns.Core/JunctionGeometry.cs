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
    public bool Smooth { get; set; }
    public bool ShowArrow { get; set; }
    // Bend positions are offsets from the junction centre, in millimetres.
    public List<JunctionPoint> Bends { get; set; } = [];
}
public sealed class JunctionPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}
public sealed class RingVertex
{
    public double AngleDegrees { get; set; }
    public double RadiusMm { get; set; }
}

public sealed class JunctionDiagram
{
    public JunctionType Type { get; set; } = JunctionType.Roundabout;
    public double RadiusMm { get; set; } = 260;
    public List<JunctionArm> Arms { get; set; } = [];
    // Empty list preserves a circular roundabout for backwards-compatible files.
    public List<RingVertex> RingVertices { get; set; } = [];
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

    public static SignElement IrregularRoundabout(double x, double y, double radius=260)
    {
        var sign=Roundabout(x,y,radius);
        sign.Junction!.RingVertices=[
            new() {AngleDegrees=0,RadiusMm=radius*0.85},
            new() {AngleDegrees=45,RadiusMm=radius*1.2},
            new() {AngleDegrees=90,RadiusMm=radius*1.15},
            new() {AngleDegrees=135,RadiusMm=radius*0.95},
            new() {AngleDegrees=180,RadiusMm=radius*1.25},
            new() {AngleDegrees=225,RadiusMm=radius*1.1},
            new() {AngleDegrees=270,RadiusMm=radius*0.9},
            new() {AngleDegrees=315,RadiusMm=radius*1.05}
        ];
        return sign;
    }

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

    public static double RadiusAtAngle(JunctionDiagram diagram,double angle)
    {
        if(diagram.Type!=JunctionType.Roundabout) return 0;
        if(diagram.RingVertices.Count==0) return diagram.RadiusMm;
        var vertices=diagram.RingVertices.OrderBy(v=>v.AngleDegrees).ToArray();
        var a=((angle%360)+360)%360;
        for(var i=0;i<vertices.Length;i++)
        {
            var low=vertices[i];
            var high=vertices[(i+1)%vertices.Length];
            var start=low.AngleDegrees;
            var end=high.AngleDegrees+(i==vertices.Length-1 ? 360 : 0);
            var t=(a<start ? a+360 : a)-start;
            if(t>=0 && t<=end-start)
                return low.RadiusMm+(high.RadiusMm-low.RadiusMm)*t/(end-start);
        }
        return diagram.RadiusMm;
    }

    public static (double X, double Y) ArmStart(SignElement element,JunctionArm arm)
    {
        var angle=arm.AngleDegrees*Math.PI/180;
        var r=element.Junction is null ? 0 : RadiusAtAngle(element.Junction,arm.AngleDegrees);
        return (element.X+Math.Sin(angle)*r,element.Y-Math.Cos(angle)*r);
    }

    public static (double X, double Y) Endpoint(SignElement element, JunctionArm arm)
    {
        var radius=element.Junction is null ? 0 : RadiusAtAngle(element.Junction,arm.AngleDegrees);
        var angle=arm.AngleDegrees*Math.PI/180;
        var d=radius+arm.LengthMm;
        return (element.X + Math.Sin(angle)*d, element.Y - Math.Cos(angle)*d);
    }

    public static void MoveEndpoint(SignElement element, JunctionArm arm, double x, double y)
    {
        var dx=x-element.X;
        var dy=y-element.Y;
        arm.AngleDegrees=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;
        var r=element.Junction is null ? 0 : RadiusAtAngle(element.Junction,arm.AngleDegrees);
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
            if(junction.RingVertices.Count==0)
            {
                var r=junction.RadiusMm*scale;
                b.Append($"M {F(cx+r)} {F(cy)} A {F(r)} {F(r)} 0 1 0 {F(cx-r)} {F(cy)} A {F(r)} {F(r)} 0 1 0 {F(cx+r)} {F(cy)} ");
            }
            else
            {
                // Sample the radial contour. The ring remains a closed, editable vector path.
                for(var i=0;i<=72;i++)
                {
                    var angle=i*5.0;
                    var radius=RadiusAtAngle(junction,angle)*scale;
                    var rad=angle*Math.PI/180;
                    var x=cx+Math.Sin(rad)*radius;
                    var y=cy-Math.Cos(rad)*radius;
                    b.Append($"{(i==0 ? "M" : "L")} {F(x)} {F(y)} ");
                }
                b.Append("Z ");
            }
        }
        foreach(var arm in junction.Arms)
        {
            var (startX,startY)=ArmStart(element,arm);
            var (endX,endY)=Endpoint(element,arm);
            b.Append($"M {F(startX*scale+tx)} {F(startY*scale+ty)} ");
            if(arm.Bends.Count==0)
            {
                b.Append($"L {F(endX*scale+tx)} {F(endY*scale+ty)} ");
            }
            else if(arm.Smooth)
            {
                // Quadratic chain through midpoint joins. The first bend controls the
                // initial sweep; successive bends produce continuous joined segments.
                for(var i=0;i<arm.Bends.Count;i++)
                {
                    var bend=arm.Bends[i];
                    var bx=(element.X+bend.X)*scale+tx;
                    var by=(element.Y+bend.Y)*scale+ty;
                    if(i==arm.Bends.Count-1)
                    {
                        b.Append($"Q {F(bx)} {F(by)} {F(endX*scale+tx)} {F(endY*scale+ty)} ");
                    }
                    else
                    {
                        var next=arm.Bends[i+1];
                        var midX=(element.X+(bend.X+next.X)/2)*scale+tx;
                        var midY=(element.Y+(bend.Y+next.Y)/2)*scale+ty;
                        b.Append($"Q {F(bx)} {F(by)} {F(midX)} {F(midY)} ");
                    }
                }
            }
            else
            {
                foreach(var bend in arm.Bends)
                    b.Append($"L {F((element.X+bend.X)*scale+tx)} {F((element.Y+bend.Y)*scale+ty)} ");
                b.Append($"L {F(endX*scale+tx)} {F(endY*scale+ty)} ");
            }
        }
        return b.ToString().TrimEnd();
    }

    public static string ArrowPathData(SignElement element,double scale=1,double tx=0,double ty=0)
    {
        var b=new StringBuilder();
        if(element.Junction is null) return "";
        foreach(var arm in element.Junction.Arms.Where(a=>a.ShowArrow))
        {
            var (ex,ey)=Endpoint(element,arm);
            double fromX,fromY;
            if(arm.Bends.Count>0)
            {
                fromX=element.X+arm.Bends[^1].X;
                fromY=element.Y+arm.Bends[^1].Y;
            }
            else (fromX,fromY)=ArmStart(element,arm);
            var vx=ex-fromX; var vy=ey-fromY;
            var len=Math.Sqrt(vx*vx+vy*vy);
            if(len<0.00001) continue;
            var ux=vx/len;var uy=vy/len;
            var size=element.StrokeWidthMm*1.75;
            var halfWidth=size*0.42;
            var bx=ex-ux*size;var by=ey-uy*size;
            b.Append($"M {F(ex*scale+tx)} {F(ey*scale+ty)} ");
            b.Append($"L {F((bx-uy*halfWidth)*scale+tx)} {F((by+ux*halfWidth)*scale+ty)} ");
            b.Append($"L {F((bx+uy*halfWidth)*scale+tx)} {F((by-ux*halfWidth)*scale+ty)} Z ");
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
        if(j.RingVertices is null) { yield return "Ring vertices cannot be null"; yield break; }
        if(j.RingVertices.Count>0)
        {
            if(j.Type!=JunctionType.Roundabout || j.RingVertices.Count is < 3 or > 64)
                yield return "Irregular ring requires a roundabout with 3 to 64 vertices";
            var angles=new HashSet<double>();
            foreach(var vertex in j.RingVertices)
            {
                if(!double.IsFinite(vertex.AngleDegrees) || vertex.AngleDegrees<0 || vertex.AngleDegrees>=360 ||
                   !double.IsFinite(vertex.RadiusMm) || vertex.RadiusMm<=0)
                    yield return "Ring vertices require finite angles [0,360) and positive radii";
                if(!angles.Add(vertex.AngleDegrees)) yield return "Ring vertex angles must be unique";
            }
        }
        foreach(var arm in j.Arms)
        {
            if(!double.IsFinite(arm.AngleDegrees) || !double.IsFinite(arm.LengthMm) || arm.LengthMm<=0)
                yield return "Arm angle and length must be finite and length positive";
            if(arm.Bends is null || arm.Bends.Count>32)
            {
                yield return "Arm bend list must contain at most 32 points";
                continue;
            }
            foreach(var bend in arm.Bends)
                if(!double.IsFinite(bend.X) || !double.IsFinite(bend.Y))
                    yield return "Arm bends must use finite coordinates";
        }
    }
}
