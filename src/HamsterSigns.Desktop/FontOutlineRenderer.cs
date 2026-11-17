using System.Globalization;
using HamsterSigns.Core;
using SkiaSharp;

namespace HamsterSigns.Desktop;

/// <summary>
/// Uses font files chosen locally by the operator. Glyph contours remain
/// unbundled and are converted into SVG path geometry in drawing millimetres.
/// The same geometry is used by the desktop canvas and exported SVG.
/// </summary>
public sealed class FontOutlineRenderer
{
    private readonly Dictionary<SignFontRole,string> fontFiles = new();

    public void SetFont(SignFontRole role,string filePath)
    {
        if(role==SignFontRole.Automatic) throw new ArgumentException("Select a concrete font role.",nameof(role));
        if(!File.Exists(filePath)) throw new FileNotFoundException("Font file was not found.",filePath);
        var ext=Path.GetExtension(filePath).ToLowerInvariant();
        if(ext is not (".ttf" or ".otf"))
            throw new ArgumentException("Only locally stored TTF and OTF fonts are supported.",nameof(filePath));
        using var face=SKTypeface.FromFile(filePath);
        if(face is null) throw new InvalidDataException("The selected file is not a loadable font.");
        fontFiles[role]=filePath;
    }

    public string? FontPath(SignFontRole role) =>
        fontFiles.TryGetValue(role,out var path) ? path : null;

    private SignFontRole EffectiveRole(SignElement element,SignColour background)
    {
        if(element.FontRole!=SignFontRole.Automatic) return element.FontRole;
        return background is SignColour.NonPrimaryWhite or SignColour.TemporaryYellow
            ? SignFontRole.TransportHeavy : SignFontRole.TransportMedium;
    }

    public bool CanOutline(SignElement element,SignColour background) =>
        fontFiles.ContainsKey(EffectiveRole(element,background));

    public string? BuildPath(SignElement element,SignColour background,
        double scale=1,double translateX=0,double translateY=0)
    {
        if(element.Kind!=ElementKind.Text) return null;
        var role=EffectiveRole(element,background);
        if(!fontFiles.TryGetValue(role,out var filePath)) return null;
        if(!File.Exists(filePath)) throw new FileNotFoundException("Assigned font has been moved or deleted.",filePath);
        using var typeface=SKTypeface.FromFile(filePath)
            ?? throw new InvalidDataException("Unable to read the selected font.");
        using var font=new SKFont(typeface,100);
        var xGlyph=font.GetGlyphs("x");
        if(xGlyph.Length==0 || xGlyph[0]==0)
            throw new InvalidDataException("Font has no lowercase 'x' for measuring x-height.");
        using var xPath=font.GetGlyphPath(xGlyph[0])
            ?? throw new InvalidDataException("Font contains no drawable lowercase 'x'.");
        if(xPath.Bounds.Height<=0)
            throw new InvalidDataException("Font has an invalid x-height.");

        var xHeight=element.FontSizeMm*scale;
        font.Size=(float)(100*xHeight/xPath.Bounds.Height);
        var baseline=(float)((element.Y+element.FontSizeMm)*scale+translateY);
        var cursor=(float)(element.X*scale+translateX);
        using var total=new SKPath();
        for(var i=0;i<element.Text.Length;i++)
        {
            var c=element.Text[i];
            if(char.IsSurrogate(c))
                throw new InvalidDataException("Complex text shaping is not supported for supplementary Unicode characters.");
            var ids=font.GetGlyphs(c.ToString());
            if(ids.Length!=1 || ids[0]==0)
                throw new InvalidDataException($"Selected font does not contain glyph U+{(int)c:X4}.");
            using var outline=font.GetGlyphPath(ids[0]);
            if(outline is not null)
                total.AddPath(outline,cursor,baseline);

            // Transport Medium uses exact tile advances where available.
            // Other fonts use their own advances until their working-drawing
            // tile metrics have been validated.
            float advance;
            if(role==SignFontRole.TransportMedium)
            {
                var current=TransportLetterMetrics.Measure(c.ToString(),xHeight).WidthMm;
                if(i+1<element.Text.Length)
                {
                    var next=element.Text[i+1].ToString();
                    var pair=TransportLetterMetrics.Measure(c+next,xHeight).WidthMm;
                    current=pair-TransportLetterMetrics.Measure(next,xHeight).WidthMm;
                }
                advance=(float)current;
            }
            else
                advance=font.GetGlyphWidths(ids)[0];
            cursor+=advance;
        }
        return total.ToSvgPathData();
    }

    public string Audit(SignDocument document)
    {
        var missing=document.Elements.Where(e=>e.Kind==ElementKind.Text)
            .Select(e=>EffectiveRole(e,document.Background))
            .Distinct()
            .Where(role=>!fontFiles.ContainsKey(role))
            .Select(role=>role.ToString()).ToArray();
        return missing.Length==0
            ? "All text font roles have locally loaded font outlines. This is not a TSRGD compliance certificate."
            : "Font files not loaded for: "+string.Join(", ",missing)+
              ". Those labels use system-font fallback until suitable fonts are loaded.";
    }
}
