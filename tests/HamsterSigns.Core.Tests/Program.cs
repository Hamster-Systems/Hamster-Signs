using HamsterSigns.Core;

static void Check(bool value, string message)
{
    if (!value) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var editor=new SignEditorSession(new SignDocument { Elements=[] });
editor.Edit(doc=>doc.Elements.Add(new SignElement { Kind=ElementKind.Text, Text="First" }));
Check(editor.Document.Elements.Count==1,"Create element");
Check(editor.CanUndo,"Undo available");
Check(editor.Undo(),"Undo executed");
Check(editor.Document.Elements.Count==0,"Undo restores document");
Check(editor.Redo(),"Redo executed");
Check(editor.Document.Elements.Count==1,"Redo restores document");

editor.Begin();
editor.Document.Elements[0].X=123;
editor.Document.Elements[0].Y=456;
editor.Commit();
Check(editor.Undo(),"Move undo executed");
Check(editor.Document.Elements[0].X==0 && editor.Document.Elements[0].Y==0,"Move restored");
Check(!editor.CanRedo==false,"Redo available after undo");

editor.Edit(doc=>doc.Title="Branch");
Check(!editor.CanRedo,"New operation clears redo");

Check(SignEditorSession.Snap(37,25)==25,"Grid snap downward");
Check(SignEditorSession.Snap(38,25)==50,"Grid snap upward");
Check(SignEditorSession.Snap(-38,25)==-50,"Negative snap");

var json=SignFile.Serialize(editor.Document);
var decoded=SignFile.Deserialize(json);
Check(decoded.Title==editor.Document.Title,"Serialization round trip");
var svg=SvgExporter.Export(new SignDocument { Elements=[new SignElement { Kind=ElementKind.Text, Text="A&B" }] });
Check(svg.Contains("A&amp;B"),"SVG escapes user text");
Check(svg.Contains("viewBox="),"SVG emits vector viewport");
Check(SignValidator.Check(editor.Document).Any(d=>d.Code=="STANDARDS"),"Compliance remains explicitly unverified");
Console.WriteLine("All tests passed.");
