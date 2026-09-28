using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.Rider
{
    public partial class RiderController
    {
        float surfaceRoll;             // visual roll of a flat roll / snap played on the water
        string surfaceTrick = "";      // manoeuvre being drawn on the water; it scores when it finishes
        float surfacePoints, surfaceTimer;

        WaveZone ComputeZone()
        {
            var s = lastSample;
            switch (State)
            {
                case RiderState.Air: return WaveZone.Air;
                case RiderState.TakeOff:
                case RiderState.Ride:
                    if (InTube) return WaveZone.Tube;
                    // "at the lip": the upper part of a real face, near the crest (both relative to the wave's size)
                    float H = Mathf.Max(1f, s.WaveHeight);
                    bool nearLip = s.BreakPhase >= 0.6f && s.BreakPhase < 2.3f
                                   && s.CrestDistance > -0.8f * H && s.CrestDistance < Mathf.Min(tuning.popWindowCrestDistance, 0.5f * s.FaceWidth + 1f)
                                   && s.FaceTop - s.Height < 0.55f * H;
                    return nearLip ? WaveZone.Lip : WaveZone.Face;
                default: return WaveZone.Flat;
            }
        }

        /// <summary>
        /// Plays the surface part of running manoeuvres and reads a new gesture on the face. Returns true when the
        /// gesture launched an air (the ride step must stop there).
        /// </summary>
        bool HandleGesture(float dt, Vector3 F, Vector3 n)
        {
            if (tricks.Busy)
            {
                Vector3 d = tricks.Tick(dt);
                yaw += d.x;
                surfaceRoll += d.z;
            }
            else surfaceRoll = Mathf.MoveTowards(surfaceRoll, 0f, 540f * dt);
            TickSurfaceTrick(dt);

            var g = In.ConsumeFlick();
            if (g.flick == Flick.None) return false;
            return PlayGesture(in g, F, n);
        }

        /// <summary>Resolves a gesture in the current zone and plays it.</summary>
        bool PlayGesture(in FlickResult g, Vector3 F, Vector3 n)
        {
            NoteGesture(g);
            float faceSpeed = FaceSpeed;
            if (!TrickCatalog.Resolve(g, Zone, faceSpeed, Grabbing, out TrickDef def, out float needed))
            {
                Event(needed > 0f && faceSpeed < needed
                    ? "trop lent — " + faceSpeed.ToString("0.0") + " / " + needed.ToString("0.0") + " m/s"
                    : "pas ici");
                return false;
            }
            switch (def.kind)
            {
                case TrickKind.Air:
                {
                    float zoneScale = Zone == WaveZone.Lip ? tuning.jumpLipScale : Zone == WaveZone.Tube ? tuning.jumpTubeScale
                                    : Zone == WaveZone.Face ? tuning.jumpFaceScale : tuning.jumpFlatScale;
                    vel += Vector3.up * JumpPop(0.7f, zoneScale * def.popScale, Mathf.Max(0f, vel.y)) + n * (0.8f * zoneScale);
                    LaunchAir(def.name);
                    jumpUsedThisAir = true;
                    tricks.Begin(def);
                    return true;
                }
                case TrickKind.Stall:
                    if (Zone == WaveZone.Flat) { Enter(RiderState.DuckDive); Event("Canard"); return true; }
                    relVel *= 0.82f;
                    Event(def.name);
                    return false;
                case TrickKind.Drive:
                    relVel += F * def.driveBoost;
                    BeginSurfaceTrick(def);
                    return false;
                case TrickKind.Settle:
                case TrickKind.Tuck:
                    return false;   // these only mean anything in the air
                default:
                    tricks.Begin(def);
                    if (def.driveBoost > 0f) relVel += F * def.driveBoost;
                    BeginSurfaceTrick(def);
                    return false;
            }
        }

        void NoteGesture(in FlickResult g)
        {
            LastGesture = g.flick;
            LastGestureQuarters = g.quarters;
            LastGestureTime = Time.time;
        }

        /// <summary>Arms a manoeuvre on the water; it scores when it finishes, and only if the rider is still up.</summary>
        void BeginSurfaceTrick(in TrickDef def)
        {
            surfaceTrick = def.name;
            surfacePoints = def.points;
            surfaceTimer = Mathf.Max(0.05f, def.duration);
            Event(def.name);
        }

        void TickSurfaceTrick(float dt)
        {
            if (surfaceTimer <= 0f) return;
            surfaceTimer -= dt;
            if (surfaceTimer > 0f) return;
            if (State == RiderState.Ride && surfacePoints > 0f)
            {
                TricksLanded++;
                OnTrick?.Invoke(surfaceTrick, surfacePoints);
            }
            surfaceTrick = ""; surfacePoints = 0f; surfaceTimer = 0f;
        }

        /// <summary>
        /// A gesture in the air: add a rotation, tuck it in, or spot the landing. Just after the lip threw the rider
        /// (coyote time) it still reads as a flick AT the lip: the pop and the whole lip vocabulary — so "crouch up the
        /// face, flick at the top" works even when the lip launched him a few frames before the thumb got there.
        /// </summary>
        void AirGesture()
        {
            var g = In.ConsumeFlick();
            if (g.flick == Flick.None) return;
            if (airWaveId >= 0 && !jumpUsedThisAir && AirTime < tuning.jumpCoyoteTime
                && TrickCatalog.Resolve(g, WaveZone.Lip, FaceSpeed, Grabbing, out TrickDef lipDef) && lipDef.kind == TrickKind.Air)
            {
                NoteGesture(g);
                vel.y += JumpPop(0.7f, tuning.jumpLipScale * lipDef.popScale, Mathf.Max(0f, vel.y));
                jumpUsedThisAir = true;
                tricks.Begin(lipDef);
                Event(lipDef.name);
                return;
            }
            PlayAirGesture(in g);
        }

        void PlayAirGesture(in FlickResult g)
        {
            NoteGesture(g);
            if (!TrickCatalog.Resolve(g, WaveZone.Air, 0f, Grabbing, out TrickDef def)) return;
            switch (def.kind)
            {
                case TrickKind.Settle:
                    tricks.StopTracks();
                    airSettling = true;
                    Event("Réception");
                    break;
                case TrickKind.Tuck:
                    tricks.Speed = tuning.airTuckSpeedup;
                    vel.y -= tuning.airTuckDrop;
                    airSettling = false;
                    Event(def.name);
                    break;
                default:
                    tricks.Begin(def);
                    airSettling = false;
                    Event(def.name);
                    break;
            }
        }
    }
}
