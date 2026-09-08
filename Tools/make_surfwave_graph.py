"""Generates the two water shader graphs of Biscotte from the Storm Breakers ocean graph (CC0) by JSON surgery.

  python Tools/make_surfwave_graph.py                 -> Assets/_Project/Shaders/SurfWaveOcean.shadergraph   (surf wave mesh)
  SURF_MODE=ocean python Tools/make_surfwave_graph.py -> Assets/_Project/Shaders/OceanAmbientClip.shadergraph (ambient plane)

SurfWaveOcean keeps the exact SB water look and GPU ambient deformation, and adds for the surf wave mesh:
  - vertex normal = normalize(SB ambient normal + mesh normal - up)  (the face shape shades correctly)
  - foam (vertex colour G = lip/face foam, A = whitewater) as a lit albedo with 3 octaves of wave-relative noise
  - tube darkening / matte roof (vertex colour B), translucency glow (vertex colour R), aerated water under the whitewater
OceanAmbientClip is the SB graph with alpha clipping inside up to 4 rectangles (_SurfRectA0..3 / _SurfRectB0..3, set by
OceanAmbient every frame): the ambient plane gets a hole under each surf wave mesh, so the two surfaces never overlap.

After generating: unity command run_script --file Tools/CheckShader.cs --entry Biscotte.Tools.CheckShader.Main ...
Debug switches (surf mode): SURF_NO_FOAM=1 SURF_NO_SSS=1 SURF_NO_NORMAL=1 SURF_DEBUG=vc (emission = vertex colour)."""
import json, re, uuid, os

SRC = "Assets/ThirdParty/StormBreakers/7-Shaders/ocean.shadergraph"
MODE = os.environ.get("SURF_MODE", "surf")
DST = "Assets/_Project/Shaders/OceanAmbientClip.shadergraph" if MODE == "ocean" else "Assets/_Project/Shaders/SurfWaveOcean.shadergraph"

NO_FOAM = os.environ.get("SURF_NO_FOAM") == "1"
NO_SSS = os.environ.get("SURF_NO_SSS") == "1"
NO_NORMAL = os.environ.get("SURF_NO_NORMAL") == "1"
DEBUG = os.environ.get("SURF_DEBUG", "")

# tunables (surf mode)
FOAM_TILING = (150.0, 30.0)   # uv0 = (s/L, xi/lambda) -> metres
FOAM_NOISE_SCALE = 0.8
FOAM_EDGE = (0.25, 0.8)
AERATED = (0.5, 0.72, 0.76, 1.0)     # milky water colour under the whitewater foam
FOAM_TINT = (0.93, 0.965, 1.0, 1.0)
FOAM_SMOOTHNESS = 0.22
FOAM_NORMAL_FLATTEN = 0.7
TUBE_DARK = 0.8
SSS_BOOST = 0.8
SSS_GLOW = (0.04, 0.36, 0.32, 1.0)   # translucency tint added to the emission by vertex colour R (steep face / lip)

raw = open(SRC, encoding="utf-8").read()
objs = [json.loads(c) for c in re.split(r"\n\s*\n", raw.strip()) if c.strip()]
byid = {o["m_ObjectId"]: o for o in objs if "m_ObjectId" in o}
graph = next(o for o in objs if o["m_Type"] == "UnityEditor.ShaderGraph.GraphData")


def nid():
    return uuid.uuid4().hex


def by_prefix(p):
    m = [o for o in objs if o.get("m_ObjectId", "").startswith(p)]
    assert len(m) == 1, (p, len(m))
    return m[0]


def block(descriptor):
    return next(o for o in objs if o.get("m_Type") == "UnityEditor.ShaderGraph.BlockNode" and o.get("m_SerializedDescriptor") == descriptor)


new_objs = []
_y = [0]


def add(o):
    new_objs.append(o)
    byid[o["m_ObjectId"]] = o
    return o


