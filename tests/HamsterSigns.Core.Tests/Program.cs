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
Check(TransportLetterMetrics.Measure("Kendal",100).FullySpecified,"TM2 lowercase letters have verified tile widths");
Check(Math.Abs(TransportLetterMetrics.Measure("a",100).WidthMm-111)<0.00001,"TM2 lowercase a is 4.44 sw");
Check(Math.Abs(TransportLetterMetrics.Measure("6",100).WidthMm-126)<0.00001,"TM3 numeral 6 is 5.04 sw");
Check(Math.Abs(TransportLetterMetrics.Measure("(",100).WidthMm-105)<0.00001,"TM2 parenthesis is 4.20 sw");
Check(!TransportLetterMetrics.Measure("Café",100).FullySpecified,"Unsupported accented glyph is flagged");
Check(TransportLetterMetrics.Measure("A B",100).WidthMm==
    TransportLetterMetrics.Measure("AB",100).WidthMm+62.5,"Word gap accounted for in tiles");
Check(!DirectionSignLayout.TypographyWarnings(specification).Any(w=>w.Contains("Unverified Transport")),
    "Common TM1/TM2/TM3 sample legends no longer use width fallback");
var accentedSpec=new DirectionSignSpecification {
    Destinations=[new DirectionDestination {Place="Café",Route="A1"}]
};
Check(DirectionSignLayout.TypographyWarnings(accentedSpec).Any(w=>w.Contains("é")),
    "Typography audit lists genuinely unsupported accented glyphs");
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
// v0.8: embedded geometry provided by a local font renderer.
var vectorDoc=new SignDocument { Elements=[new SignElement {
    Kind=ElementKind.Text, Text="TEST", X=40,Y=50,FontSizeMm=100,
    FontRole=SignFontRole.TransportHeavy,TextColor="#000000"
}]};
var vectorSvg=SvgExporter.Export(vectorDoc,_=>"M 40 50 L 70 50 L 70 80 Z");
Check(vectorSvg.Contains("fill-rule=\"nonzero\""),"Outline export emits a vector filled path");
Check(vectorSvg.Contains("M 40 50 L 70 50 L 70 80 Z"),"Outline export embeds provider path");
Check(!vectorSvg.Contains("<text"),"Outlined text is not dependent on recipient fonts");
Check(SvgExporter.Export(vectorDoc).Contains("<text"),"SVG falls back to readable text when fonts are unavailable");
Check(SignFile.Deserialize(SignFile.Serialize(vectorDoc)).Elements[0].FontRole==SignFontRole.TransportHeavy,
    "Font role survives JSON round-trip");
var assigned=DirectionSignLayout.Build(DirectionSignLayout.Example());
Check(assigned.Elements.Any(e=>e.Kind==ElementKind.Text && e.Text=="(M6)" &&
    e.FontRole==SignFontRole.MotorwayPermanent),"Motorway route patch selects specialist font role");
Check(assigned.Elements.Any(e=>e.Kind==ElementKind.Text && e.Text=="Kendal" &&
    e.FontRole==SignFontRole.TransportMedium),"Primary destinations use Medium role");
// v0.9 — selected Chapter 7 geometry and sign-layout regression checks.
Check(DirectionLayoutRules.MainBorderWidth(100)==37.5,"Main border measures 1.5 stroke widths");
Check(DirectionLayoutRules.OuterTileClearance(100)==62.5,"Clearance inside main border is 2.5 sw");
Check(DirectionLayoutRules.RoutePatchInset(100)==25,"Route patches inset by 1 sw");
Check(DirectionLayoutRules.DarkPatchBorderWidth(100)==12.5,"Dark-on-dark patch white border 0.5 sw");
Check(DirectionLayoutRules.PatchCornerRadius(100)==25,"Patch inside corner radius 1 sw");
Check(DirectionLayoutRules.RoundingIncrement(100)==5 && DirectionLayoutRules.RoundingIncrement(150)==10 &&
      DirectionLayoutRules.RoundingIncrement(250)==15,"Chapter 7 size rounding increment varies by x-height");
Check(DirectionLayoutRules.RoundUp(151,150)==160 &&
      DirectionLayoutRules.RoundUp(151,150,false)==151,"Optional sign dimension rounding");
Check(DirectionLayoutRules.NeedsWhitePatchBorder(RoutePanel.Motorway,SignColour.PrimaryGreen),
      "Motorway blue patch on green requires white border");
