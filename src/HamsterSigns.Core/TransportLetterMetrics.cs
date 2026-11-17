namespace HamsterSigns.Core;

/// <summary>
/// UK sign lettering tile calculations in stroke-width units.
/// Transport Medium tile widths from DfT Schedule 17 TM1, TM2 and TM3.
/// Other glyphs are intentionally marked approximate until the relevant
/// working drawing is transcribed and checked. This is NOT a font renderer.
/// </summary>
public enum TransportAlphabet { Medium, Heavy, Motorway, MotorwayBlack }

public readonly record struct LetterMetrics(
    double WidthMm,
    double TileHeightMm,
    double StrokeWidthMm,
    bool FullySpecified,
    IReadOnlyList<char> UnverifiedCharacters);

public static class TransportLetterMetrics
{
    // Official TM1 upper case tile widths, stroke-width units (sw).
    private static readonly IReadOnlyDictionary<char,double> MediumCapitals =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
        .Zip(new double[] {
            5.44,5.88,5.92,6.16,5.28,4.76,6.20,6.40,2.92,3.72,5.52,4.28,
            7.36,6.72,6.24,5.20,6.32,5.64,5.48,4.36,6.16,5.20,7.32,5.12,4.92,4.76
        }).ToDictionary(pair=>pair.First,pair=>pair.Second);

    // DfT Schedule 17 TM2: lowercase characters and punctuation (sw).
    private static readonly IReadOnlyDictionary<char,double> MediumLowercase =
        "abcdefghijklmnopqrstuvwxyz"
        .Zip(new double[] {
            4.44,4.68,4.12,4.76,4.36,3.00,4.56,4.48,2.16,2.32,4.32,2.48,
            6.56,4.48,4.72,4.72,4.72,3.20,3.88,3.24,4.60,3.92,5.88,4.16,3.92,3.88
        }).ToDictionary(pair=>pair.First,pair=>pair.Second);

    private static readonly IReadOnlyDictionary<char,double> MediumPunctuation =
        new Dictionary<char,double> {
            ['(']=4.20, [')']=4.20, ['&']=5.04, ['?']=5.52,
            ['.']=2.12, [':']=2.12, [';']=2.12, [',']=2.60,
            ['!']=1.56, ['-']=2.64, ['\\'']=3.40, ['"']=3.68,
            ['/']=3.12, ['*']=3.12
        };

    // DfT Schedule 17 TM3: numeral tiles (sw). Special fraction
    // characters require their own named tile handling in a future pass.
    private static readonly IReadOnlyDictionary<char,double> MediumDigits =
        "1234567890"
        .Zip(new double[] {3.16,4.80,5.08,5.28,4.88,5.04,4.16,5.20,5.12,5.32})
        .ToDictionary(pair=>pair.First,pair=>pair.Second);

    public static double StrokeWidth(double xHeightMm)
    {
        if(!double.IsFinite(xHeightMm) || xHeightMm<=0)
            throw new ArgumentOutOfRangeException(nameof(xHeightMm));
        return xHeightMm/4;
    }

    public static double TileHeight(double xHeightMm) => 8*StrokeWidth(xHeightMm);

    // Chapter 7 section 2.3: standard gap between adjacent words.
    public static double WordSpace(double xHeightMm) => 2.5*StrokeWidth(xHeightMm);

    /// <summary>
    /// Uses exact TM1 uppercase dimensions when supported; otherwise a
    /// clearly flagged fallback. Do not use these metrics for sign approvals.
    /// </summary>
    public static LetterMetrics Measure(string text,double xHeightMm,
        TransportAlphabet alphabet=TransportAlphabet.Medium)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sw=StrokeWidth(xHeightMm);
        double units=0;
        var unknown=new HashSet<char>();
        for(var i=0;i<text.Length;i++)
        {
            var ch=text[i];
            if(ch==' ') { units+=2.5;continue; }
            if(alphabet==TransportAlphabet.Medium &&
               MediumCapitals.TryGetValue(ch,out var value))
            {
                // TM1 note 6: W has a wider tile when followed by T,V,X,Y,Z.
                if(ch=='W' && i+1<text.Length && "TVXYZ".Contains(text[i+1]))
                    value=7.56;
                // TM1 note 5: adjust tile overlap for T,V,Y preceding a,e,g,o,r,u.
                if(i+1<text.Length && "aegoru".Contains(text[i+1]))
                    value-=ch switch { 'T'=>0.44, 'V'=>0.68, 'Y'=>0.48, _=>0 };
                units+=value;continue;
            }
            if(alphabet==TransportAlphabet.Medium && (
                MediumLowercase.TryGetValue(ch,out value) ||
                MediumPunctuation.TryGetValue(ch,out value) ||
                MediumDigits.TryGetValue(ch,out value)))
            {
                units+=value;continue;
            }
            // Conservative fallback only. Not a verified tile-width table.
            units+=ch switch {
                'i' or 'l' or '1' or '!' => 2.5,
                '.' or ',' or ':' or '\'' => 2.0,
                'm' or 'w' => 6.8,
                _ => 4.8
            };
            unknown.Add(ch);
        }
        return new LetterMetrics(units*sw,8*sw,sw,unknown.Count==0,
            unknown.OrderBy(c=>c).ToArray());
    }

    public static bool IsExactMediumCapitals(string text) =>
        Measure(text,100).FullySpecified;
}
