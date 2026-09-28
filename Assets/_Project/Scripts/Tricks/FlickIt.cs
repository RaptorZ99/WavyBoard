using UnityEngine;

namespace Biscotte.Tricks
{
    /// <summary>What the right stick just did with the board.</summary>
    public enum Flick
    {
        None,
        /// <summary>Crouched, then flicked straight up: the pop.</summary>
        Up,
        /// <summary>Crouched, then flicked up and out to the right: the board is thrown out that way and rolls.</summary>
        UpRight,
        /// <summary>Flicked up and out to the left.</summary>
        UpLeft,
        /// <summary>Swept round the right of the rim on the way up: the board spins under you. How far it was swept
        /// is in <see cref="FlickResult.quarters"/> — a quarter turn is a 180, a half turn a 360, and so on.</summary>
        ScoopRight,
        /// <summary>Swept round the left of the rim on the way up.</summary>
        ScoopLeft,
        /// <summary>A full lap of the rim, clockwise: the big one.</summary>
        CircleRight,
        /// <summary>A full lap the other way.</summary>
        CircleLeft,
        /// <summary>Pushed up, then snapped down: the nollie. Weight forward, board thrown nose first.
        /// There is deliberately no gesture for pulling the stick DOWN from the middle — that is the crouch, and
        /// holding it there is the stall. A player can only mean one thing by it.</summary>
        Nollie,
    }

    /// <summary>One recognised gesture.</summary>
    public struct FlickResult
    {
        public Flick flick;
        /// <summary>0..1, how deep and how long the stick was loaded before the flick. This is the pop height.</summary>
        public float charge;
        /// <summary>0..1, how hard the flick was.</summary>
        public float snap;
        /// <summary>Signed sweep traced on the rim before firing (deg, + = clockwise).</summary>
        public float arcDeg;
        /// <summary>Sweep quantised to quarter turns, 0..4. This is what picks 180 / 360 / 540 out of one gesture.</summary>
        public int quarters;
        /// <summary>+1 = the manoeuvre goes to the rider's right, -1 to the left. Set from the side of the rim the
        /// thumb actually travelled through, which is what a player feels — not from the sign of the swept angle.</summary>
        public float sign;

        /// <summary>How big to play it. A loaded, snappy flick is the big one.</summary>
        public float Power => Mathf.Clamp(0.6f + 0.7f * charge + 0.3f * snap, 0.6f, 1.55f);
    }

    /// <summary>
    /// The Skate "flick-it" recogniser, on the right stick. Pure logic — feed it a stick and a clock, so tests and
    /// the autopilot can drive synthetic paths through it.
    ///
    /// The rule that matters, and the one this gets right: <b>the pop fires the instant the stick crosses upward</b>.
    /// Not when it settles, not when you let go. Pull down to crouch (the longer you hold, the higher you go), then
    /// snap up — and you are already in the air. Everything else is flavour decided at that same instant:
    ///
    ///     centre, down, straight up ................. plain pop
    ///     centre, down, up and out to one side ...... the board is thrown out and rolls that way
    ///     centre, down, round the rim, then up ...... the board spins under you
    ///     ... and keep winding the rim .............. it keeps spinning: another half turn of thumb is another
    ///                                                 half turn of board, for as long as you keep going
    ///     round and round without coming up ......... a lap: the big rotation
    ///     pushed up, then snapped down .............. nose first, the nollie
    ///     parked out on the rim in the air .......... a grab (see <see cref="Held"/>)
    ///
    /// Three details earn their keep:
    ///
    /// <b>A pop must be loaded.</b> The stick has to have been at the bottom before it can fire going up — which is
    /// the rule every skater already has in their thumb, and the one thing that makes pushing the stick UP mean
    /// something of its own (the nollie's wind-up) instead of being a second, weaker ollie.
    ///
    /// <b>The up-trigger moves.</b> A straight flick has to cross well above the middle to count, or every twitch
    /// would pop. A gesture that is already swept round the rim only has to cross the horizontal, because out there
    /// the stick is a long way from the middle already and "crossing upward" is the whole flick.
    ///
    /// <b>Rotation is wound, not quantised.</b> Starting at the bottom and arriving at the top is always about a
    /// half lap of thumb however hard you try to make it a quarter or three quarters, so reading the exact fraction
    /// back out would be a lie. Instead the first pass fires the spin and every further <see cref="windArc"/>
    /// degrees of continued winding fires another one, the same way round. 180, 360, 540, 900 all come out of one
    /// continuous motion that the player is steering while it happens, and none of them costs a frame of latency.
    ///
    /// Your own rotation is NOT in here: in Skate the left stick spins the body while you are in the air, and it
    /// works the same way in this game. The right stick is the board, the left stick is you.
    /// </summary>
    public class FlickIt
    {
        [Header("Geometry (stick units)")]
        public float deadzone = 0.25f;      // inside this the stick is centred and the gesture resets
        public float upTrigger = 0.45f;     // a STRAIGHT flick has to cross this on the way up to fire
        public float sweptTrigger = 0f;     // ... a swept one only has to cross this (the horizontal)
        public float downTrigger = 0.45f;   // crossing this on the way down fires the nose-first flick
        public float loadY = -0.4f;         // held below this = crouching
        public float topY = 0.4f;           // held above this = the nollie's wind-up
        public float rim = 0.55f;           // out here the swept angle is meaningful
        public float sideX = 0.42f;         // the flick ended far enough out to one side to count as a roll

