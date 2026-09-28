"""Poly Haven CC0 downloader (public keyless API).
Usage: python polyhaven_dl.py <out_dir> <spec> [--dry]
 spec lines: type:id:res[:fmt]   e.g. hdris:secluded_beach:4k:hdr | textures:coast_sand_02:2k:jpg | models:coast_rocks_03:2k:gltf
"""
import sys, json, os, urllib.request, hashlib
UA={"User-Agent":"Mozilla/5.0 (WavyBoardSurf asset fetch)"}
def _open(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA))
def _retrieve(url, dest):
    with _open(url) as r, open(dest, "wb") as f:
        while True:
            chunk=r.read(1<<20)
            if not chunk: break
            f.write(chunk)
API="https://api.polyhaven.com/files/"
def fetch(url, dest, dry):
    if dry: print("  would download", url, "->", dest); return
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if os.path.exists(dest) and os.path.getsize(dest)>0: print("  exists", dest); return
    print("  GET", url); _retrieve(url, dest)
def main():
    out=sys.argv[1]; spec=sys.argv[2]; dry="--dry" in sys.argv
    for line in open(spec):
        line=line.strip()
        if not line or line.startswith("#"): continue
        parts=line.split(":"); typ, aid, res = parts[0], parts[1], parts[2]; fmt = parts[3] if len(parts)>3 else None
        files=json.load(_open(API+aid))
        base=os.path.join(out, typ, aid)
        print(f"[{typ}] {aid} @ {res}")
        if typ=="hdris":
            f=files["hdri"][res][fmt or "hdr"]; fetch(f["url"], os.path.join(base, os.path.basename(f["url"])), dry)
        elif typ=="textures":
            fmt=fmt or "jpg"
            for mapname in ("Diffuse","nor_gl","Rough","AO","Displacement","arm"):
                if mapname in files and res in files[mapname] and fmt in files[mapname][res]:
                    f=files[mapname][res][fmt]; fetch(f["url"], os.path.join(base, os.path.basename(f["url"])), dry)
        elif typ=="models":
            fmt=fmt or "gltf"
            entry=files[fmt][res]; key=list(entry.keys())[0]; f=entry[key]
            fetch(f["url"], os.path.join(base, os.path.basename(f["url"])), dry)
            for rel, inc in (f.get("include") or {}).items():
                fetch(inc["url"], os.path.join(base, rel), dry)
if __name__=="__main__": main()