Check(!DirectionLayoutRules.NeedsWhitePatchBorder(RoutePanel.NonPrimary,SignColour.PrimaryGreen),
      "White patch on green requires no extra white border");
Check(Math.Abs(TransportLetterMetrics.Measure("b",100).WidthMm-122)<0.00001,
      "Corrected TM2 b tile width equals 4.88 sw");
Check(Math.Abs(TransportLetterMetrics.Measure("'",100).WidthMm-39)<0.00001,
      "Corrected TM2 apostrophe width equals 1.56 sw");
var layout=DirectionSignLayout.Build(DirectionSignLayout.Example());
var mainBorder=layout.Elements.First();
Check(mainBorder.Kind==ElementKind.Panel &&
      Math.Abs(mainBorder.StrokeWidthMm-37.5)<0.00001 &&
      mainBorder.CornerRadiusMm==mainBorder.StrokeWidthMm,
      "Main sign border width and radius generated in sw");
var motorwayPatch=layout.Elements.Single(e=>e.Kind==ElementKind.Panel && e.Fill=="#174c9a");
var motorwayLegend=layout.Elements.Single(e=>e.Kind==ElementKind.Text && e.Text=="(M6)");
Check(Math.Abs(motorwayPatch.StrokeWidthMm-12.5)<0.00001 &&
      motorwayPatch.Stroke=="#ffffff","White patch border generated for dark on dark");
Check(motorwayPatch.CornerRadiusMm==25,"Route patch has rounded corners");
Check(Math.Abs(motorwayLegend.X-motorwayPatch.X-25)<0.00001,
      "Route patch horizontal text inset equals 1 sw");
Check(motorwayPatch.Y<=motorwayLegend.Y && motorwayPatch.Y+motorwayPatch.Height>=motorwayLegend.Y+200,
      "Route patch vertically encloses text tile");
var placeLegend=layout.Elements.Single(e=>e.Kind==ElementKind.Text && e.Text=="The SOUTH");
Check(Math.Abs(placeLegend.Y-motorwayLegend.Y)<0.00001,
      "Place and route-number tiles align vertically");
Check(motorwayPatch.X-placeLegend.X-TransportLetterMetrics.Measure(placeLegend.Text,100).WidthMm>=62.5-0.00001,
      "Route patch maintains at least 2.5 sw distance from legend tile");
Check(layout.WidthMm%DirectionLayoutRules.RoundingIncrement(100)==0 &&
      layout.HeightMm%DirectionLayoutRules.RoundingIncrement(100)==0,
      "Generated overall dimensions use optional rounded increments");
var noRoundDesign=DirectionSignLayout.Example();
noRoundDesign.RoundDimensions=false;
var unrounded=DirectionSignLayout.Build(noRoundDesign);
Check(unrounded.WidthMm<=layout.WidthMm && unrounded.HeightMm<=layout.HeightMm,
      "Disabling rounding preserves or reduces sign dimensions");
Check(SignFile.Deserialize(SignFile.Serialize(layout)).Elements.First().CornerRadiusMm==37.5,
      "Rounded panel geometry survives native project roundtrip");
Check(SvgExporter.Export(layout).Contains("rx=\"25\""),
      "SVG export includes rounded patch corners");
var invalidRadius=new SignDocument { Elements=[new SignElement {
    Kind=ElementKind.Panel,Width=100,Height=80,CornerRadiusMm=50
}]};
Check(SignValidator.Check(invalidRadius).Any(d=>d.Code=="RADIUS" && d.IsError),
      "Invalid panel corner radii rejected");