        [Header("Speed (stick units / s)")]
        public float flickSpeed = 2.6f;     // below this it is a push, not a flick
        public float snapSpeedRef = 14f;    // flick speed that scores snap = 1

        [Header("Classification (deg of swept angle)")]
        public float scoopArc = 45f;        // swept round the rim on the way up = a scoop
        public float lapArc = 250f;         // wound this far round without ever turning up = a lap
        public float windArc = 150f;        // further winding after a gesture, per extra half turn of board

        [Header("Timing (s)")]
        public float chargeTime = 0.40f;    // holding down this long is a full charge
        public float debounce = 0.12f;      // minimum gap between two gestures
        public float holdTime = 0.12f;      // parked out on the rim this long reads as a hold (the grab)

        // ---- state
        Vector2 prev;
        float prevT;
        bool hasPrev, fired, visitedBottom, visitedTop;
        float arc, sideAcc, chargeAcc, downSince = -1f, lastFireT = -99f, peakSpeed;
        float outSince = -1f;
        float totalArc;     // everything swept since the stick left the centre, for the record and the HUD
        float lastSign = 1f;// which way the last gesture went, so continued winding keeps spinning the same way

        /// <summary>0..1 — the stick is pulled down and held: the crouch. Doubles as the stall input on the water.</summary>
        public float Loaded { get; private set; }
        /// <summary>The stick is out of the deadzone right now.</summary>
        public bool Active { get; private set; }
        /// <summary>The stick is moving fast enough to be a flick. Below this it is just a push, and the camera
        /// can safely have it.</summary>
        public bool Fast { get; private set; }
        /// <summary>The stick has been parked out on the rim, still, for <see cref="holdTime"/>: in the air that is
        /// a grab, and which way it points chooses which one.</summary>
        public bool Held { get; private set; }
        /// <summary>Direction the held stick points, normalised. Meaningless while <see cref="Held"/> is false.</summary>
        public Vector2 HoldDir { get; private set; }
        /// <summary>Signed sweep since the last gesture fired (deg).</summary>
        public float Arc => arc;
        /// <summary>Signed sweep since the stick last left the centre (deg): the whole wind, for the HUD.</summary>
        public float TotalArc => totalArc;

        public void Reset()
        {
            hasPrev = false; fired = false; visitedBottom = false; visitedTop = false;
            arc = 0f; totalArc = 0f; sideAcc = 0f; chargeAcc = 0f; downSince = -1f; peakSpeed = 0f; outSince = -1f;
            lastSign = 1f;
            Loaded = 0f; Active = false; Fast = false; Held = false; HoldDir = Vector2.zero;
        }

