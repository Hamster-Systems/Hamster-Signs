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
Check(editor.CanRedo,"Redo available after undo");

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
var curve=new SignElement
{
    Kind=ElementKind.CubicBezier,
    X=100, Y=200, Control1X=180, Control1Y=250,
    Control2X=300, Control2Y=350, X2=400, Y2=500,
    StrokeWidthMm=40
};
var curveDoc=new SignDocument { Elements=[curve] };
var curveSvg=SvgExporter.Export(curveDoc);
Check(curveSvg.Contains("M 100 200 C 180 250, 300 350, 400 500"),"Curved road exported as SVG cubic path");
var curveCopy=SignFile.Deserialize(SignFile.Serialize(curveDoc));
Check(curveCopy.Elements[0].Control2Y==350,"Curve control points round-trip");
var curveEditor=new SignEditorSession(curveCopy);
curveEditor.Edit(d=>SignEditorSession.Move(d.Elements[0],125,250,100,200,25));
Check(curveEditor.Document.Elements[0].Control1X==205,"Move updates curve control points");
Check(curveEditor.Undo() && curveEditor.Document.Elements[0].Control1X==180,"Undo restores curved road geometry");
curve.Control1X=double.NaN;
Check(SignValidator.Check(curveDoc).Any(d=>d.Code=="COORD" && d.IsError),"Invalid control points rejected");
var junction=JunctionGeometry.Roundabout(1000,900,200);
Check(junction.Junction!.Arms.Count==4,"Roundabout creates four connected arms");
var p0=JunctionGeometry.Endpoint(junction,junction.Junction.Arms[0]);
Check(Math.Abs(p0.X-1000)<0.00001 && Math.Abs(p0.Y-250)<0.00001,"Roundabout north arm endpoint");
var d=JunctionGeometry.PathData(junction);
Check(d.Contains(" A 200 200 ") && d.Contains(" L "),"Roundabout SVG paths contain ring and connected arms");
var jdoc=new SignDocument { Elements=[junction] };
var exported=SvgExporter.Export(jdoc);
Check(exported.Contains("stroke-linejoin="),"Junction SVG retains joined stroke treatment");
var jcopy=SignFile.Deserialize(SignFile.Serialize(jdoc));
Check(jcopy.Elements[0].Junction!.Arms.Count==4,"Junction arms survive project save and load");
var jEditor=new SignEditorSession(jcopy);
jEditor.Edit(doc=>doc.Elements[0].Junction!.Arms.Add(new JunctionArm { AngleDegrees=45,LengthMm=450 }));
Check(jEditor.Document.Elements[0].Junction!.Arms.Count==5,"Add arm");
Check(jEditor.Undo() && jEditor.Document.Elements[0].Junction!.Arms.Count==4,"Junction arm edits are undoable");
var t=JunctionGeometry.TJunction(1000,900);
Check(t.Junction!.Type==JunctionType.Intersection && t.Junction.Arms.Count==3,"T-junction preset");
var cross=JunctionGeometry.Crossroads(1000,900);
Check(cross.Junction!.Arms.Count==4,"Crossroads preset");
JunctionGeometry.MoveEndpoint(t,t.Junction.Arms[0],1500,900);
Check(Math.Abs(t.Junction.Arms[0].AngleDegrees-90)<0.00001,"Dragging endpoint updates arm angle");
t.Junction.Arms[0].LengthMm=-1;
Check(SignValidator.Check(new SignDocument { Elements=[t] }).Any(x=>x.Code=="JUNCTION" && x.IsError),"Invalid junction arm rejected");
Console.WriteLine("All tests passed.");