// v0.10 template library and portable vector drafting symbols.
var sourceSpec=DirectionSignLayout.Example();
var stackTemplate=SignTemplateCatalog.Build(SignTemplateKind.StackAdvance,sourceSpec);
Check(stackTemplate.Template==SignTemplateKind.StackAdvance,"Stack template records its type");
Check(stackTemplate.DirectionDesign!.Destinations.Count==3,"Template retains structured destinations");
var mapTemplate=SignTemplateCatalog.Build(SignTemplateKind.MapAdvance,sourceSpec);
Check(mapTemplate.WidthMm>stackTemplate.WidthMm,"Map template reserves a diagram column");
Check(mapTemplate.Elements.Any(e=>e.Kind==ElementKind.Junction),"Map template includes an editable junction");
Check(!mapTemplate.Elements.Any(e=>e.Kind==ElementKind.Line),"Map preset replaces row arrows with junction diagram");
var mapText=mapTemplate.Elements.First(e=>e.Kind==ElementKind.Text && e.Text=="Kendal");
var stackText=stackTemplate.Elements.First(e=>e.Kind==ElementKind.Text && e.Text=="Kendal");
Check(mapText.X>stackText.X,"Map template shifts legend and route columns");
Check(sourceSpec.Background==SignColour.PrimaryGreen,"Template generation does not mutate caller's specification");
var motorwayTemplate=SignTemplateCatalog.Build(SignTemplateKind.MotorwayMap,sourceSpec);
Check(motorwayTemplate.Background==SignColour.MotorwayBlue,"Motorway template uses blue background");
Check(motorwayTemplate.Elements.Any(e=>e.Kind==ElementKind.Junction),"Motorway template provides map diagram");
var templateSaved=SignFile.Deserialize(SignFile.Serialize(motorwayTemplate));
Check(templateSaved.Template==SignTemplateKind.MotorwayMap &&
      templateSaved.Elements.Any(e=>e.Kind==ElementKind.Junction),"Template and map geometry survive project save");
var changedTemplate=new SignEditorSession(stackTemplate);
changedTemplate.Edit(d=>{
    var replacement=SignTemplateCatalog.Build(SignTemplateKind.MapAdvance,sourceSpec);
    d.Elements=replacement.Elements;d.WidthMm=replacement.WidthMm;d.Template=replacement.Template;
});
Check(changedTemplate.Undo() && changedTemplate.Document.Template==SignTemplateKind.StackAdvance,
      "Template replacement can be undone");
var symbolKinds=Enum.GetValues<SignSymbolKind>();
Check(symbolKinds.Length==5,"Initial drafting symbol palette contains five entries");
foreach(var kind in symbolKinds)
{
    var symbol=SignSymbolLibrary.Create(kind,50,80,175);
    var symbolDocument=new SignDocument { Elements=[symbol] };
    Check(SignValidator.Check(symbolDocument).All(d=>!d.IsError),$"{kind} symbol validates");
    var symbolSvg=SvgExporter.Export(symbolDocument);
    Check(symbolSvg.Contains("transform=") && symbolSvg.Contains("<path"),
          $"{kind} symbol exports as a transformed SVG path");
    var symbolCopy=SignFile.Deserialize(SignFile.Serialize(symbolDocument));
    Check(symbolCopy.Elements[0].SymbolKind==kind,$"{kind} symbol survives JSON roundtrip");
}
Check(SignValidator.Check(new SignDocument {
    Elements=[new SignElement {Kind=ElementKind.Symbol,Width=-1,Height=200}]
}).Any(d=>d.Code=="SYMBOL" && d.IsError),"Invalid symbol sizing rejected");
Check(SvgExporter.Export(mapTemplate).Contains("<path"),"Map template exports vector diagram");
// Screenshot review follow-up: map route associations and readable arrows.
var screenshotMap=SignTemplateCatalog.Build(SignTemplateKind.MapAdvance,DirectionSignLayout.Example());
var screenshotJunction=screenshotMap.Elements.Single(e=>e.Kind==ElementKind.Junction);
var mappedRows=screenshotMap.DirectionDesign!.Destinations;
Check(mappedRows.All(d=>d.LinkedMapArmId.HasValue),"Map rows link to an editable road arm");
Check(mappedRows.Select(d=>d.LinkedMapArmId).Distinct().Count()==mappedRows.Count,
      "Distinct destination rows map to distinct arms");
Check(screenshotJunction.Junction!.Arms.Count==mappedRows.Count+1,
      "Map generator includes one incoming and one outgoing arm per destination");
Check(screenshotJunction.Junction.Arms.Count(a=>a.ShowArrow)==mappedRows.Count,
      "Outgoing routes have arrowheads and incoming road does not");
Check(screenshotJunction.Junction.Arms.All(a=>a.EndOffsetX.HasValue && a.EndOffsetY.HasValue),
      "Map arms use independent endpoint coordinates");
Check(screenshotJunction.Junction.Arms.Take(mappedRows.Count).All(a=>a.Bends.Count>0 && a.Smooth),
      "Map branches use curved bends rather than tiny straight compass spokes");
Check(JunctionGeometry.PathData(screenshotJunction).Contains(" Q "),
      "Map SVG includes curved route branches");
Check(JunctionGeometry.ArrowPathData(screenshotJunction).Contains(" Z "),
      "Map road tips generate vector arrowheads");
