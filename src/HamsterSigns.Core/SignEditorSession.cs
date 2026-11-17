namespace HamsterSigns.Core;

/// <summary>Document editing state and history independent of the desktop framework.</summary>
public sealed class SignEditorSession
{
    private readonly Stack<string> undo = new();
    private readonly Stack<string> redo = new();
    private string? transaction;
    public SignDocument Document { get; private set; }

    public SignEditorSession(SignDocument document) => Document = document;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public void Begin()
    {
        if (transaction is not null) throw new InvalidOperationException("An edit is already active.");
        transaction = SignFile.Serialize(Document);
    }
    public bool Commit()
    {
        if (transaction is null) return false;
        var before = transaction;
        transaction = null;
        if (before == SignFile.Serialize(Document)) return false;
        undo.Push(before);
        redo.Clear();
        return true;
    }
    public void Cancel()
    {
        if (transaction is null) return;
        Document = SignFile.Deserialize(transaction);
        transaction = null;
    }
    public void Edit(Action<SignDocument> edit)
    {
        Begin();
        try { edit(Document); Commit(); }
        catch { Cancel(); throw; }
    }
    public bool Undo()
    {
        if (transaction is not null || undo.Count == 0) return false;
        redo.Push(SignFile.Serialize(Document));
        Document = SignFile.Deserialize(undo.Pop());
        return true;
    }
    public bool Redo()
    {
        if (transaction is not null || redo.Count == 0) return false;
        undo.Push(SignFile.Serialize(Document));
        Document = SignFile.Deserialize(redo.Pop());
        return true;
    }
    public void ReplaceDocument(SignDocument document)
    {
        transaction = null;
        undo.Clear();
        redo.Clear();
        Document = document;
    }
    public static double Snap(double coordinate, double step)
    {
        if (!double.IsFinite(coordinate)) throw new ArgumentOutOfRangeException(nameof(coordinate));
        return step > 0 && double.IsFinite(step) ? Math.Round(coordinate / step, MidpointRounding.AwayFromZero) * step : coordinate;
    }
    public static void Move(SignElement element, double x, double y, double originX, double originY, double grid)
    {
        var dx = Snap(x, grid) - originX;
        var dy = Snap(y, grid) - originY;
        if (element.Kind is ElementKind.Line or ElementKind.CubicBezier) { element.X2 += dx; element.Y2 += dy; }
        if (element.Kind == ElementKind.CubicBezier)
        {
            element.Control1X += dx; element.Control1Y += dy;
            element.Control2X += dx; element.Control2Y += dy;
        }
        element.X += dx;
        element.Y += dy;
    }
}
