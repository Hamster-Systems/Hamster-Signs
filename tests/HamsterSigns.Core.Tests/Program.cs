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
var advanced=JunctionGeometry.IrregularRoundabout(1100,1000,260);
Check(advanced.Junction!.RingVertices.Count==8,"Irregular roundabout has editable radial vertices");
var a0=advanced.Junction.Arms[0];
Check(Math.Abs(JunctionGeometry.RadiusAtAngle(advanced.Junction,0)-221)<0.01,"Ring radius sampled at north point");
a0.Bends.Add(new JunctionPoint { X=90,Y=-480 });
a0.Bends.Add(new JunctionPoint { X=130,Y=-600 });
a0.Smooth=true;
a0.ShowArrow=true;
var path=JunctionGeometry.PathData(advanced);
Check(path.Contains(" Q ") && path.Contains(" Z "),"Smooth bends and irregular closed ring exported as paths");
var arrowData=JunctionGeometry.ArrowPathData(advanced);
Check(arrowData.Contains(" Z"),"Arrowhead exported as filled triangular outline");
var advancedSvg=SvgExporter.Export(new SignDocument { Elements=[advanced] });
Check(advancedSvg.Contains("stroke=\"none\""),"SVG exporter adds separate filled arrowhead path");
var advancedCopy=SignFile.Deserialize(SignFile.Serialize(new SignDocument { Elements=[advanced] }));
Check(advancedCopy.Elements[0].Junction!.Arms[0].Bends.Count==2 && advancedCopy.Elements[0].Junction!.Arms[0].ShowArrow,"Advanced arm state survives roundtrip");
var advancedEditor=new SignEditorSession(advancedCopy);
advancedEditor.Edit(doc=>doc.Elements[0].Junction!.RingVertices[0].RadiusMm=300);
Check(advancedEditor.Undo() && Math.Abs(advancedEditor.Document.Elements[0].Junction!.RingVertices[0].RadiusMm-221)<0.01,"Irregular ring radius undo restores shape");
var broken=JunctionGeometry.IrregularRoundabout(1100,1000);
broken.Junction!.Arms[0].Bends.Add(new JunctionPoint { X=double.PositiveInfinity,Y=10 });
Check(SignValidator.Check(new SignDocument { Elements=[broken] }).Any(x=>x.Code=="JUNCTION" && x.IsError),"Non-finite bend rejected");
broken.Junction.Arms[0].Bends.Clear();
broken.Junction.RingVertices[0].RadiusMm=-1;
Check(SignValidator.Check(new SignDocument { Elements=[broken] }).Any(x=>x.Code=="JUNCTION" && x.IsError),"Invalid irregular ring radius rejected");
var segmented=JunctionGeometry.TJunction(500,500);
segmented.Junction!.Arms[0].Bends.Add(new JunctionPoint {X=100,Y=-100});
Check(JunctionGeometry.PathData(segmented).Contains(" L "),"Straight multi-segment junction arm exported");
var specification=DirectionSignLayout.Example();
var built=DirectionSignLayout.Build(specification,"Test sign","HS-100");
Check(built.WidthMm>720 && built.HeightMm>0,"Direction layout auto sizes sign face");
Check(built.DirectionDesign!.Destinations.Count==3,"Structured destinations retained");
Check(built.Elements.Count(e=>e.Kind==ElementKind.Text)==6,"Direction rows generate place and route legends");
Check(built.Elements.Any(e=>e.Kind==ElementKind.Panel && e.Fill=="#174c9a"),"Motorway route patch created");
Check(built.Elements.Any(e=>e.Kind==ElementKind.Text && e.Text=="(M6)" && e.TextColor=="#ffffff"),"Route patch uses contrasting text");
Check(built.Elements.Any(e=>e.Kind==ElementKind.Line),"Directional arrows produce vector geometry");
Check(DirectionSignLayout.Validate(specification).Count==0,"Valid structured sign accepted");
var builtSvg=SvgExporter.Export(built);
Check(builtSvg.Contains("Kendal") && builtSvg.Contains("(M6)"),"Generated legends appear in SVG");
var builtCopy=SignFile.Deserialize(SignFile.Serialize(built));
Check(builtCopy.DirectionDesign!.Destinations[2].RouteStyle==RoutePanel.Motorway,"Direction design survives save/load");
var signEdit=new SignEditorSession(builtCopy);
signEdit.Edit(doc=>doc.Elements=DirectionSignLayout.Build(new DirectionSignSpecification {
    XHeightMm=120, Background=SignColour.MotorwayBlue,
    Destinations=[new DirectionDestination {Place="The NORTH",Route="M6",RouteStyle=RoutePanel.None,Arrow=DirectionArrow.Ahead}]
}).Elements);
Check(signEdit.Undo() && signEdit.Document.Elements.Count==built.Elements.Count,"Generated elements support undo");
var whiteSpec=new DirectionSignSpecification {
    Background=SignColour.NonPrimaryWhite,
    Destinations=[new DirectionDestination {Place="Village",Route="B1234",Arrow=DirectionArrow.Right,RouteStyle=RoutePanel.Motorway}]
};
var whiteSign=DirectionSignLayout.Build(whiteSpec);
Check(whiteSign.Elements.Where(e=>e.Kind==ElementKind.Line).All(e=>e.Stroke=="#111111"),"Black arrows on white signs");
Check(whiteSign.Elements.Any(e=>e.Kind==ElementKind.Text && e.Text=="Village" && e.TextColor=="#111111"),"White sign uses black main legend");
Check(DirectionSignLayout.Validate(new DirectionSignSpecification { XHeightMm=0,Destinations=[] }).Count>0,"Invalid x-height and empty design rejected");
Check(DirectionSignLayout.Build(new DirectionSignSpecification {
    Destinations=[new DirectionDestination {Place="A"}]
}).WidthMm%5==0,"Generated dimensions rounded to five-millimetre increments");
// v0.7 — official TM1 capital-letter tiles and honest fallback auditing.
Check(TransportLetterMetrics.StrokeWidth(100)==25,"x-height converts to sw at one quarter");
Check(TransportLetterMetrics.TileHeight(100)==200,"tile height equals twice x-height");
Check(TransportLetterMetrics.WordSpace(100)==62.5,"Chapter 7 word spacing equals 2.5 sw");
var mediumA=TransportLetterMetrics.Measure("A",100);
Check(mediumA.FullySpecified && Math.Abs(mediumA.WidthMm-136)<0.00001,"TM1 A tile width 5.44 sw");
Check(Math.Abs(TransportLetterMetrics.Measure("WT",100).WidthMm-298)<0.00001,"TM1 contextual W width before T");
Check(Math.Abs(TransportLetterMetrics.Measure("T",100).WidthMm-109)<0.00001,"TM1 standard T width");
Check(!TransportLetterMetrics.Measure("Kendal",100).FullySpecified,"Lowercase letters explicitly unverified");
Check(TransportLetterMetrics.Measure("A B",100).WidthMm==
    TransportLetterMetrics.Measure("AB",100).WidthMm+62.5,"Word gap accounted for in tiles");
Check(DirectionSignLayout.TypographyWarnings(specification).Any(w=>w.Contains("Unverified Transport")),
    "Typography audit lists unverified characters");
var capitalsOnly=new DirectionSignSpecification {
    XHeightMm=100,
    Destinations=[new DirectionDestination {Place="NORTH",Route="",Arrow=DirectionArrow.Ahead}]
};
var capitalWarnings=DirectionSignLayout.TypographyWarnings(capitalsOnly);
Check(!capitalWarnings.Any(w=>w.StartsWith("Unverified Transport")),"Verified uppercase tiles do not raise fallback warning");
Check(capitalWarnings.Any(w=>w.Contains("system font")),"Font outlines remain flagged as unverified");
var twoWordWidth=DirectionSignLayout.Build(new DirectionSignSpecification {
    XHeightMm=100, Destinations=[new DirectionDestination {Place="NORTH SOUTH"}]
}).WidthMm;
var oneWordWidth=DirectionSignLayout.Build(new DirectionSignSpecification {
    XHeightMm=100, Destinations=[new DirectionDestination {Place="NORTHSOUTH"}]
}).WidthMm;
Check(twoWordWidth>oneWordWidth,"Sign auto sizing respects the prescribed word gap");
Console.WriteLine("All tests passed.");
