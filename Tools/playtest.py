#!/usr/bin/env python3
"""Automated playtest through the Unity CLI: enters Play mode, drives the rider with the autopilot in a given mode,
forces one wave size, captures the game view now and then, and prints the PlaytestRecorder report.

  python3 Tools/playtest.py --mode Airs --scale 1.0 --seconds 60 --captures 6 --name airs_m

Modes: Pocket (tube rides), Airs (climb to the lip, launch, flick a trick), Weave (direction changes: camera swing).
Screenshots land in Assets/Screenshots~/playtest/<name>_<i>.png. The Editor may be unfocused: the HeadlessPlayTicker
steps frames explicitly.
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
a = ap.parse_args()

cmd("editor_play", timeout=90)
for _ in range(30):
    if ev("return UnityEditor.EditorApplication.isPlaying ? \"yes\" : \"no\";") == "yes":
        break
    time.sleep(1)
time.sleep(2)

setup = f"""
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
if (r == null) return "no rider";
var ap = r.gameObject.GetComponent<WavyBoard.Debugging.RiderAutoPilot>();
if (ap == null) ap = r.gameObject.AddComponent<WavyBoard.Debugging.RiderAutoPilot>();
ap.rider = r; ap.spot = r.spot; ap.active = true; ap.mode = WavyBoard.Debugging.RiderAutoPilot.Mode.{a.mode};
var rec = r.gameObject.GetComponent<WavyBoard.Debugging.PlaytestRecorder>();
if (rec == null) rec = r.gameObject.AddComponent<WavyBoard.Debugging.PlaytestRecorder>();
rec.rider = r; rec.captureDir = "{'Assets/Screenshots~/playtest/' + a.name if a.auto else ''}"; rec.maxCaptures = {a.auto};
System.IO.Directory.CreateDirectory("{'Assets/Screenshots~/playtest/' + a.name}");
var sched = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Wave.WaveSetScheduler>();
sched.sizes = new WavyBoard.Wave.WaveSetScheduler.SetSize[] {{ new WavyBoard.Wave.WaveSetScheduler.SetSize {{ name = "test", scale = {a.scale}f, weight = 1f }} }};
sched.setInterval = 34f; sched.wavePeriod = 11f; sched.wavesPerSet = 3;
WavyBoard.EditorTools.HeadlessPlayTicker.StepsPerUpdate = {a.steps};
return WavyBoard.EditorTools.HeadlessPlayTicker.Enable(true) + " | mode {a.mode} scale {a.scale}";
"""
print("setup:", ev(setup))

state = """
var r = UnityEngine.Object.FindFirstObjectByType<WavyBoard.Rider.RiderController>();
var s = r.Sample; var d = UnityEngine.Object.FindFirstObjectByType<WavyBoard.CameraRig.CameraDirector>();
return string.Format("t={0:0.0} {1} ride={2:0.0} tube={3} face={4:0.0} H={5:0.0} cd={6:0.0} phase={7:0.00} cam={8} last={9}",
  UnityEngine.Time.time, r.State, r.RideTime, r.InTube, r.FaceSpeed, s.WaveHeight, s.CrestDistance, s.BreakPhase, d != null ? d.ShotName : "-", r.LastEvent);
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
