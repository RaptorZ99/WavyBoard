"""Builds Assets/_Project/Shaders/SurfWaveOcean.shadergraph from the Storm Breakers ocean graph (CC0).
The copy keeps the exact SB water look and GPU ambient deformation, and adds for the surf wave mesh:
  - vertex normal = normalize(SB ambient normal + mesh normal - up)  (the face shape shades correctly)
  - foam (vertex color G = lip/face foam, A = whitewater) with wave-relative gradient noise breakup, into Emission/Smoothness/NormalTS
  - tube darkening (vertex color B) on the emission
  - extra sub-surface backlight from the pocket energy (vertex color R) in the vertex stage
Usage: python Tools/make_surfwave_graph.py   (then let Unity import the asset)"""
import json, re, uuid, os

SRC = "Assets/ThirdParty/StormBreakers/7-Shaders/ocean.shadergraph"
DST = "Assets/_Project/Shaders/SurfWaveOcean.shadergraph"

# tunables
FOAM_TILING = (150.0, 30.0)   # uv0 = (s/L, xi/lambda) -> metres
FOAM_NOISE_SCALE = 0.8
FOAM_EDGE = (0.15, 0.55)
FOAM_TINT = (0.93, 0.965, 1.0, 1.0)
FOAM_SMOOTHNESS = 0.22
FOAM_NORMAL_FLATTEN = 0.7
TUBE_DARK = 0.55
SSS_BOOST = 0.8

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


def PropertyRef(prop_name):
    prop = next(o for o in objs if o.get("m_Type", "").endswith("ShaderProperty") and o.get("m_Name") == prop_name)
    # clone an existing property node bound to the same property so the slot layout is right
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


edges = graph["m_Edges"]


def edge(a, aslot, b, bslot):
    edges.append({"m_OutputSlot": {"m_Node": {"m_Id": a["m_ObjectId"]}, "m_SlotId": aslot}, "m_InputSlot": {"m_Node": {"m_Id": b["m_ObjectId"]}, "m_SlotId": bslot}})


def remove_edge_into(node_obj, slot=None):
    for e in list(edges):
        if e["m_InputSlot"]["m_Node"]["m_Id"] == node_obj["m_ObjectId"] and (slot is None or e["m_InputSlot"]["m_SlotId"] == slot):
            edges.remove(e)
            return e
    raise KeyError(node_obj.get("m_Name"))


# ---- anchors in the SB graph (object id prefixes, see Tools/sg_dump.py)
blk_emission = by_prefix("cbeb78")
blk_smooth = by_prefix("ae10c8")
blk_normalTS = by_prefix("150977")
blk_vnormal = by_prefix("8e7323")
mul_sss = by_prefix("64fc8c")
redirect_sss = by_prefix("41aefc")
src_emission = by_prefix("2fa898")
src_smooth = by_prefix("de3695")
src_normalTS = by_prefix("7c65d8")
src_vnormal = by_prefix("1480b3")

# ================= vertex stage: normal = normalize(ambientNormal + meshNormal - up)
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

# ================= vertex stage: sub-surface boost from the pocket energy (vertex color R)
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

# ================= fragment stage: foam mask = smoothstep(foamRaw * (0.55 + 0.9 * noise))
vc = VertexColor()
sp = Split()
edge(vc, 0, sp, 0)
fadd = Add()
edge(sp, 2, fadd, 0)
edge(sp, 4, fadd, 1)
fraw = Saturate()
edge(fadd, 2, fraw, 0)
to = TilingOffset(FOAM_TILING)
gn = GradientNoise(FOAM_NOISE_SCALE)
edge(to, 3, gn, 0)
nmul = Multiply(0.9)
edge(gn, 2, nmul, 0)
nadd = Add((0.55, 0.55, 0.55, 0.55))
edge(nmul, 2, nadd, 0)
arg = Multiply()
edge(fraw, 1, arg, 0)
edge(nadd, 2, arg, 1)
mask = Smoothstep(*FOAM_EDGE)
edge(arg, 2, mask, 2)

# foam colour = total light * tint
plight, plight_out = PropertyRef("_totalLigthColor")
tint = ColorC(FOAM_TINT)
fcol = Multiply()
edge(plight, plight_out, fcol, 0)
edge(tint, 0, fcol, 1)

# emission: lerp to foam, then tube darkening
e = remove_edge_into(blk_emission)
assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_emission["m_ObjectId"]
lerp_e = Lerp()
edge(src_emission, e["m_OutputSlot"]["m_SlotId"], lerp_e, 0)
edge(fcol, 2, lerp_e, 1)
edge(mask, 3, lerp_e, 2)
tmul = Multiply(TUBE_DARK)
edge(sp, 3, tmul, 0)
tk = OneMinus()
edge(tmul, 2, tk, 0)
emul = Multiply()
edge(lerp_e, 3, emul, 0)
edge(tk, 1, emul, 1)
edge(emul, 2, blk_emission, e["m_InputSlot"]["m_SlotId"])

# smoothness: foam is rough
e = remove_edge_into(blk_smooth)
assert e["m_OutputSlot"]["m_Node"]["m_Id"] == src_smooth["m_ObjectId"]
lerp_s = Lerp((FOAM_SMOOTHNESS,) * 4)
edge(src_smooth, e["m_OutputSlot"]["m_SlotId"], lerp_s, 0)
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
print("wrote %s: %d original objects + %d new, edges=%d" % (DST, len(objs), len(new_objs), len(edges)))
