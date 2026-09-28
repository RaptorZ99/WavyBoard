using UnityEngine;

namespace WavyBoard.Tricks
{
    /// <summary>Where on the wave the rider is when a gesture lands. The same gesture reads as a different
    /// manoeuvre in each zone — that is what lets the whole wave be a playground instead of just the lip.</summary>
    public enum WaveZone
    {
        /// <summary>Paddling, or off the wave entirely.</summary>
        Flat,
        /// <summary>Low or mid face, away from the pocket: the place to draw a line and set up.</summary>
        Face,
        /// <summary>In the pocket, up near the curl: the launch pad.</summary>
        Lip,
        /// <summary>Inside the barrel.</summary>
        Tube,
        /// <summary>Airborne.</summary>
        Air,
    }

    public enum TrickKind
    {
        /// <summary>A turn drawn on the water: the board stays on the surface.</summary>
        Carve,
        /// <summary>A rotation played on the surface (spinner, flat roll).</summary>
        Surface,
        /// <summary>Leaves the water with a rotation programme.</summary>
        Air,
        /// <summary>Slows down and sets the rail; no rotation.</summary>
        Stall,
        /// <summary>Drives the board down the face for speed instead of turning it.</summary>
        Drive,
        /// <summary>Ends a rotation and squares the board up for the landing. Air only.</summary>
        Settle,
        /// <summary>Pulls the board in tight: whatever is already turning turns faster. Air only.</summary>
        Tuck,
    }

    /// <summary>One manoeuvre. Rotations are total degrees played over <see cref="duration"/>.</summary>
    public struct TrickDef
    {
        public string name;        // shown to the player (French)
        public TrickKind kind;
        public float yawDeg;       // about the surface normal / world up  (+ = toward the rider's right)
        public float pitchDeg;     // about the board's right axis         (+ = backflip)
        public float rollDeg;      // about the board's forward axis       (+ = el rollo direction)
        public float duration;     // seconds the rotation is played over
        public float points;       // base score
        public float minSpeed;     // m/s of face speed needed; below it the gesture does nothing
        public float popScale;     // Air only: multiplier on the launch impulse
        public float driveBoost;   // m/s handed straight to the board along its forward axis (Drive, and bottom turns)

        public bool HasRotation => yawDeg != 0f || pitchDeg != 0f || rollDeg != 0f;
    }

    /// <summary>
    /// The Skate "flick-it" vocabulary transposed to a bodyboard, one table, resolved by zone.
    ///
    /// Two axes carry the whole thing, and they are the two a skateboarder's thumb already knows:
    ///
    ///   <b>how far you swept the rim before flicking up</b> decides how much the board SPINS (flat, about your own
    ///   axis): nothing = none, a quarter = 180, a half = 360, three quarters = 540;
    ///
    ///   <b>flicking up and OUT to one side instead of straight up</b> throws the board over and makes it ROLL —
    ///   which, hit off the curl, is an El Rollo, the move the whole sport is built on.
    ///
    ///   A full lap of the rim is the big, deliberate one, and loading the crouch first turns it end over end.
    ///
    ///                      FLAT (line-up)      FACE (drawing a line)   LIP (in the pocket)   AIR (already up)
    ///   down, up ......... hop                 chop hop                AIR                   spot the landing
    ///   down, up + side .. hop roll            snap / top turn         EL ROLLO              add a roll -> ARS
    ///   down, quarter, up  hop 180             cutback                 air 180               add 180
    ///   down, half, up ... hop 360             spinner 360             air 360               add 360
    ///   down, 3/4, up .... hop 360             spinner 540             air 540               add 540
    ///   full lap ......... flat rollo          flat roll               ARS  (loaded: BACKFLIP)  add a roll
    ///   up, then down .... duck dive           nose drop (speed)       INVERT                tuck: rotate faster
    ///
    /// Holding the stick DOWN is not in the table: that is the crouch, and sitting on it is the stall, read
    /// straight off <see cref="FlickIt.Loaded"/>. Holding it OUT in the air is the grab, read off
    /// <see cref="FlickIt.Held"/>. Neither needs a gesture of its own, and neither can be confused with one.
    ///
    /// Scoops and laps are signed by the side of the rim the thumb travelled through, so forward and reverse spins
    /// are two genuinely different gestures the player feels in their thumb.
    /// </summary>
    public static class TrickCatalog
    {
        // ---------------------------------------------------------------- resolution

