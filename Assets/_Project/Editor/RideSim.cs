using System;
using System.Collections.Generic;
using System.Text;
using WavyBoard.Debugging;
using WavyBoard.InputSys;
using WavyBoard.Ocean;
using WavyBoard.Rider;
using WavyBoard.Wave;
using UnityEditor;
using UnityEngine;

namespace WavyBoard.EditorTools
{
    /// <summary>
    /// Rides a whole wave in the Editor, without Play mode and without touching the open scene: a hidden spot, sea,
    /// wave and rider, stepped at the physics rate with a <see cref="RiderBot"/> on the controls. Deterministic and far
    /// faster than real time (a 40 s ride takes a fraction of a second), for the gameplay tests and for tuning:
    ///
    ///   unity command eval --code 'return WavyBoard.EditorTools.RideSim.Trace(1f, "Pocket", 40f);'
    ///
    /// The rider waits at the line-up facing the beach, the wave (aimed at him, as the game's scheduler does) comes in
    /// and the bot paddles it, exactly as a player would.
    /// </summary>
    public sealed class RideSim : IDisposable
    {
        public const float Dt = 0.02f;
        const string kTuning = "Assets/_Project/Data/RiderTuning.asset";
        const string kBoard = "Assets/_Project/Data/Board_Standard.asset";

        public readonly RiderController Rider;
        public readonly RiderBot Bot = new RiderBot();
        public readonly SurfWave Wave;
        public readonly SurfSpotConfig Spot;
        readonly WaterSurfaceComposite water;
        readonly List<GameObject> objects = new List<GameObject>();

        /// <summary>Simulated time (the wave's clock).</summary>
        public double Time { get; private set; }
        public float Elapsed => (float)(Time - Wave.SpawnTime);

        // ---- what happened
        public readonly List<string> Events = new List<string>();
        public int Launches, Landings, CleanLandings, Wipeouts, TubeRides, TubeExits, Pumps, DirectionChanges;
        public float RideSeconds, TubeSeconds, LongestTube, MaxAirPeak, DeepestInTube, FarthestAhead = float.MinValue;
        public string WipeoutReason = "";
        public bool Took;
        readonly StringBuilder trace = new StringBuilder();
        float tubeRun, sideTimer, side = 1f;
        bool wasInTube;

        public RideSim(float heightScale, RiderBot.Plan plan)
        {
            Spot = Make<SurfSpotConfig>("RideSim Spot");
            water = Make<WaterSurfaceComposite>("RideSim Water");
            var wgo = new GameObject("RideSim Wave", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(wgo);
            Wave = wgo.AddComponent<SurfWave>();
            Rider = Make<RiderController>("RideSim Rider");
            Rider.tuning = AssetDatabase.LoadAssetAtPath<RiderTuning>(kTuning);
            Rider.board = AssetDatabase.LoadAssetAtPath<BoardSpec>(kBoard);
            Rider.spot = Spot;
            if (Rider.tuning == null || Rider.board == null) throw new InvalidOperationException("rider tuning or board asset missing");
            Rider.Input = Bot;
            Rider.WaterSource = water;
            Bot.plan = plan;

            Vector3 lineup = Spot.LineupPosition();
            Wave.Spawn(Spot, heightScale, 1, lineup);
            water.Register(Wave);
            Time = Wave.SpawnTime;
            Rider.PlaceAt(lineup, Spot.travelYawDeg, Time);
            Rider.OnEvent += OnEvent;
            Rider.OnLanded += (impact, clean) => { Landings++; if (clean) CleanLandings++; MaxAirPeak = Mathf.Max(MaxAirPeak, Rider.AirPeak); };
        }

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(go);
            return go.AddComponent<T>();
        }

        void OnEvent(string e)
        {
            if (e == "Pump") { Pumps++; return; }
            if (e == "Riding") Took = true;
            if (e == "Envol" || e == "Air" || e == "Chop hop" || e == "Hop") Launches++;
            if (e.StartsWith("Wipeout")) { Wipeouts++; WipeoutReason = e; }
            if (Events.Count < 200) Events.Add(Elapsed.ToString("0.00") + " " + e);
        }

        /// <summary>Steps the rider (and the bot) for up to <paramref name="seconds"/>, or until <paramref name="until"/> holds.</summary>
        public void Run(float seconds, Func<RideSim, bool> until = null, float traceEvery = 0f)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            float nextTrace = Elapsed;
            for (int i = 0; i < steps; i++)
            {
                Bot.Think(Rider, (float)Time);
                Time += Dt;
                Rider.Step(Dt, Time);
                Record();
                if (traceEvery > 0f && Elapsed >= nextTrace) { TraceLine(); nextTrace = Elapsed + traceEvery; }
                if (until != null && until(this)) return;
            }
        }

        /// <summary>Runs until the rider is up and riding (at most <paramref name="timeout"/> s). False if he never got up.</summary>
        public bool RunUntilRiding(float timeout = 40f)
        {
            Run(timeout, sim => sim.Rider.State == RiderState.Ride && sim.Rider.RideTime > 0.7f);
            return Rider.State == RiderState.Ride;
        }

        public bool Riding => Rider.State == RiderState.Ride || Rider.State == RiderState.Air;

