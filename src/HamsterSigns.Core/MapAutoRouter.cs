namespace HamsterSigns.Core;

/// <summary>
/// Deterministic, bounded prototype routing. Moves a destination group and
/// its associated road-arm endpoint together, then searches control-point
/// positions for adequate clearance from rectangular legend tiles.
/// Never reshapes unrelated arms and never claims TSRGD approval.
/// </summary>
public static class MapAutoRouter
{
    public sealed record Result(bool Success, string Message, int CandidateCount);

    public static Result MoveLinkedLegend(SignDocument document, Guid selectedElementId,
        IReadOnlyDictionary<Guid,(double X,double Y)> origins,
        double dx, double dy, bool protectClearance=true)
    {
        if(document.Template is not (SignTemplateKind.MapAdvance or SignTemplateKind.MotorwayMap))
            return new(false,"Auto-routing requires a map-type template.",0);
        if(!double.IsFinite(dx) || !double.IsFinite(dy) || origins.Count==0)
            return new(false,"Invalid legend movement.",0);

        var row=document.DirectionDesign?.Destinations.FirstOrDefault(d=>
            d.MapPlaceElementId==selectedElementId ||
            d.MapRouteElementId==selectedElementId ||
            d.MapPatchElementId==selectedElementId);
        var junction=document.Elements.FirstOrDefault(e=>e.Kind==ElementKind.Junction);
        var arm=junction?.Junction?.Arms.FirstOrDefault(a=>a.Id==row?.LinkedMapArmId);
        if(row is null || junction is null || arm is null)
            return new(false,"Selected legend has no linked road arm.",0);

        var group=MapLegendGroups.Resolve(document,selectedElementId);
        if(group.Count==0 || group.Count!=origins.Count ||
           group.Any(e=>!origins.ContainsKey(e.Id)))
            return new(false,"Destination group has changed since drag started.",0);

        // Snapshot the live state, not the pointer-down origins. During a drag
        // the active proposal may already have moved the same objects.
        var previousGroup=group.ToDictionary(e=>e.Id,e=>(e.X,e.Y));
        var previousEnd=JunctionGeometry.Endpoint(junction,arm);
        var previousBends=arm.Bends.Select(p=>new JunctionPoint { X=p.X,Y=p.Y }).ToList();
        var previousSmooth=arm.Smooth;
        var previousAngle=arm.AngleDegrees;
        var previousLength=arm.LengthMm;
        var previousOffsetX=arm.EndOffsetX;
        var previousOffsetY=arm.EndOffsetY;

        void Rollback()
        {
            foreach(var e in group)
            {
                var previous=previousGroup[e.Id];
                e.X=previous.X;
                e.Y=previous.Y;
            }
            arm.EndOffsetX=previousOffsetX;
            arm.EndOffsetY=previousOffsetY;
            arm.AngleDegrees=previousAngle;
            arm.LengthMm=previousLength;
            arm.Smooth=previousSmooth;
            arm.Bends=previousBends;
        }

        if(!MapLegendGroups.MoveFromOrigins(document,origins,dx,dy,false))
            return new(false,"Destination would leave the sign face.",0);
        if(MapSignLayout.AuditLegendOverlaps(document).Count>0 && protectClearance)
        {
            Rollback();
            return new(false,"Destination would overlap another legend.",0);
        }

        // Endpoint tracks the label group's actual offset from drag origin.
        // This works for multiple pointer moves within one edit transaction.
        var anchor=group.First(e=>e.Id==selectedElementId);
        var effectiveDx=anchor.X-origins[selectedElementId].X;
        var effectiveDy=anchor.Y-origins[selectedElementId].Y;
        var oldStart=previousGroup[selectedElementId];
        var incrementalDx=anchor.X-oldStart.X;
        var incrementalDy=anchor.Y-oldStart.Y;
        JunctionGeometry.MoveEndpoint(junction,arm,
            previousEnd.X+incrementalDx,previousEnd.Y+incrementalDy);
        arm.Smooth=true;
        var end=JunctionGeometry.Endpoint(junction,arm);
        var start=JunctionGeometry.ArmStart(junction,arm);
        var centreX=(start.X+end.X)/2;
        var centreY=(start.Y+end.Y)/2;
        var nx=-(end.Y-start.Y);
        var ny=end.X-start.X;
        var length=Math.Sqrt(nx*nx+ny*ny);
        if(length<0.000001) { nx=0;ny=1;length=1; }
        nx/=length;ny/=length;

        // Try the previous curve translated toward the legend, then several
        // progressively wider quadratic detours in both perpendicular directions.
        var bend=previousBends.Count>0
            ? previousBends[0] : new JunctionPoint {
                X=centreX-junction.X,Y=centreY-junction.Y
            };
        var xHeight=document.DirectionDesign?.XHeightMm ?? 100;
        var attempts=new List<(double X,double Y)> {
            (junction.X+bend.X+incrementalDx*.5,
             junction.Y+bend.Y+incrementalDy*.5),
            (centreX,centreY)
        };
        foreach(var multiplier in new[] { 0.5,1.0,1.5,2.0,3.0,4.0 })
        {
            var d=xHeight*multiplier;
            attempts.Add((centreX+nx*d,centreY+ny*d));
            attempts.Add((centreX-nx*d,centreY-ny*d));
        }
        int tried=0;
        int bestScore=int.MaxValue;
        (double X,double Y) best=attempts[0];
        foreach(var candidate in attempts)
        {
            tried++;
            arm.Bends=[new JunctionPoint {
                X=candidate.X-junction.X,Y=candidate.Y-junction.Y
            }];
            var score=MapRoadClearance.Audit(document).Count;
            if(score<bestScore)
            {
                bestScore=score;
                best=candidate;
            }
            if(score==0) return new(true,"Destination moved and linked road adjusted.",tried);
        }

        if(!protectClearance)
        {
            arm.Bends=[new JunctionPoint {
                X=best.X-junction.X,Y=best.Y-junction.Y
            }];
            return new(true,$"Best-effort route has {bestScore} clearance warning(s).",tried);
        }
        Rollback();
        return new(false,
            $"No safe road bend found in {tried} attempts; move restored.",tried);
    }
}
