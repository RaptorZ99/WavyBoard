#!/usr/bin/env python3
"""Automated playtest through the Unity CLI: enters Play mode, hands the rider to the bot (RiderBot, through the
RiderAutoPilot) with a given plan, forces one wave size, captures the game view now and then, and prints the
PlaytestRecorder report. It checks what only the real game shows (the camera, the HUD, frame pacing); the gameplay
itself is covered by the EditMode ride tests (RideTests, run in a second without Play mode).

  python3 Tools/playtest.py --mode Airs --scale 1.0 --seconds 60 --captures 6 --name airs_m

Plans: Pocket (in and out of the tube by the pace), InAndOut (in and out of the tube by pivots: cutback to the curl,
U-turn in the barrel), Exit (races the curl), Cruise (hands off), Stall, Airs (off the lip with a trick), Carve
(cutbacks: the camera swing). Screenshots land in Assets/Screenshots~/playtest/<name>_<i>.png. The Editor
may be unfocused: the HeadlessPlayTicker steps frames explicitly.
"""
import argparse, json, subprocess, time

def cmd(*args, timeout=60):
    out = subprocess.run(["unity", "command", *args, "--timeout", str(timeout), "--format", "json", "--no-banner"],
                         capture_output=True, text=True).stdout
    try:
        d = json.loads(out)
    except Exception:
        return None, out[:400]
    if not d.get("success"):
        return None, json.dumps(d.get("errors"))[:600]
    r = (d.get("data") or {}).get("result")
    return (r.get("result") if isinstance(r, dict) else r), None

def ev(code, timeout=60):
    r, err = cmd("eval", "--code", code, timeout=timeout)
    return r if err is None else "ERR " + err

ap = argparse.ArgumentParser()
ap.add_argument("--mode", default="Airs")
ap.add_argument("--scale", type=float, default=1.0)
ap.add_argument("--seconds", type=float, default=60)
ap.add_argument("--captures", type=int, default=6)
ap.add_argument("--steps", type=int, default=6)
ap.add_argument("--name", default="run")
ap.add_argument("--auto", type=int, default=0, help="event-driven screenshots (air apex, tube, landing, swing, ride)")
ap.add_argument("--mixed", action="store_true", help="keep the random set sizes instead of forcing --scale")
ap.add_argument("--interval", type=float, default=34)
ap.add_argument("--period", type=float, default=11)
ap.add_argument("--nospacing", action="store_true", help="disable the wave spacing guard (to reproduce crowding)")
a = ap.parse_args()

cmd("editor_play", timeout=90)
for _ in range(30):
    if ev("return UnityEditor.EditorApplication.isPlaying ? \"yes\" : \"no\";") == "yes":
        break
    time.sleep(1)
time.sleep(2)

sizes = "" if a.mixed else ("sched.sizes = new WavyBoard.Wave.WaveSetScheduler.SetSize[] { new WavyBoard.Wave.WaveSetScheduler.SetSize { name = \"test\", scale = %sf, weight = 1f } };" % a.scale)
setup = f"""
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
if (r == null) return "no rider";
var ap = r.gameObject.GetComponent<WavyBoard.Debugging.RiderAutoPilot>();
if (ap == null) ap = r.gameObject.AddComponent<WavyBoard.Debugging.RiderAutoPilot>();
ap.rider = r; ap.plan = WavyBoard.Debugging.RiderBot.Plan.{a.mode}; ap.enabled = true;
var rec = r.gameObject.GetComponent<WavyBoard.Debugging.PlaytestRecorder>();
if (rec == null) rec = r.gameObject.AddComponent<WavyBoard.Debugging.PlaytestRecorder>();
rec.rider = r; rec.captureDir = "{'Assets/Screenshots~/playtest/' + a.name if a.auto else ''}"; rec.maxCaptures = {a.auto};
System.IO.Directory.CreateDirectory("{'Assets/Screenshots~/playtest/' + a.name}");
var sched = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Wave.WaveSetScheduler>();
{sizes}
{'sched.minCrestSpacing = 0f; sched.spacingPerHeight = 0f;' if a.nospacing else ''}
sched.setInterval = {a.interval}f; sched.wavePeriod = {a.period}f; sched.wavesPerSet = 3;
WavyBoard.EditorTools.HeadlessPlayTicker.StepsPerUpdate = {a.steps};
return WavyBoard.EditorTools.HeadlessPlayTicker.Enable(true) + " | mode {a.mode} scale {a.scale}";
"""
print("setup:", ev(setup))

state = """
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
var s = r.Sample; var d = UnityEngine.Object.FindFirstObjectByType<WavyBoard.CameraRig.CameraDirector>();
return string.Format("t={0:0.0} {1} ride={2:0.0} tube={3} line={4:0.0}/{5:0.0} d={6:0.0} H={7:0.0} phase={8:0.00} cam={9} last={10}",
  UnityEngine.Time.time, r.State, r.RideTime, r.InTube, r.LineSpeed, r.PaceTarget, s.PeelDistance, s.WaveHeight, s.BreakPhase, d != null ? d.ShotName : "-", r.LastEvent);
"""
t0 = time.time()
interval = a.seconds / max(1, a.captures)
i = 0
while time.time() - t0 < a.seconds:
    time.sleep(interval)
    i += 1
    if a.captures > 0:
        cmd("capture_game_view", "--save_path", f"Assets/Screenshots~/playtest/{a.name}_{i}.png")
    print(f"[{i}]", ev(state))

print(ev("return UnityEngine.Object.FindFirstObjectByType<WavyBoard.Debugging.PlaytestRecorder>().Report();"))
ev("return WavyBoard.EditorTools.HeadlessPlayTicker.Enable(false);")
cmd("editor_stop", timeout=90)
print("done", a.name)