        void Record()
        {
            var s = Rider.Sample;
            if (Rider.State == RiderState.Ride) RideSeconds += Dt;
            bool inTube = Rider.State == RiderState.Ride && Rider.InTube;
            if (inTube)
            {
                TubeSeconds += Dt; tubeRun += Dt;
                LongestTube = Mathf.Max(LongestTube, tubeRun);
                DeepestInTube = Mathf.Min(DeepestInTube, s.PeelDistance);
                if (!wasInTube) TubeRides++;
            }
            else
            {
                // out of the tube and still riding: a tube made
                if (wasInTube && Rider.State == RiderState.Ride) TubeExits++;
                tubeRun = 0f;
            }
            wasInTube = inTube;
            if (Rider.State == RiderState.Ride) FarthestAhead = Mathf.Max(FarthestAhead, s.PeelDistance);

            // riding the other way along the line for a moment (as the camera reads it): a cutback, a rebound
            float vT = Vector3.Dot(Rider.Velocity, (Vector3)s.CrestDir);
            if (Rider.State == RiderState.Ride && Mathf.Abs(vT) > 1.2f && Mathf.Sign(vT) != side)
            {
                sideTimer += Dt;
                if (sideTimer > 0.25f) { side = Mathf.Sign(vT); sideTimer = 0f; DirectionChanges++; }
            }
            else sideTimer = 0f;
        }

        void TraceLine()
        {
            var s = Rider.Sample;
            trace.AppendFormat("{0,6:0.00} {1,-8} {2,-17} d={3,6:0.0} cd={4,5:0.0}/{5,4:0.0} ph={6:0.00} tube={7} line={8,5:0.0} pace={9,5:0.0} vp={10,4:0.0} pow={11:0.00} vT={12,5:0.0} y={13:0.0} H={14:0.0}\n",
                Elapsed, Rider.State, Bot.Intent, s.PeelDistance, s.CrestDistance, s.FaceWidth, s.BreakPhase, Rider.InTube ? "Y" : "-",
                Rider.LineSpeed, Rider.PaceTarget, s.PeelSpeed, Rider.Power, Vector3.Dot(Rider.Velocity, (Vector3)s.CrestDir),
                Rider.Position.y - s.SeaLevel, s.WaveHeight);
        }

        public string Summary()
            => $"took {Took} ride {RideSeconds:0.0}s | turns round {DirectionChanges} | tube rides {TubeRides} exits {TubeExits} longest {LongestTube:0.0}s total {TubeSeconds:0.0}s deepest {DeepestInTube:0.0} m | farthest ahead {FarthestAhead:0.0} m | launches {Launches} landings {Landings} (clean {CleanLandings}) peak {MaxAirPeak:0.0} m | pumps {Pumps} | wipeouts {Wipeouts} {WipeoutReason} | final {Rider.State}";

        public string TraceText => trace.ToString();

        /// <summary>Takes some of the controls from the bot (the rest stay its own): see <see cref="Hands"/>.</summary>
        public Hands TakeOver()
        {
            var h = new Hands(Bot, Rider);
            Rider.Input = h;
            return h;
        }

        /// <summary>The bot's hands with some controls taken over by a test or a tuning script: the trim forced, the
        /// steering scripted, the stall held, the sprint button mashed.</summary>
        public sealed class Hands : IRiderInput
        {
            readonly RiderBot bot;
            readonly RiderController rider;
            public float? trim;
            public Func<RiderController, float> steer;
            public bool stall, mashSprint;
            int frame;
            public Hands(RiderBot bot, RiderController rider) { this.bot = bot; this.rider = rider; }
            public Vector2 Move => new Vector2(steer != null ? steer(rider) : bot.Move.x, trim ?? bot.Move.y);
            public bool SprintHeld => mashSprint ? (frame++ / 8) % 2 == 0 : bot.SprintHeld;
            public bool StallHeld => stall || bot.StallHeld;
            public bool Crouched => bot.Crouched;
            public float CrouchCharge => bot.CrouchCharge;
            public bool StickHeld => bot.StickHeld;
            public bool ConsumePump() => bot.ConsumePump();
            public bool ConsumeDuck() => bot.ConsumeDuck();
            public bool ConsumeStance() => bot.ConsumeStance();
            public bool ConsumeReset() => bot.ConsumeReset();
            public bool ConsumeJump(out float charge) => bot.ConsumeJump(out charge);
            public Tricks.FlickResult ConsumeFlick() => bot.ConsumeFlick();
            public void Rumble(float low, float high, float duration) { }
        }

        /// <summary>Steering that points the board straight up the face (over the top of the wave).</summary>
        public static float SteerOverTheTop(RiderController r)
            => Mathf.Clamp(Vector3.SignedAngle(r.HeadingDir, -(Vector3)r.Sample.TravelDir, Vector3.up) / 22f, -1f, 1f);

        public void Dispose()
        {
            Rider.OnEvent -= OnEvent;
            Wave.ReleaseNow();
            water.ReleaseBuffers();
            foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            objects.Clear();
        }

        /// <summary>One ride with a plan at a wave size, traced every <paramref name="every"/> s (tuning, through eval).</summary>
        public static string Trace(float heightScale, string plan, float seconds = 40f, float every = 0.25f, bool pumps = true)
        {
            using (var sim = new RideSim(heightScale, (RiderBot.Plan)Enum.Parse(typeof(RiderBot.Plan), plan)))
            {
                sim.Bot.pumps = pumps;
                sim.RunUntilRiding();
                sim.Run(seconds, s => !s.Riding, every);
                return sim.Summary() + "\n" + string.Join(" | ", sim.Events) + "\n" + sim.TraceText;
            }
        }
    }
}