        /// <summary>
        /// Names the manoeuvre a gesture produces here. Returns false when the gesture means nothing in this zone
        /// (too slow, wrong place) — the rider then simply keeps riding.
        ///
        /// The speed asked for is FACE speed — your speed relative to the water, not the speed you are being
        /// carried at — and on a good ride that runs about 1.5 to 5.5 m/s. These used to ask 5 to 8, which is why
        /// almost every big manoeuvre was refused everywhere on the wave, silently, and the controls felt dead.
        /// </summary>
        public static bool Resolve(in FlickResult g, WaveZone zone, float faceSpeed, bool grabbing, out TrickDef def)
            => Resolve(g, zone, faceSpeed, grabbing, out def, out _);

        /// <summary>As above, and also reports the speed the manoeuvre wanted, so a refusal can say why.</summary>
        public static bool Resolve(in FlickResult g, WaveZone zone, float faceSpeed, bool grabbing,
                                   out TrickDef def, out float needed)
        {
            def = default;
            needed = 0f;
            switch (zone)
            {
                case WaveZone.Air: return ResolveAir(g, out def);
                case WaveZone.Lip: if (!ResolveLip(g, out def)) return false; break;
                case WaveZone.Tube: if (!ResolveTube(g, out def)) return false; break;
                case WaveZone.Face: if (!ResolveFace(g, out def)) return false; break;
                default: if (!ResolveFlat(g, out def)) return false; break;
            }
            needed = def.minSpeed;
            if (faceSpeed < def.minSpeed) return false;
            // amplitude: a loaded, snappy flick throws bigger and scores more
            float p = g.Power;
            def.popScale *= p;
            def.driveBoost *= p;
            def.points *= 0.75f + 0.5f * p;
            if (def.kind == TrickKind.Air) def.duration /= Mathf.Clamp(p, 0.7f, 1.4f);
            if (grabbing) { def.name = "Grab " + def.name; def.points *= 1.15f; }
            return true;
        }

        /// <summary>How much flat spin a sweep of the rim buys. This is the heart of the scheme: one gesture, and
        /// how far round you took it before flicking up is the whole difference between a 180 and a 540.</summary>
        public static float SpinOf(int quarters)
        {
            switch (Mathf.Clamp(quarters, 0, 4))
            {
                case 0:
                case 1: return 180f;
                case 2: return 360f;
                case 3: return 540f;
                default: return 720f;
            }
        }

        static bool IsScoop(Flick f) => f == Flick.ScoopRight || f == Flick.ScoopLeft;
        static bool IsCircle(Flick f) => f == Flick.CircleRight || f == Flick.CircleLeft;
        static bool IsSideFlick(Flick f) => f == Flick.UpRight || f == Flick.UpLeft;

        /// <summary>+1 to the rider's right, -1 to the left. Older callers that build a result by hand and leave
        /// <see cref="FlickResult.sign"/> at zero still get the side their gesture name implies.</summary>
        static float Side(in FlickResult g)
        {
            if (g.sign > 0f) return 1f;
            if (g.sign < 0f) return -1f;
            return g.flick == Flick.UpRight || g.flick == Flick.ScoopRight || g.flick == Flick.CircleRight ? 1f : -1f;
        }

        // ---------------------------------------------------------------- flat water, or paddling around
        // You can play anywhere. Popping off flat water gives a small hop, but every air gesture works out of it,
        // so messing about in the line-up is its own thing rather than dead time.

