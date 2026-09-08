"""Dump a Unity Shader Graph (.shadergraph JSON stream) as a readable node/edge list. Usage: python Tools/sg_dump.py <file> [filter]"""
import json, re, sys, glob, os
path = sys.argv[1]
filt = sys.argv[2] if len(sys.argv) > 2 else None
raw = open(path, encoding="utf-8").read()
objs = [json.loads(x) for x in re.split(r'\n\s*\n', raw.strip()) if x.strip()]
byid = {o['m_ObjectId']: o for o in objs if 'm_ObjectId' in o}
# guid -> subgraph name
guid2name = {}
for meta in glob.glob(os.path.join(os.path.dirname(path), '**', '*.shadersubgraph.meta'), recursive=True):
    m = re.search(r'guid: (\w+)', open(meta, encoding='utf-8').read())
    if m: guid2name[m.group(1)] = os.path.basename(meta).replace('.shadersubgraph.meta', '')
g = [o for o in objs if o.get('m_Type') == 'UnityEditor.ShaderGraph.GraphData'][0]
def nname(o):
    t = o.get('m_Type', '').split('.')[-1]
    if t == 'PropertyNode':
        p = byid.get(o.get('m_Property', {}).get('m_Id'), {})
        return f"Prop[{p.get('m_Name')}]"
    if t == 'SubGraphNode':
        m = re.search(r'"guid":\s*"(\w+)"', o.get('m_SerializedSubGraph', ''))
        return f"SubGraph[{guid2name.get(m.group(1), m.group(1)) if m else '?'}]"
    if t == 'BlockNode': return f"Block[{o.get('m_SerializedDescriptor')}]"
    if t == 'CustomInterpolatorNode': return f"CustomInterp[{o.get('customBlockNodeName')}]"
    if t == 'PositionNode': return f"Position[space={o.get('m_Space')}]"
    if t == 'NormalVectorNode': return f"NormalVector[space={o.get('m_Space')}]"
    if t == 'TransformNode': return f"Transform[{o.get('m_Conversion',{}).get('from')}->{o.get('m_Conversion',{}).get('to')} type={o.get('m_ConversionType')}]"
    return f"{t}[{o.get('m_Name','')}]"
def sname(sid):
    s = byid.get(sid, {}); return s.get('m_DisplayName') or s.get('m_ShaderOutputName') or str(s.get('m_SlotId'))
def short(oid): return oid[:6]
for e in g.get('m_Edges', []):
    a = e['m_OutputSlot']; b = e['m_InputSlot']
    na = byid[a['m_Node']['m_Id']]; nb = byid[b['m_Node']['m_Id']]
    line = f"{nname(na)}#{short(na['m_ObjectId'])}.{sname(a['m_SlotId'])} -> {nname(nb)}#{short(nb['m_ObjectId'])}.{sname(b['m_SlotId'])}"
    if filt is None or filt.lower() in line.lower(): print(line)
