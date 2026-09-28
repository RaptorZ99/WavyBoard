#!/bin/bash
# Headless autopilot play session driven through the Unity CLI (Editor may be unfocused: HeadlessPlayTicker steps frames).
# Usage: bash Tools/headless_session.sh <name> [captures=6] [interval_s=8] [doAirs=false] [steps=6]
# Screenshots land in Assets/Screenshots~/<name>_<i>.png ; the rider state is printed after every capture.
name=${1:-run}; iters=${2:-6}; interval=${3:-8}; airs=${4:-false}; steps=${5:-6}
cd "$(dirname "$0")/.." || exit 1

unity command editor_play --timeout 60 --format json --no-banner >/dev/null 2>&1
python -c "import time; time.sleep(3)"

unity command eval --timeout 60 --format json --no-banner --code "
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
if (r == null) return \"no rider (is the Playground scene open?)\";
var ap = r.gameObject.GetComponent<WavyBoard.Debugging.RiderAutoPilot>();
if (ap == null) ap = r.gameObject.AddComponent<WavyBoard.Debugging.RiderAutoPilot>();
ap.rider = r; ap.spot = r.spot; ap.active = true; ap.doAirs = $airs;
WavyBoard.EditorTools.HeadlessPlayTicker.StepsPerUpdate = $steps;
return WavyBoard.EditorTools.HeadlessPlayTicker.Enable(true) + \" autopilot ready\";
" 2>&1 | python -c "
import sys,json
raw=sys.stdin.read()
try:
    d=json.loads(raw); r=(d.get('data') or {}).get('result'); print('setup:', r.get('result') if isinstance(r,dict) else r, d.get('errors') or '')
except Exception: print('setup raw:', raw[:600])"

for i in $(seq 1 "$iters"); do
  python -c "import time; time.sleep($interval)"
  unity command capture_game_view --save_path "Screenshots~/${name}_$i.png" --timeout 60 --format json --no-banner >/dev/null 2>&1
  unity command eval --timeout 60 --format json --no-banner --code "
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
var s = r.Sample;
return string.Format(\"t={0:0.0} f={1} state={2} ride={3:0.0} tube={4}/{5:0.0} tot={6:0.0} waves={7} wipe={8} ({9}) spd={10:0.0} phase={11:0.00} H={12:0.0} cd={13:0.0} peel={14:0.0} last={15}\",
  UnityEngine.Time.time, WavyBoard.EditorTools.HeadlessPlayTicker.Frames, r.State, r.RideTime, r.InTube, r.TubeTime, r.TotalTubeTime, r.WavesRidden, r.Wipeouts, r.LastWipeoutReason, r.Speed, s.BreakPhase, s.WaveHeight, s.CrestDistance, s.PeelDistance, r.LastEvent);
" 2>&1 | python -c "
import sys,json
raw=sys.stdin.read()
try:
    d=json.loads(raw); r=(d.get('data') or {}).get('result'); print('[$i]', r.get('result') if isinstance(r,dict) else r)
except Exception: print('[$i] raw:', raw[:400])"
done

unity command eval --timeout 60 --format json --no-banner --code "return WavyBoard.EditorTools.HeadlessPlayTicker.Enable(false);" >/dev/null 2>&1
unity command editor_stop --timeout 60 --format json --no-banner >/dev/null 2>&1
echo "session $name done"