        /// <summary>Feeds one stick sample. Returns a gesture the moment one fires.</summary>
        public FlickResult Feed(Vector2 stick, float now)
        {
            var none = new FlickResult { flick = Flick.None };
            float mag = stick.magnitude;
            Active = mag >= deadzone;

            if (!hasPrev) { prev = stick; prevT = now; hasPrev = true; return none; }
            float dt = Mathf.Max(1e-4f, now - prevT);
            Vector2 d = stick - prev;
            float speed = d.magnitude / dt;
            float vy = d.y / dt;
            peakSpeed = Mathf.Max(peakSpeed, speed);
            Fast = speed >= flickSpeed * 0.6f;

            // Whether the stick had ALREADY been at the bottom when this frame started. The frame that snaps past
            // the down trigger is itself below the crouch line, so asking after the update would make the
            // nose-first flick impossible: it would always look like the end of a crouch.
            bool hadBottom = visitedBottom;

            // crouch: how long the stick has been held down, capped. Pushing it up instead winds up a nollie.
            if (stick.y < loadY)
            {
                if (downSince < 0f) downSince = now;
                chargeAcc = Mathf.Clamp01((now - downSince) / Mathf.Max(0.05f, chargeTime));
                visitedBottom = true;
            }
            if (stick.y > topY) visitedTop = true;
            Loaded = stick.y < loadY ? Mathf.Max(0.3f, chargeAcc) : 0f;

            // held out on the rim and barely moving: a grab in the air, and nothing at all on the water
            if (mag > rim && speed < flickSpeed * 0.35f)
            {
                if (outSince < 0f) outSince = now;
                Held = now - outSince >= holdTime;
                if (Held) HoldDir = stick / Mathf.Max(1e-4f, mag);
            }
            else { outSince = -1f; Held = false; }

            // sweep traced out on the rim, so a scoop can be told from a straight flick, and which side of the rim
            // the thumb travelled through, which is what decides left from right
            if (mag > rim && prev.magnitude > rim)
            {
                float a0 = Mathf.Atan2(prev.y, prev.x) * Mathf.Rad2Deg;
                float a1 = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
                float step = -Mathf.DeltaAngle(a0, a1);   // negated so + reads as clockwise to the player
                arc += step;
                totalArc += step;
                sideAcc += stick.x;
            }

            // Back at the centre, and ACTUALLY at rest there: the gesture is over, ready for the next one. Speed is
            // the whole test — a flick from the bottom to the top passes clean through the middle at forty stick-
            // widths a second, and forgetting the crouch on that frame would make the ollie impossible.
            if (mag < deadzone && speed < flickSpeed)
            {
                prev = stick; prevT = now;
                fired = false; visitedBottom = false; visitedTop = false;
                arc = 0f; totalArc = 0f; sideAcc = 0f; chargeAcc = 0f; downSince = -1f; peakSpeed = 0f; outSince = -1f;
                Held = false;
                return none;
            }

            FlickResult res = none;
            bool armed = now - lastFireT > debounce;

            if (armed && !fired)
            {
                bool swept = visitedBottom && Mathf.Abs(arc) >= scoopArc;

                // wound right round without ever turning upward: a lap, the deliberate big one
                if (Mathf.Abs(arc) >= lapArc && peakSpeed >= flickSpeed)
                    res = Fire(arc > 0f ? Flick.CircleRight : Flick.CircleLeft, arc > 0f ? 1f : -1f, now);

                // THE POP: the frame the stick crosses upward past its trigger, moving up fast, having been loaded
                // at the bottom first. A straight flick has to reach well above the middle; one already swept out
                // on the rim only has to cross the horizontal, because out there it is a long way from the middle
                // already and turning upward IS the flick.
                else if (visitedBottom && CrossedUp(stick, swept ? sweptTrigger : upTrigger) && vy >= flickSpeed)
                {
                    float side = sideAcc >= 0f ? 1f : -1f;
                    Flick f = swept ? (side > 0f ? Flick.ScoopRight : Flick.ScoopLeft)
                            : Mathf.Abs(stick.x) >= sideX ? (stick.x > 0f ? Flick.UpRight : Flick.UpLeft)
                            : Flick.Up;
                    if (!swept) side = stick.x >= 0f ? 1f : -1f;
                    res = Fire(f, side, now);
                }

                // Nose first. Three things have to be true, and each one rules out something the player meant
                // differently: the stick was wound up at the TOP (or pulling down would just be the crouch), it had
                // not already been at the bottom (same reason), and it went straight (the descending half of a lap
                // of the rim passes the same trigger at the same speed, and is told apart by the arc it swept on
                // the way — exactly as a scoop is told apart from a plain pop).
                else if (visitedTop && !hadBottom && Mathf.Abs(arc) < scoopArc
                         && prev.y >= -downTrigger && stick.y < -downTrigger && vy <= -flickSpeed)
                    res = Fire(Flick.Nollie, stick.x >= 0f ? 1f : -1f, now);
            }
            // KEEP WINDING: the thumb never came home, it just carried on round the rim. Every further half turn is
            // another half turn of board, the same way round as the one before. This is where 360s, 540s and the
            // silly ones come from, and the player is steering it as it happens instead of having pre-declared it.
            else if (armed && fired && Mathf.Abs(arc) >= windArc && mag > rim)
                res = Fire(lastSign > 0f ? Flick.ScoopRight : Flick.ScoopLeft, lastSign, now, quartersOverride: 1);

            prev = stick; prevT = now;
            return res;
        }

        bool CrossedUp(Vector2 stick, float trigger) => prev.y <= trigger && stick.y > trigger;

        /// <summary>
        /// Fires a gesture and re-arms the winding counter, so continuing round the rim can fire again.
        /// A gesture fired BY that continued winding always carries one half turn (quartersOverride = 1): the
        /// rotation adds up by firing repeatedly, not by each firing being bigger than the last.
        /// </summary>
        FlickResult Fire(Flick f, float side, float now, int quartersOverride = -1)
        {
            var r = new FlickResult
            {
                flick = f,
                charge = chargeAcc,
                snap = Mathf.Clamp01((peakSpeed - flickSpeed) / Mathf.Max(1f, snapSpeedRef - flickSpeed)),
                arcDeg = totalArc,
                quarters = quartersOverride >= 0 ? quartersOverride
                         : Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(totalArc) / 90f), 0, 4),
                sign = side,
            };
            fired = true;
            lastFireT = now;
            lastSign = side;
            arc = 0f;          // the wind starts again from here
            return r;
        }
    }
}
