#!/bin/bash
# Triggers a script recompile in the open Editor and waits for it (prints compile errors if any).
cd "$(dirname "$0")/.." || exit 1
unity command recompile --timeout 300 --format json --no-banner >/dev/null 2>&1
python -c "import time; time.sleep(4)"
for i in $(seq 1 80); do
  out=$(unity command recompile_status --timeout 30 --format json --no-banner 2>/dev/null)
  st=$(echo "$out" | python -c "
import sys,json
try:
    d=json.load(sys.stdin); r=d['data']['result']; r=json.loads(r) if isinstance(r,str) else r
    print(r.get('status'), 'failed=%s' % r.get('failed'), json.dumps(r.get('errors'))[:1500])
except Exception as e: print('unreachable')")
  case "$st" in
    completed*|idle*) echo "$st"; break;;
    compiling*|triggered*|unreachable*) python -c "import time; time.sleep(3)";;
    *) echo "$st"; break;;
  esac
done
# the Pipeline server restarts with the domain reload: wait until it answers again
for i in $(seq 1 30); do
  ok=$(unity command eval --timeout 20 --format json --no-banner --code 'return "ready";' 2>/dev/null | python -c "
import sys,json
try:
    d=json.load(sys.stdin); r=(d.get('data') or {}).get('result'); print('ok' if (r.get('result') if isinstance(r,dict) else r)=='ready' else 'no')
except Exception: print('no')")
  if [ "$ok" = "ok" ]; then exit 0; fi
  python -c "import time; time.sleep(3)"
done
echo "editor not answering after compilation"