def draw():
    _y[0] += 160
    return {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": 2400.0, "y": float(_y[0]), "width": 0.0, "height": 0.0}}


def v4(v):
    return {"x": float(v[0]), "y": float(v[1]), "z": float(v[2]), "w": float(v[3])}


def s_dyn(idx, name, out, val=(0, 0, 0, 0)):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": v4(val), "m_DefaultValue": v4((0, 0, 0, 0))})


def s_dynval(idx, name, out, val=0.0):
    m = {"e%d%d" % (r, c): float(val) for r in range(4) for c in range(4)}
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": m, "m_DefaultValue": {k: 0.0 for k in m}})


def s_v1(idx, name, out, val=0.0):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": float(val), "m_DefaultValue": 0.0, "m_Labels": []})


def s_v2(idx, name, out, val=(0, 0)):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector2MaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": {"x": float(val[0]), "y": float(val[1])}, "m_DefaultValue": {"x": 0.0, "y": 0.0}, "m_Labels": []})


def s_v3(idx, name, out, val=(0, 0, 0)):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": {"x": float(val[0]), "y": float(val[1]), "z": float(val[2])}, "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0}, "m_Labels": []})


def s_v4(idx, name, out, val=(0, 0, 0, 0)):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": name, "m_SlotType": 1 if out else 0, "m_Hidden": False, "m_ShaderOutputName": name, "m_StageCapability": 3, "m_Value": v4(val), "m_DefaultValue": v4((0, 0, 0, 0)), "m_Labels": []})


