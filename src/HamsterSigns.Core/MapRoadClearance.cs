namespace HamsterSigns.Core;

/// <summary>
/// Approximate vector centreline-to-legend clearance checks for map diagrams.
/// Samples quadratic/straight branches; this is not glyph-outline, legal or
/// manufacturer-grade collision analysis.
/// </summary>
public static class MapRoadClearance
{
    public static IReadOnlyList<string> Audit(SignDocument document)
    {
        if(document.Template is not (SignTemplateKind.MapAdvance or SignTemplateKind.MotorwayMap))
            return [];
        var network=document.Elements.FirstOrDefault(e=>e.Kind==ElementKind.Junction);
        if(network?.Junction is null) return [];
        var rows=document.DirectionDesign?.Destinations;
        if(rows is null) return [];
        var warnings=new List<string>();
        // Keep each arm as a polyline so crossings *between* sample points
        // are detected, not just sample points inside a rectangle.
        var roadPolylines=network.Junction.Arms
            .Select(a=>SampleArm(network,a).ToArray()).ToList();
        var clearance=network.StrokeWidthMm/2+
            TransportLetterMetrics.StrokeWidth(rows.Count>0 ? document.DirectionDesign!.XHeightMm : 100);
        foreach(var element in document.Elements)
        {
            if(element.Kind!=ElementKind.Text && !(element.Kind==ElementKind.Panel && element.Fill!="none"))
                continue;
            double w=element.Kind==ElementKind.Text
                ? TransportLetterMetrics.Measure(element.Text,element.FontSizeMm).WidthMm : element.Width;
            double h=element.Kind==ElementKind.Text
                ? TransportLetterMetrics.TileHeight(element.FontSizeMm) : element.Height;
            var left=element.X-clearance;
            var top=element.Y-clearance;
            var right=element.X+w+clearance;
            var bottom=element.Y+h+clearance;
            if(roadPolylines.Any(line=>
                line.Length>0 && (
                    (line.Length==1 && InRect(line[0],left,top,right,bottom)) ||
                    Enumerable.Range(1,line.Length-1).Any(i=>
                        SegmentTouchesRect(line[i-1],line[i],left,top,right,bottom)))))
                warnings.Add($"Road-to-legend clearance: {element.Kind} {element.Id} may overlap a road centreline.");
        }
        return warnings;
    }

    private static bool InRect((double X,double Y) point,
        double left,double top,double right,double bottom) =>
        point.X>=left && point.X<=right && point.Y>=top && point.Y<=bottom;

    /// <summary>Liang-Barsky exact segment versus expanded rectangle test.</summary>
    public static bool SegmentTouchesRect((double X,double Y) start,(double X,double Y) end,
        double left,double top,double right,double bottom)
    {
        if(!double.IsFinite(start.X) || !double.IsFinite(start.Y) ||
           !double.IsFinite(end.X) || !double.IsFinite(end.Y) ||
           !double.IsFinite(left) || !double.IsFinite(top) ||
           !double.IsFinite(right) || !double.IsFinite(bottom) ||
           left>right || top>bottom) return false;
        var dx=end.X-start.X;
        var dy=end.Y-start.Y;
        var t0=0d;
        var t1=1d;
        static bool Clip(double p,double q,ref double near,ref double far)
        {
            if(Math.Abs(p)<1e-12) return q>=0;
            var ratio=q/p;
            if(p<0) near=Math.Max(near,ratio);
            else far=Math.Min(far,ratio);
            return near<=far;
        }
        return Clip(-dx,start.X-left,ref t0,ref t1) &&
            Clip(dx,right-start.X,ref t0,ref t1) &&
            Clip(-dy,start.Y-top,ref t0,ref t1) &&
            Clip(dy,bottom-start.Y,ref t0,ref t1);
    }

    public static IEnumerable<(double X,double Y)> SampleArm(SignElement junction,JunctionArm arm)
    {
        var start=JunctionGeometry.ArmStart(junction,arm);
        var end=JunctionGeometry.Endpoint(junction,arm);
        var from=start;
        if(arm.Bends.Count==0)
        {
            foreach(var p in SampleLine(start,end)) yield return p;
            yield break;
        }
        if(!arm.Smooth)
        {
            foreach(var bend in arm.Bends)
            {
                var next=(junction.X+bend.X,junction.Y+bend.Y);
                foreach(var p in SampleLine(from,next)) yield return p;
                from=next;
            }
            foreach(var p in SampleLine(from,end)) yield return p;
            yield break;
        }
        for(var index=0;index<arm.Bends.Count;index++)
        {
            var bend=arm.Bends[index];
            var control=(junction.X+bend.X,junction.Y+bend.Y);
            var target=index==arm.Bends.Count-1 ? end :
                (junction.X+(bend.X+arm.Bends[index+1].X)/2,
                 junction.Y+(bend.Y+arm.Bends[index+1].Y)/2);
            for(var i=0;i<=32;i++)
            {
                var t=i/32.0;
                var q=1-t;
                yield return (
                    q*q*from.X+2*q*t*control.Item1+t*t*target.Item1,
                    q*q*from.Y+2*q*t*control.Item2+t*t*target.Item2
                );
            }
            from=target;
        }
    }

    private static IEnumerable<(double X,double Y)> SampleLine(
        (double X,double Y) a,(double X,double Y) b)
    {
        for(var i=0;i<=32;i++)
        {
            var t=i/32.0;
            yield return (a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
        }
    }
}