Check(screenshotJunction.Junction.Arms.All(a=>{
    var (px,py)=JunctionGeometry.Endpoint(screenshotJunction,a);
    return px>=0 && py>=0 && px<=screenshotMap.WidthMm && py<=screenshotMap.HeightMm;
}),"All generated map arms terminate within the sign face");
var linkedCopy=SignFile.Deserialize(SignFile.Serialize(screenshotMap));
Check(linkedCopy.DirectionDesign!.Destinations.All(d=>d.LinkedMapArmId.HasValue),
      "Destination-to-arm links survive saving and reopening");
var testLinkedArm=linkedCopy.Elements.Single(e=>e.Kind==ElementKind.Junction).Junction!.Arms[0];
var previousX=testLinkedArm.EndOffsetX!.Value;
var linkEditor=new SignEditorSession(linkedCopy);
linkEditor.Edit(d=>d.Elements.Single(e=>e.Kind==ElementKind.Junction).Junction!.Arms[0].EndOffsetX+=35);
Check(linkEditor.Undo() &&
      Math.Abs(linkEditor.Document.Elements.Single(e=>e.Kind==ElementKind.Junction)
          .Junction!.Arms[0].EndOffsetX!.Value-previousX)<0.00001,
      "Map endpoint movement is undoable");
var noLink=JunctionGeometry.Roundabout(200,200,40);
noLink.Junction!.Arms[0].EndOffsetX=10;
Check(JunctionGeometry.Validate(noLink).Any(e=>e.Contains("coordinate pair")),
      "Incomplete endpoint override rejected");
var arrowStack=SignTemplateCatalog.Build(SignTemplateKind.StackAdvance,DirectionSignLayout.Example());
Check(arrowStack.Elements.Where(e=>e.Kind==ElementKind.Line).All(e=>e.StrokeWidthMm>=17.5),
      "Stack direction arrows have increased stroke weight");
// v0.11: legend positions follow road branches, not an unrelated stacked list.
var mapZones=SignTemplateCatalog.Build(SignTemplateKind.MapAdvance,DirectionSignLayout.Example());
var centre=mapZones.Elements.Single(e=>e.Kind==ElementKind.Junction);
var leftLabel=mapZones.Elements.Single(e=>e.Kind==ElementKind.Text && e.Text=="Lancaster");
var rightLabel=mapZones.Elements.Single(e=>e.Kind==ElementKind.Text && e.Text=="The SOUTH");
var aheadLabel=mapZones.Elements.Single(e=>e.Kind==ElementKind.Text && e.Text=="Kendal");
Check(leftLabel.X<centre.X && rightLabel.X>centre.X && aheadLabel.X>centre.X,
    "Destinations occupy side-specific zones around map centre");
Check(aheadLabel.Y<centre.Y && Math.Abs(leftLabel.Y-rightLabel.Y)<200,
    "Ahead legend sits above the junction; side legends flank it");
Check(MapSignLayout.Audit(mapZones).Count==0,
    "Example map has no orphaned links or overlapping text/patch tiles");
var routeLabels=mapZones.Elements.Where(e=>e.Kind==ElementKind.Text &&
    (e.Text=="A6" || e.Text=="A591" || e.Text=="(M6)")).ToArray();
Check(routeLabels.Length==3,"All map route numbers remain present");
var motorwayPatchInMap=mapZones.Elements.Single(e=>e.Kind==ElementKind.Panel &&
    e.Fill=="#174c9a");
Check(motorwayPatchInMap.X>centre.X && motorwayPatchInMap.Y>aheadLabel.Y,
    "Motorway patch stays with its right-side destination");
var allLeft=new DirectionSignSpecification {
    Destinations=Enumerable.Range(0,6).Select(i=>new DirectionDestination {
        Place=$"Destination {i+1}",Route="A123",Arrow=DirectionArrow.Left
    }).ToList()
};
var crowdedMap=SignTemplateCatalog.Build(SignTemplateKind.MapAdvance,allLeft);
Check(MapSignLayout.Audit(crowdedMap).Count==0,
    "Six left-side branches fit without label collisions");
Check(crowdedMap.Elements.Where(e=>e.Kind==ElementKind.Text)
    .All(e=>e.Y>=0 && e.Y+TransportLetterMetrics.TileHeight(e.FontSizeMm)<=crowdedMap.HeightMm),
    "Six left-side destination tiles fit on the sign face");
var newMapSave=SignFile.Deserialize(SignFile.Serialize(mapZones));
Check(MapSignLayout.Audit(newMapSave).Count==0,
    "Positioned map legends and arm links survive JSON save/load");