def s_uv(idx=0):
    return add({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.UVMaterialSlot", "m_ObjectId": nid(), "m_Id": idx, "m_DisplayName": "UV", "m_SlotType": 0, "m_Hidden": False, "m_ShaderOutputName": "UV", "m_StageCapability": 3, "m_Value": {"x": 0.0, "y": 0.0}, "m_DefaultValue": {"x": 0.0, "y": 0.0}, "m_Labels": [], "m_Channel": 0})


def node(typ, name, slots, extra=None, sgver=0):
    n = {"m_SGVersion": sgver, "m_Type": "UnityEditor.ShaderGraph." + typ, "m_ObjectId": nid(), "m_Group": {"m_Id": ""}, "m_Name": name, "m_DrawState": draw(),
         "m_Slots": [{"m_Id": s["m_ObjectId"]} for s in slots], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": False, "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []}}
    if extra:
        n.update(extra)
    add(n)
    graph["m_Nodes"].append({"m_Id": n["m_ObjectId"]})
    return n


# ---- node factories (slot ids follow the ShaderGraph node definitions)
def Multiply(b=0.0):
    return node("MultiplyNode", "Multiply", [s_dynval(0, "A", False), s_dynval(1, "B", False, b), s_dynval(2, "Out", True)])


def Add(b=(0, 0, 0, 0)):
    return node("AddNode", "Add", [s_dyn(0, "A", False), s_dyn(1, "B", False, b), s_dyn(2, "Out", True)])


def Subtract(b=(0, 0, 0, 0)):
    return node("SubtractNode", "Subtract", [s_dyn(0, "A", False), s_dyn(1, "B", False, b), s_dyn(2, "Out", True)])


def Lerp(b=(0, 0, 0, 0)):
    return node("LerpNode", "Lerp", [s_dyn(0, "A", False), s_dyn(1, "B", False, b), s_dyn(2, "T", False), s_dyn(3, "Out", True)])


def Saturate():
    return node("SaturateNode", "Saturate", [s_dyn(0, "In", False), s_dyn(1, "Out", True)])


def OneMinus():
    return node("OneMinusNode", "One Minus", [s_dyn(0, "In", False), s_dyn(1, "Out", True)])


def Normalize():
    return node("NormalizeNode", "Normalize", [s_dyn(0, "In", False), s_dyn(1, "Out", True)])


def Smoothstep(e1, e2):
    return node("SmoothstepNode", "Smoothstep", [s_dyn(0, "Edge1", False, (e1,) * 4), s_dyn(1, "Edge2", False, (e2,) * 4), s_dyn(2, "In", False), s_dyn(3, "Out", True)])


def Split():
    return node("SplitNode", "Split", [s_dyn(0, "In", False), s_v1(1, "R", True), s_v1(2, "G", True), s_v1(3, "B", True), s_v1(4, "A", True)])


def VertexColor():
    return node("VertexColorNode", "Vertex Color", [s_v4(0, "Out", True, (1, 1, 1, 1))], {"m_PreviewMode": 2})


def NormalVectorObj():
    return node("NormalVectorNode", "Normal Vector", [s_v3(0, "Out", True, (0, 0, 1))], {"m_PreviewMode": 2, "m_Space": 0})


def GradientNoise(scale):
    return node("GradientNoiseNode", "Gradient Noise", [s_uv(0), s_v1(1, "Scale", False, scale), s_v1(2, "Out", True)])


def TilingOffset(tiling):
    return node("TilingAndOffsetNode", "Tiling And Offset", [s_uv(0), s_v2(1, "Tiling", False, tiling), s_v2(2, "Offset", False), s_v2(3, "Out", True)])


def ColorC(rgba):
    return node("ColorNode", "Color", [s_v4(0, "Out", True)], {"m_Color": {"color": {"r": rgba[0], "g": rgba[1], "b": rgba[2], "a": rgba[3]}, "mode": 0}}, sgver=1)


def CustomFunction(name, fname, body, inputs, outputs):
    """inputs/outputs: list of (slotIndex, slotName, dim) with dim in (1, 3, 4)."""
    slots = []
    for idx, sname, dim in inputs:
        slots.append({1: s_v1, 3: s_v3, 4: s_v4}[dim](idx, sname, False))
    for idx, sname, dim in outputs:
        slots.append({1: s_v1, 3: s_v3, 4: s_v4}[dim](idx, sname, True))
    return node("CustomFunctionNode", name, slots, {"m_SourceType": 1, "m_FunctionName": fname, "m_FunctionSource": "", "m_FunctionBody": body}, sgver=1)


def PropertyRef(prop_name):
    prop = next(o for o in objs if o.get("m_Type", "").endswith("ShaderProperty") and o.get("m_Name") == prop_name)
    tmpl = next(o for o in objs if o.get("m_Type") == "UnityEditor.ShaderGraph.PropertyNode" and o.get("m_Property", {}).get("m_Id") == prop["m_ObjectId"])
    slots = []
    for s in tmpl["m_Slots"]:
        sc = json.loads(json.dumps(byid[s["m_Id"]]))
        sc["m_ObjectId"] = nid()
        add(sc)
        slots.append(sc)
    n = json.loads(json.dumps(tmpl))
    n["m_ObjectId"] = nid()
    n["m_DrawState"] = draw()
    n["m_Group"] = {"m_Id": ""}
    n["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    add(n)
    graph["m_Nodes"].append({"m_Id": n["m_ObjectId"]})
    out_slot = next(s["m_Id"] for s in slots if s["m_SlotType"] == 1)
    return n, out_slot


def Vector4Property(name, default=(0, 0, 0, 0)):
    """Adds an exposed Vector4 material property (in the 'Set By Scripts' category) and returns (propertyNode, outSlotId)."""
    tmpl_prop = next(o for o in objs if o.get("m_Type", "").endswith("Vector3ShaderProperty"))
    tmpl_node = next(o for o in objs if o.get("m_Type") == "UnityEditor.ShaderGraph.PropertyNode" and o.get("m_Property", {}).get("m_Id") == tmpl_prop["m_ObjectId"])
    p = json.loads(json.dumps(tmpl_prop))
    p["m_Type"] = "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty"
    p["m_ObjectId"] = nid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = name
    p["m_OverrideReferenceName"] = ""
    p["m_Value"] = v4(default)
    add(p)
    graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
    cat = next((o for o in objs if o.get("m_Type", "").endswith("CategoryData") and o.get("m_Name") == "Set By Scripts"), None)
    if cat is not None:
        cat["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})
    n = json.loads(json.dumps(tmpl_node))
    n["m_ObjectId"] = nid()
    n["m_DrawState"] = draw()
    n["m_Group"] = {"m_Id": ""}
    n["m_Property"] = {"m_Id": p["m_ObjectId"]}
    slots = []
    for s in tmpl_node["m_Slots"]:
        sc = json.loads(json.dumps(byid[s["m_Id"]]))
        sc["m_ObjectId"] = nid()
        sc["m_Type"] = "UnityEditor.ShaderGraph.Vector4MaterialSlot"
        sc["m_Value"] = v4(default)
        sc["m_DefaultValue"] = v4((0, 0, 0, 0))
        add(sc)
        slots.append(sc)
    n["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    add(n)
    graph["m_Nodes"].append({"m_Id": n["m_ObjectId"]})
    out_slot = next(s["m_Id"] for s in slots if s["m_SlotType"] == 1)
    return n, out_slot


edges = graph["m_Edges"]


def edge(a, aslot, b, bslot):
    edges.append({"m_OutputSlot": {"m_Node": {"m_Id": a["m_ObjectId"]}, "m_SlotId": aslot}, "m_InputSlot": {"m_Node": {"m_Id": b["m_ObjectId"]}, "m_SlotId": bslot}})


def remove_edge_into(node_obj, slot=None):
    for e in list(edges):
        if e["m_InputSlot"]["m_Node"]["m_Id"] == node_obj["m_ObjectId"] and (slot is None or e["m_InputSlot"]["m_SlotId"] == slot):
            edges.remove(e)
            return e
    raise KeyError(node_obj.get("m_Name"))


def out_slot_of(node_obj):
    return next(byid[s["m_Id"]]["m_Id"] for s in node_obj["m_Slots"] if byid[s["m_Id"]]["m_SlotType"] == 1)


# ---- anchors in the SB graph (object id prefixes, see Tools/sg_dump.py)
blk_emission = by_prefix("cbeb78")
blk_smooth = by_prefix("ae10c8")
blk_normalTS = by_prefix("150977")
blk_vnormal = by_prefix("8e7323")
blk_alpha = block("SurfaceDescription.Alpha")
mul_sss = by_prefix("64fc8c")
redirect_sss = by_prefix("41aefc")
src_emission = by_prefix("2fa898")
src_smooth = by_prefix("de3695")
src_normalTS = by_prefix("7c65d8")
src_vnormal = by_prefix("1480b3")
undeformed_frag = by_prefix("10e1bd")   # CustomInterpolator undeformedPosition (fragment stage)

if MODE == "ocean":
    # ================= ambient plane: alpha-clip holes under the surf wave meshes
    tgt = next(o for o in objs if o["m_Type"].endswith("UniversalTarget"))
    tgt["m_AlphaClip"] = True
    # AlphaClipThreshold block (clone of the Alpha block)
    thr = json.loads(json.dumps(blk_alpha))
    thr["m_ObjectId"] = nid()
    thr["m_Name"] = "SurfaceDescription.AlphaClipThreshold"
    thr["m_SerializedDescriptor"] = "SurfaceDescription.AlphaClipThreshold"
    tslots = []
    for s in blk_alpha["m_Slots"]:
        sc = json.loads(json.dumps(byid[s["m_Id"]]))
        sc["m_ObjectId"] = nid()
        sc["m_DisplayName"] = "Alpha Clip Threshold"
        sc["m_ShaderOutputName"] = "AlphaClipThreshold"
        sc["m_Value"] = 0.5
        add(sc)
        tslots.append(sc)
    thr["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in tslots]
    add(thr)
    graph["m_FragmentContext"]["m_Blocks"].append({"m_Id": thr["m_ObjectId"]})
    if any(b["m_Id"] == blk_alpha["m_ObjectId"] for b in graph["m_Nodes"]):
        graph["m_Nodes"].append({"m_Id": thr["m_ObjectId"]})

    body_lines = ["float inside = 0.0;", "float2 rel; float s; float d;"]
    for i in range(4):
        a = "A%d" % i
        b = "B%d" % i
        body_lines.append("rel = WP.xz - %s.xy; s = rel.x * %s.z + rel.y * %s.w; d = -rel.x * %s.w + rel.y * %s.z; inside = max(inside, step(%s.x, s) * step(s, %s.y) * step(%s.z, d) * step(d, %s.w));" % (a, a, a, a, a, b, b, b, b))
    body_lines.append("Out = 1.0 - inside;")
    cf = CustomFunction("SurfHole (Custom Function)", "SurfHole", "\n".join(body_lines),
                        [(0, "WP", 3)] + [(1 + i, "A%d" % i, 4) for i in range(4)] + [(5 + i, "B%d" % i, 4) for i in range(4)],
                        [(9, "Out", 1)])
    edge(undeformed_frag, out_slot_of(undeformed_frag), cf, 0)
    far = (1e9, 1e9, 1e9, 1e9)
    for i in range(4):
        pa, sa = Vector4Property("_SurfRectA%d" % i, (0, 0, 1, 0))
        pb, sb = Vector4Property("_SurfRectB%d" % i, far)
        edge(pa, sa, cf, 1 + i)
        edge(pb, sb, cf, 5 + i)
    e = remove_edge_into(blk_alpha)
    amul = Multiply()
    edge(byid[e["m_OutputSlot"]["m_Node"]["m_Id"]], e["m_OutputSlot"]["m_SlotId"], amul, 0)
    edge(cf, 9, amul, 1)
    edge(amul, 2, blk_alpha, e["m_InputSlot"]["m_SlotId"])
else:
    # ================= back faces of the lip ribbon / face grid seen from inside the barrel must NOT use the SB
    # underwater look (a flat translucent sheet): force every IsFrontFace predicate to true
    for o in list(objs):
        if o.get("m_Type", "").endswith("IsFrontFaceNode"):
            for e in list(edges):
                if e["m_OutputSlot"]["m_Node"]["m_Id"] == o["m_ObjectId"]:
                    tgt_node = byid[e["m_InputSlot"]["m_Node"]["m_Id"]]
                    for s in tgt_node["m_Slots"]:
                        sl = byid[s["m_Id"]]
                        if sl["m_Id"] == e["m_InputSlot"]["m_SlotId"]:
                            sl["m_Value"] = True
                    edges.remove(e)

    # ================= vertex stage: normal = normalize(ambientNormal + meshNormal - up)
    if not NO_NORMAL:
        e = remove_edge_into(blk_vnormal)
        assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_vnormal["m_ObjectId"]
        sub = Subtract((0, 1, 0, 0))
        edge(src_vnormal, e["m_OutputSlot"]["m_SlotId"], sub, 0)
        nv = NormalVectorObj()
        ad = Add()
        edge(sub, 2, ad, 0)
        edge(nv, 0, ad, 1)
        nz = Normalize()
        edge(ad, 2, nz, 0)
        edge(nz, 1, blk_vnormal, e["m_InputSlot"]["m_SlotId"])

    # ================= vertex stage: sub-surface boost from the translucency channel (vertex colour R)
    if not NO_SSS:
        e = None
        for cand in list(edges):
            if cand["m_OutputSlot"]["m_Node"]["m_Id"] == redirect_sss["m_ObjectId"] and cand["m_InputSlot"]["m_Node"]["m_Id"] == mul_sss["m_ObjectId"]:
                e = cand
                edges.remove(cand)
                break
        assert e is not None
        vc2 = VertexColor()
        sp2 = Split()
        edge(vc2, 0, sp2, 0)
        mk = Multiply(SSS_BOOST)
        edge(sp2, 1, mk, 0)
        sadd = Add()
        edge(redirect_sss, e["m_OutputSlot"]["m_SlotId"], sadd, 0)
        edge(mk, 2, sadd, 1)
        edge(sadd, 2, mul_sss, e["m_InputSlot"]["m_SlotId"])

    # ================= fragment stage
    vc = VertexColor()
    sp = Split()
    edge(vc, 0, sp, 0)

    if DEBUG == "vc":
        e = remove_edge_into(blk_emission)
        edge(vc, 0, blk_emission, e["m_InputSlot"]["m_SlotId"])
        e = remove_edge_into(blk_smooth)
        edge(sp, 4, blk_smooth, e["m_InputSlot"]["m_SlotId"])
    elif not NO_FOAM:
        # foamRaw = foam (G) + 0.55 * whitewater (A); three octaves of wave-relative gradient noise break it up
        wwk = Multiply(0.55)
        edge(sp, 4, wwk, 0)
        fadd = Add()
        edge(sp, 2, fadd, 0)
        edge(wwk, 2, fadd, 1)
        fraw = Saturate()
        edge(fadd, 2, fraw, 0)
        to = TilingOffset(FOAM_TILING)
        gn1 = GradientNoise(FOAM_NOISE_SCALE)
        gn2 = GradientNoise(FOAM_NOISE_SCALE * 3.7)
        gn3 = GradientNoise(FOAM_NOISE_SCALE * 14.0)
        edge(to, 3, gn1, 0)
        edge(to, 3, gn2, 0)
        edge(to, 3, gn3, 0)
        n1 = Multiply(0.7)
        edge(gn1, 2, n1, 0)
        n2 = Multiply(0.45)
        edge(gn2, 2, n2, 0)
        n3 = Multiply(0.2)
        edge(gn3, 2, n3, 0)
        nsum = Add()
        edge(n1, 2, nsum, 0)
        edge(n2, 2, nsum, 1)
        nsum2 = Add()
        edge(nsum, 2, nsum2, 0)
        edge(n3, 2, nsum2, 1)
        nadd = Add((0.3, 0.3, 0.3, 0.3))
        edge(nsum2, 2, nadd, 0)
        arg = Multiply()
        edge(fraw, 1, arg, 0)
        edge(nadd, 2, arg, 1)
        mask = Smoothstep(*FOAM_EDGE)
        edge(arg, 2, mask, 2)
        om = OneMinus()
        edge(mask, 3, om, 0)

        # foam is a lit albedo (BaseColor), not emission: it gets sun, sky and the ripple normals like real foam
        blk_base = block("SurfaceDescription.BaseColor")
        tint = ColorC(FOAM_TINT)
        bmul = Multiply()
        edge(tint, 0, bmul, 0)
        edge(mask, 3, bmul, 1)
        edge(bmul, 2, blk_base, 0)

        # emission: water colour darkened inside the tube (B), plus a translucency glow (R + roof), aerated under whitewater, gone under foam
        e = remove_edge_into(blk_emission)
        assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_emission["m_ObjectId"]
        tmul = Multiply(TUBE_DARK)
        edge(sp, 3, tmul, 0)
        tk = OneMinus()
        edge(tmul, 2, tk, 0)
        emul = Multiply()
        edge(src_emission, e["m_OutputSlot"]["m_SlotId"], emul, 0)
        edge(tk, 1, emul, 1)
        glowc = ColorC(SSS_GLOW)
        gb = Multiply(0.7)
        edge(sp, 3, gb, 0)
        gamt = Add()
        edge(sp, 1, gamt, 0)
        edge(gb, 2, gamt, 1)
        glow = Multiply()
        edge(glowc, 0, glow, 0)
        edge(gamt, 2, glow, 1)
        eadd = Add()
        edge(emul, 2, eadd, 0)
        edge(glow, 2, eadd, 1)
        aer = ColorC(AERATED)
        ak = Multiply(0.6)
        edge(sp, 4, ak, 0)
        lerp_a = Lerp()
        edge(eadd, 2, lerp_a, 0)
        edge(aer, 0, lerp_a, 1)
        edge(ak, 2, lerp_a, 2)
        efin = Multiply()
        edge(lerp_a, 3, efin, 0)
        edge(om, 1, efin, 1)
        edge(efin, 2, blk_emission, e["m_InputSlot"]["m_SlotId"])

        # smoothness: foam is rough, and the tube roof must not mirror the sky
        e = remove_edge_into(blk_smooth)
        assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_smooth["m_ObjectId"]
        lerp_t = Lerp((0.15, 0.15, 0.15, 0.15))
        edge(src_smooth, e["m_OutputSlot"]["m_SlotId"], lerp_t, 0)
        edge(sp, 3, lerp_t, 2)
        lerp_s = Lerp((FOAM_SMOOTHNESS,) * 4)
        edge(lerp_t, 3, lerp_s, 0)
        edge(mask, 3, lerp_s, 2)
        edge(lerp_s, 3, blk_smooth, e["m_InputSlot"]["m_SlotId"])

        # normal: flatten the ripples under the foam
        e = remove_edge_into(blk_normalTS)
        assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_normalTS["m_ObjectId"]
        fm = Multiply(FOAM_NORMAL_FLATTEN)
        edge(mask, 3, fm, 0)
        lerp_n = Lerp((0, 0, 1, 0))
        edge(src_normalTS, e["m_OutputSlot"]["m_SlotId"], lerp_n, 0)
        edge(fm, 2, lerp_n, 2)
        edge(lerp_n, 3, blk_normalTS, e["m_InputSlot"]["m_SlotId"])

graph["m_Path"] = "Biscotte"
os.makedirs(os.path.dirname(DST), exist_ok=True)
with open(DST, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n\n".join(json.dumps(o, indent=4) for o in objs + new_objs) + "\n")
print("mode=%s flags: NO_FOAM=%s NO_SSS=%s NO_NORMAL=%s DEBUG=%s" % (MODE, NO_FOAM, NO_SSS, NO_NORMAL, DEBUG))
print("wrote %s: %d original objects + %d new, edges=%d" % (DST, len(objs), len(new_objs), len(edges)))
