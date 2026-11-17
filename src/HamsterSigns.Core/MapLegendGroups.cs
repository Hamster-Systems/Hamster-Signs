namespace HamsterSigns.Core;

/// <summary>
/// Resolve and move an entire map legend (place, route and coloured patch) as
/// one logical group, while retaining independent underlying CAD objects.
/// </summary>
public static class MapLegendGroups
{
    public static IReadOnlyList<SignElement> Resolve(SignDocument document,Guid elementId)
    {
        if(document.Template is not (SignTemplateKind.MapAdvance or SignTemplateKind.MotorwayMap) ||
            document.DirectionDesign is null) return [];
        foreach(var row in document.DirectionDesign.Destinations)
        {
            var ids=new[] {row.MapPlaceElementId,row.MapRouteElementId,row.MapPatchElementId}
                .Where(id=>id.HasValue).Select(id=>id!.Value).ToHashSet();
            if(!ids.Contains(elementId)) continue;
            return document.Elements.Where(e=>ids.Contains(e.Id)).ToArray();
        }
        return [];
    }

    public static IReadOnlyDictionary<Guid,(double X,double Y)> Origins(
        SignDocument document,Guid selectedElementId) =>
        Resolve(document,selectedElementId).ToDictionary(e=>e.Id,e=>(e.X,e.Y));

    public static bool MoveFromOrigins(SignDocument document,
        IReadOnlyDictionary<Guid,(double X,double Y)> origins,
        double dx,double dy,bool enforceClearance=true)
    {
        if(origins.Count==0 || !double.IsFinite(dx) || !double.IsFinite(dy))
            return false;
        var elements=document.Elements.Where(e=>origins.ContainsKey(e.Id)).ToArray();
        if(elements.Length!=origins.Count) return false;
        var previous=elements.ToDictionary(e=>e.Id,e=>(e.X,e.Y));
        foreach(var element in elements)
        {
            var start=origins[element.Id];
            element.X=start.X+dx;
            element.Y=start.Y+dy;
        }
        var okay=elements.All(e=>FitsOnFace(e,document));
        if(okay && enforceClearance)
        {
            okay=MapRoadClearance.Audit(document).Count==0 &&
                !MapSignLayout.AuditLegendOverlaps(document).Any();
        }
        if(okay) return true;
        foreach(var element in elements)
        {
            var start=previous[element.Id];
            element.X=start.X;
            element.Y=start.Y;
        }
        return false;
    }

    private static bool FitsOnFace(SignElement element,SignDocument document)
    {
        double width=element.Kind==ElementKind.Text
            ? TransportLetterMetrics.Measure(element.Text,element.FontSizeMm).WidthMm
            : element.Width;
        double height=element.Kind==ElementKind.Text
            ? TransportLetterMetrics.TileHeight(element.FontSizeMm)
            : element.Height;
        var safety=element.FontSizeMm>0 ? Math.Min(10,element.FontSizeMm*.05) : 5;
        return element.X>=safety && element.Y>=safety &&
            element.X+width<=document.WidthMm-safety &&
            element.Y+height<=document.HeightMm-safety;
    }
}