var orphaned=SignFile.Deserialize(SignFile.Serialize(mapZones));
orphaned.DirectionDesign!.Destinations[0].LinkedMapArmId=Guid.NewGuid();
Check(MapSignLayout.Audit(orphaned).Any(w=>w.Contains("no matching road arm")),
    "Map audit flags broken destination-to-arm links");
// v0.12 map legend groups and sampled road/label clearance.
var groupMap=SignFile.Deserialize(SignFile.Serialize(mapZones));
var groupedRow=groupMap.DirectionDesign!.Destinations.Single(d=>d.Place=="The SOUTH");
Check(groupedRow.MapPlaceElementId.HasValue && groupedRow.MapRouteElementId.HasValue &&
      groupedRow.MapPatchElementId.HasValue,"Map destination retains group component IDs");
var linkedParts=MapLegendGroups.Resolve(groupMap,groupedRow.MapPatchElementId!.Value);
Check(linkedParts.Count==3,"Selecting route patch resolves whole destination group");
var startPositions=MapLegendGroups.Origins(groupMap,groupedRow.MapPlaceElementId!.Value);
Check(startPositions.Count==3,"Position snapshots include place route and patch");
Check(MapLegendGroups.MoveFromOrigins(groupMap,startPositions,15,20,false),
      "Linked map group moves all component positions");
Check(linkedParts.All(e=>Math.Abs(e.X-startPositions[e.Id].X-15)<0.00001 &&
      Math.Abs(e.Y-startPositions[e.Id].Y-20)<0.00001),
      "All group components keep their relative alignment");
Check(!MapLegendGroups.MoveFromOrigins(groupMap,startPositions,-100000,0),
      "Group movement is blocked outside the sign bounds");
Check(linkedParts.All(e=>e.X>=0),"Rejected group movement restores existing positions");
var clearanceMap=SignFile.Deserialize(SignFile.Serialize(mapZones));
Check(MapRoadClearance.Audit(clearanceMap).Count==0,
      "Generated example starts with road-to-legend clearance");
var centreRoad=clearanceMap.Elements.Single(e=>e.Kind==ElementKind.Junction);
var collisionRow=clearanceMap.DirectionDesign!.Destinations.First();
var collisionText=clearanceMap.Elements.Single(e=>e.Id==collisionRow.MapPlaceElementId);
collisionText.X=centreRoad.X;
collisionText.Y=centreRoad.Y;
Check(MapRoadClearance.Audit(clearanceMap).Any(s=>s.Contains("Road-to-legend")),
      "Collision check catches legend placed over the incoming road");
var blockedMap=SignFile.Deserialize(SignFile.Serialize(mapZones));
var firstGroup=blockedMap.DirectionDesign!.Destinations.First();
var groupStart=MapLegendGroups.Origins(blockedMap,firstGroup.MapPlaceElementId!.Value);
var original=blockedMap.Elements.Single(e=>e.Id==firstGroup.MapPlaceElementId);
var blockedX=blockedMap.Elements.Single(e=>e.Kind==ElementKind.Junction).X-original.X;
var blockedY=blockedMap.Elements.Single(e=>e.Kind==ElementKind.Junction).Y-original.Y;
Check(!MapLegendGroups.MoveFromOrigins(blockedMap,groupStart,blockedX,blockedY,true),
      "Collision-protected move rejects a legend placed over a road");
Check(Math.Abs(original.X-groupStart[original.Id].X)<0.00001,
      "Rejected move restores the original label coordinates");
var groupHistory=new SignEditorSession(SignFile.Deserialize(SignFile.Serialize(mapZones)));
var historyRow=groupHistory.Document.DirectionDesign!.Destinations.First();
var historyStart=MapLegendGroups.Origins(groupHistory.Document,historyRow.MapPlaceElementId!.Value);
groupHistory.Edit(d=> {
    if(!MapLegendGroups.MoveFromOrigins(d,historyStart,5,5,false))
        throw new InvalidOperationException("Expected legend move to succeed.");
});
Check(groupHistory.Undo() &&
      groupHistory.Document.Elements.Single(e=>e.Id==historyRow.MapPlaceElementId).X==
      historyStart[historyRow.MapPlaceElementId!.Value].X,
      "One undo restores the complete destination group");