        static bool ResolveFlat(in FlickResult g, out TrickDef d)
        {
            d = default;
            float sign = Side(g);
            if (g.flick == Flick.Up)
            {
                d = new TrickDef { name = "Hop", kind = TrickKind.Air, duration = 0.4f, points = 25f, minSpeed = 0f, popScale = 0.75f };
                return true;
            }
            if (IsSideFlick(g.flick))
            {
                d = new TrickDef { name = "Hop roll", kind = TrickKind.Air, rollDeg = 360f * sign, duration = 0.7f, points = 60f, minSpeed = 0f, popScale = 0.8f };
                return true;
            }
            if (IsScoop(g.flick))
            {
                float spin = Mathf.Min(360f, SpinOf(g.quarters));
                d = new TrickDef { name = "Hop " + Mathf.RoundToInt(spin), kind = TrickKind.Air, yawDeg = spin * sign,
                                   duration = 0.6f + spin / 900f, points = 45f + spin * 0.08f, minSpeed = 0f, popScale = 0.85f };
                return true;
            }
            if (IsCircle(g.flick))
            {
                d = new TrickDef { name = "Rollo à plat", kind = TrickKind.Air, rollDeg = 360f * sign, duration = 0.75f, points = 90f, minSpeed = 0f, popScale = 0.9f };
                return true;
            }
            if (g.flick == Flick.Nollie)
            {
                d = new TrickDef { name = "Canard", kind = TrickKind.Stall, duration = 0.4f, points = 0f, minSpeed = 0f, popScale = 1f };
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- on the open face

        static bool ResolveFace(in FlickResult g, out TrickDef d)
        {
            d = default;
            float sign = Side(g);
            if (g.flick == Flick.Up)
            {
                d = new TrickDef { name = "Chop hop", kind = TrickKind.Air, duration = 0.5f, points = 40f, minSpeed = 1.4f, popScale = 0.55f };
                return true;
            }
            if (IsSideFlick(g.flick))
            {
                d = new TrickDef { name = "Snap", kind = TrickKind.Carve, yawDeg = 95f * sign, duration = 0.45f,
                                   points = 70f, minSpeed = 1.8f, popScale = 1f, driveBoost = 0.6f };
                return true;
            }
            if (IsScoop(g.flick))
            {
                if (g.quarters <= 1)
                {
                    // a quarter of the rim on the open face is the classic turn back toward the foam
                    d = new TrickDef { name = sign > 0f ? "Cutback" : "Cutback reverse", kind = TrickKind.Carve,
                                       yawDeg = 175f * sign, duration = 0.85f, points = 110f, minSpeed = 2.4f, popScale = 1f, driveBoost = 0.8f };
                    return true;
                }
                float spin = SpinOf(g.quarters);
                d = new TrickDef { name = (sign > 0f ? "Spinner " : "Spinner reverse ") + Mathf.RoundToInt(spin),
                                   kind = TrickKind.Surface, yawDeg = spin * sign, duration = 0.55f + spin / 700f,
                                   points = 90f + spin * 0.12f, minSpeed = 2.4f, popScale = 1f };
                return true;
            }
            if (IsCircle(g.flick))
            {
                d = new TrickDef { name = "Roll à plat", kind = TrickKind.Surface, rollDeg = 360f * sign, duration = 0.85f, points = 150f, minSpeed = 2.8f, popScale = 1f };
                return true;
            }
            if (g.flick == Flick.Nollie)
            {
                // weight forward, nose down the face: no rotation, just the drive a bottom turn is really for
                d = new TrickDef { name = "Bottom turn", kind = TrickKind.Drive, duration = 0.4f, points = 25f,
                                   minSpeed = 1.2f, popScale = 1f, driveBoost = 2.2f };
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- up in the pocket, at the curl

        static bool ResolveLip(in FlickResult g, out TrickDef d)
        {
            d = default;
            float sign = Side(g);
            if (g.flick == Flick.Up)
            {
                d = new TrickDef { name = "Air", kind = TrickKind.Air, duration = 0.6f, points = 140f, minSpeed = 2.0f, popScale = 1f };
                return true;
            }
            if (IsSideFlick(g.flick))
            {
                // thrown out sideways with the curl and rolled over by it: the move the sport is built on
                d = new TrickDef { name = "El Rollo", kind = TrickKind.Air, rollDeg = 360f * sign, duration = 0.8f,
                                   points = 260f, minSpeed = 2.6f, popScale = 1.05f };
                return true;
            }
            if (IsScoop(g.flick))
            {
                float spin = SpinOf(g.quarters);
                d = new TrickDef
                {
                    name = (sign > 0f ? "Air reverse " : "Air forward ") + Mathf.RoundToInt(spin),
                    kind = TrickKind.Air,
                    yawDeg = spin * sign,
                    duration = 0.55f + spin / 800f,
                    points = 120f + spin * 0.4f,
                    minSpeed = spin >= 540f ? 4.0f : spin >= 360f ? 3.0f : 2.2f,
                    popScale = 1f + spin / 3600f,
                };
                return true;
            }
            if (IsCircle(g.flick))
            {
                // loaded lap = you set your weight first and the board goes end over end: a backflip.
                // quick lap = the curl takes the board round with it while you spin inside it: the ARS.
                d = g.charge >= 0.5f
                    ? new TrickDef { name = "Backflip", kind = TrickKind.Air, pitchDeg = 360f, rollDeg = 60f * sign, duration = 1.05f, points = 380f, minSpeed = 4.2f, popScale = 1.25f }
                    : new TrickDef { name = "ARS", kind = TrickKind.Air, rollDeg = 360f * sign, yawDeg = 360f * sign, duration = 1.0f, points = 340f, minSpeed = 3.8f, popScale = 1.15f };
                return true;
            }
            if (g.flick == Flick.Nollie)
            {
                d = new TrickDef { name = "Invert", kind = TrickKind.Air, pitchDeg = 150f, rollDeg = 210f * sign, duration = 0.95f, points = 300f, minSpeed = 2.8f, popScale = 1.15f };
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- already in the air

        static bool ResolveAir(in FlickResult g, out TrickDef d)
        {
            d = default;
            float sign = Side(g);
            if (IsScoop(g.flick))
            {
                float spin = SpinOf(g.quarters);
                d = new TrickDef { name = "+" + Mathf.RoundToInt(spin), kind = TrickKind.Air, yawDeg = spin * sign,
                                   duration = 0.3f + spin / 1400f, points = 50f + spin * 0.18f, minSpeed = 0f, popScale = 0f };
                return true;
            }
            if (IsCircle(g.flick) || IsSideFlick(g.flick))
            {
                // a roll thrown mid-flight on top of a spin is exactly how an ARS is built
                d = new TrickDef { name = "+Rollo", kind = TrickKind.Air, rollDeg = 360f * sign, duration = 0.55f, points = 120f, minSpeed = 0f, popScale = 0f };
                return true;
            }
            if (g.flick == Flick.Nollie)
            {
                // tuck: pull it in tight and the rotation you already have runs faster
                d = new TrickDef { name = "Tuck", kind = TrickKind.Tuck, duration = 0.3f, points = 0f, minSpeed = 0f, popScale = 0f };
                return true;
            }
            if (g.flick == Flick.Up)
            {
                // spot the landing: stop turning and square the board up. The one gesture that makes big airs landable.
                d = new TrickDef { name = "", kind = TrickKind.Settle, duration = 0.25f, points = 0f, minSpeed = 0f, popScale = 0f };
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- deep in the barrel

        /// <summary>
        /// Deep in the barrel. This used to answer "no" to everything but a straight flick up, on the theory that
        /// the barrel is for holding your line — which is a fine theory and a miserable thing to play, because the
        /// one place on the wave where you most want to do something is the one place the controls went dead.
        ///
        /// The barrel now takes the whole lip vocabulary. It asks a little less speed, because you are in the
        /// slowest part of the wave, and pays more, because you are in the hardest.
        /// </summary>
        static bool ResolveTube(in FlickResult g, out TrickDef d)
        {
            if (g.flick == Flick.Up)
            {
                d = new TrickDef { name = "Sortie de tube", kind = TrickKind.Air, duration = 0.6f, points = 200f, minSpeed = 2.2f, popScale = 1f };
                return true;
            }
            if (g.flick == Flick.Nollie)
            {
                // drive the nose and get back out in front of the foam ball
                d = new TrickDef { name = "Relance", kind = TrickKind.Drive, duration = 0.35f, points = 30f, minSpeed = 0f, popScale = 1f, driveBoost = 1.8f };
                return true;
            }
            if (!ResolveLip(g, out d)) return false;
            d.minSpeed *= 0.75f;
            d.points *= 1.4f;
            return true;
        }

        // ---------------------------------------------------------------- naming a completed air

        /// <summary>
        /// Names an air from the rotation it actually ended up doing, so a trick assembled from several gestures
        /// mid-flight still gets a proper name (this is what turns "El Rollo then a scoop" into an ARS).
        /// </summary>
        public static string NameAir(float yawDeg, float pitchDeg, float rollDeg, bool grabbed)
        {
            float yaw = Mathf.Abs(yawDeg), pitch = Mathf.Abs(pitchDeg), roll = Mathf.Abs(rollDeg);
            int spin = Mathf.RoundToInt(yaw / 180f) * 180;
            bool reverse = yawDeg > 0f;
            string name;

            if (roll >= 270f && spin >= 360) name = "ARS " + spin;
            else if (roll >= 270f) name = "El Rollo";
            else if (pitch >= 300f) name = pitchDeg > 0f ? "Backflip" : "Frontflip";
            else if (pitch >= 110f) name = "Invert";
            else if (spin >= 180) name = (reverse ? "Air reverse " : "Air forward ") + spin;
            else name = "Air";

            return grabbed ? "Grab " + name : name;
        }
    }
}