// v0.13: destination-aware road routing and deterministic rollback.
var routingDoc=SignFile.Deserialize(SignFile.Serialize(mapZones));
var routingRow=routingDoc.DirectionDesign!.Destinations.First();
var routingId=routingRow.MapPlaceElementId!.Value;
var routingOrigins=MapLegendGroups.Origins(routingDoc,routingId);
var routingNetwork=routingDoc.Elements.Single(e=>e.Kind==ElementKind.Junction);
var linkedRoutingArm=routingNetwork.Junction!.Arms.Single(a=>a.Id==routingRow.LinkedMapArmId);
var otherEndBefore=routingNetwork.Junction.Arms.Where(a=>a.Id!=linkedRoutingArm.Id)
    .ToDictionary(a=>a.Id,a=>JunctionGeometry.Endpoint(routingNetwork,a));
var endpointBefore=JunctionGeometry.Endpoint(routingNetwork,linkedRoutingArm);
var reroute=MapAutoRouter.MoveLinkedLegend(routingDoc,routingId,routingOrigins,5,5,false);
Check(reroute.Success && reroute.CandidateCount>0,
    "Auto-router can move a group and evaluate road bend candidates");
var endpointAfter=JunctionGeometry.Endpoint(routingNetwork,linkedRoutingArm);
Check(Math.Abs(endpointAfter.X-endpointBefore.X-5)<0.00001 &&
      Math.Abs(endpointAfter.Y-endpointBefore.Y-5)<0.00001,
    "Road endpoint follows the destination movement exactly");
Check(routingNetwork.Junction.Arms.Where(a=>a.Id!=linkedRoutingArm.Id)
    .All(a=> {
        var before=otherEndBefore[a.Id];
        var after=JunctionGeometry.Endpoint(routingNetwork,a);
        return Math.Abs(before.X-after.X)<0.00001 && Math.Abs(before.Y-after.Y)<0.00001;
    }),"Auto-router leaves unrelated roads unchanged");
Check(routingDoc.Elements.Single(e=>e.Id==routingId).X==routingOrigins[routingId].X+5,
    "Auto-router moves the destination components");
var beforeInvalid=routingDoc.Elements.Single(e=>e.Id==routingId).X;
var endpointBeforeInvalid=JunctionGeometry.Endpoint(routingNetwork,linkedRoutingArm);
var invalidMove=MapAutoRouter.MoveLinkedLegend(routingDoc,routingId,routingOrigins,-100000,0,true);
Check(!invalidMove.Success,"Auto-router rejects out-of-face destination movement");
Check(Math.Abs(routingDoc.Elements.Single(e=>e.Id==routingId).X-beforeInvalid)<0.00001 &&
      JunctionGeometry.Endpoint(routingNetwork,linkedRoutingArm)==endpointBeforeInvalid,
    "Rejected route leaves group and arm geometry unchanged");
var linkedHistory=new SignEditorSession(SignFile.Deserialize(SignFile.Serialize(mapZones)));
var selectedHistoryRow=linkedHistory.Document.DirectionDesign!.Destinations.First();
var routeOrigins=MapLegendGroups.Origins(linkedHistory.Document,selectedHistoryRow.MapPlaceElementId!.Value);
var historicalArm=linkedHistory.Document.Elements.Single(e=>e.Kind==ElementKind.Junction)
    .Junction!.Arms.Single(a=>a.Id==selectedHistoryRow.LinkedMapArmId);
var oldEndpoint=JunctionGeometry.Endpoint(
    linkedHistory.Document.Elements.Single(e=>e.Kind==ElementKind.Junction),historicalArm);
linkedHistory.Edit(d=> {
    var moved=MapAutoRouter.MoveLinkedLegend(d,selectedHistoryRow.MapPlaceElementId!.Value,
        routeOrigins,5,5,false);
    if(!moved.Success) throw new InvalidOperationException(moved.Message);
});
Check(linkedHistory.Undo() &&
    Math.Abs(linkedHistory.Document.Elements.Single(e=>e.Id==selectedHistoryRow.MapPlaceElementId).X-
        routeOrigins[selectedHistoryRow.MapPlaceElementId!.Value].X)<0.00001,
    "Undo restores grouped auto-route movement");
var restoredJunction=linkedHistory.Document.Elements.Single(e=>e.Kind==ElementKind.Junction);
Check(JunctionGeometry.Endpoint(restoredJunction,
    restoredJunction.Junction!.Arms.Single(a=>a.Id==selectedHistoryRow.LinkedMapArmId))==oldEndpoint,
    "Undo also restores the associated road endpoint");
Console.WriteLine("All tests passed.");
