using NUnit.Framework;
using WavyBoard.Debugging;
using WavyBoard.EditorTools;
using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.Tests
{
    /// <summary>
    /// Whole rides, simulated in the Editor (<see cref="RideSim"/>: hidden objects, the open scene is never touched) on
    /// the three wave sizes the game deals (0.68, 1 and 1.45 x 3.4 m), with a bot at the player's controls. Each test is
    /// a promise the game makes to the player.
    /// </summary>
    public class RideTests
    {
        static readonly float[] Sizes = { 0.68f, 1f, 1.45f };

        static RideSim Up(float size, RiderBot.Plan plan)
        {
            var sim = new RideSim(size, plan);
            Assert.IsTrue(sim.RunUntilRiding(), $"never got up on a x{size} wave: {sim.Summary()}");
            return sim;
        }

        static void AssertNoFall(RideSim sim, string what) => Assert.AreEqual(0, sim.Wipeouts, $"{what}: {sim.Summary()}\n{string.Join(" | ", sim.Events)}");

        /// <summary>Paddles into the wave when it comes, stands up and rides it; letting go of the sprint at the take-off
        /// (what everybody does) does not end the ride.</summary>
        [Test]
        public void CatchesTheWaveAndRidesIt([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(10f);
                AssertNoFall(sim, "the first seconds of a ride");
                Assert.AreEqual(RiderState.Ride, sim.Rider.State, sim.Summary());
            }
        }

        /// <summary>The bug players hit: deep in the barrel, leaning toward the exit, the curl sucked them back until it
        /// closed on them. Now the wave's power holds a trimming rider at the curl's pace and more: he comes out.</summary>
        [Test]
        public void RacesOutOfADeepTube([ValueSource(nameof(Sizes))] float size, [Values(false, true)] bool pumping)
        {
            using (var sim = Up(size, RiderBot.Plan.Stall))
            {
                float H = sim.Wave.Params.height;
                sim.Run(15f, x => !x.Riding || (x.Rider.InTube && x.Rider.Sample.PeelDistance < -1.8f * H));
                Assert.IsTrue(sim.Rider.InTube, $"the stall never took him deep in the tube: {sim.Summary()}");

                sim.Bot.plan = RiderBot.Plan.Exit;
                sim.Bot.pumps = pumping;
                float t0 = sim.Elapsed;
                sim.Run(10f, x => !x.Riding || (!x.Rider.InTube && x.Rider.Sample.PeelDistance > 0.3f * H));
                AssertNoFall(sim, "racing out of the tube");
                Assert.IsTrue(sim.Rider.Sample.PeelDistance > 0.3f * H && !sim.Rider.InTube,
                    $"still behind the curl after {sim.Elapsed - t0:0.0} s: {sim.Summary()}");
                Assert.Less(sim.Elapsed - t0, pumping ? 6f : 8f, "out, but it took too long");
            }
        }

        /// <summary>Pumping in rhythm gets a rider out of the tube sooner than trimming alone.</summary>
        [Test]
        public void PumpingBeatsTrimming()
        {
            float Exit(bool pumping)
            {
                using (var sim = Up(1f, RiderBot.Plan.Stall))
                {
                    float H = sim.Wave.Params.height;
                    sim.Run(15f, x => x.Rider.InTube && x.Rider.Sample.PeelDistance < -1.8f * H);
                    sim.Bot.plan = RiderBot.Plan.Exit;
                    sim.Bot.pumps = pumping;
                    float t0 = sim.Elapsed;
                    sim.Run(10f, x => !x.Riding || (!x.Rider.InTube && x.Rider.Sample.PeelDistance > 0.3f * H));
                    return sim.Elapsed - t0;
                }
            }
            float trimmed = Exit(false), pumped = Exit(true);
            Assert.Less(pumped, trimmed * 0.85f, $"pumping {pumped:0.0} s vs trimming {trimmed:0.0} s");
        }

        /// <summary>The stall lets the curl come over: from in front of it, holding it gets the rider tubed.</summary>
        [Test]
        public void StallingGetsBarreled([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(4f);
                Assert.Greater(sim.Rider.Sample.PeelDistance, 0f, $"should be in front of the curl before stalling: {sim.Summary()}");
                sim.Bot.plan = RiderBot.Plan.Stall;
                sim.Run(8f, x => !x.Riding || x.Rider.InTube);
                AssertNoFall(sim, "stalling into the tube");
                Assert.IsTrue(sim.Rider.InTube, $"the curl never came over: {sim.Summary()}");
            }
        }

        /// <summary>The trim is the rider's pace against the curl: leaning on gains on it, sitting back lets it come.</summary>
        [Test]
        public void TheStickSetsThePace([ValueSource(nameof(Sizes))] float size)
        {
            float Ahead(float trim)
            {
                using (var sim = Up(size, RiderBot.Plan.Cruise))
                {
                    sim.Run(3f);
                    sim.TakeOver().trim = trim;
                    float d0 = sim.Rider.Sample.PeelDistance;
                    sim.Run(4f);
                    AssertNoFall(sim, $"trimming at {trim}");
                    return sim.Rider.Sample.PeelDistance - d0;
                }
            }
            float back = Ahead(-1f), centred = Ahead(0f), forward = Ahead(1f);
            Assert.Less(back, centred - 1f, $"sitting back {back:0.0} m vs centred {centred:0.0} m");
            Assert.Greater(forward, centred + 1f, $"leaning on {forward:0.0} m vs centred {centred:0.0} m");
        }

        /// <summary>Hands off the sticks the board holds its line: it neither sinks to the flat nor climbs over the lip,
        /// nor falls behind into the curl.</summary>
        [Test]
        public void HandsOffTheBoardHoldsItsLine([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(8f, x => x.Bot.Intent == "hands off");
                Assert.AreEqual("hands off", sim.Bot.Intent, sim.Summary());
                sim.Run(0.5f);
                float H = sim.Wave.Params.height;
                float y0 = sim.Rider.Position.y - sim.Rider.Sample.SeaLevel, lo = y0, hi = y0;
                sim.Run(10f, x =>
                {
                    float y = x.Rider.Position.y - x.Rider.Sample.SeaLevel;
                    lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    return !x.Riding;
                });
                AssertNoFall(sim, "hands off");
                Assert.AreEqual(RiderState.Ride, sim.Rider.State, sim.Summary());
                Assert.Less(hi - lo, 0.35f * H, $"the line wandered from {lo:0.0} to {hi:0.0} m on a {H:0.0} m face");
                Assert.Greater(sim.Rider.Sample.PeelDistance, -1f, "fell behind into the curl");
            }
        }

        /// <summary>Up the face fast, crouched: off the lip, a trick, and back down on the face of the wave.</summary>
        [Test]
        public void FliesOffTheLipAndLandsOnTheFace([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Airs))
            {
                sim.Run(25f, x => !x.Riding);
                AssertNoFall(sim, "airs");
                int lipLaunches = sim.Events.FindAll(e => e.EndsWith(" Envol")).Count;
                Assert.GreaterOrEqual(lipLaunches, 2, sim.Summary());
                Assert.GreaterOrEqual(sim.CleanLandings, 2, sim.Summary());
                Assert.Greater(sim.MaxAirPeak, 1f, sim.Summary());
            }
        }

        /// <summary>A tube rider goes in and comes out again and again, and is never closed out.</summary>
        [Test]
        public void TubeRidesComeOut([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Pocket))
            {
                sim.Run(30f, x => !x.Riding);
                AssertNoFall(sim, "tube riding");
                Assert.GreaterOrEqual(sim.TubeRides, 2, sim.Summary());
                Assert.GreaterOrEqual(sim.TubeExits, 2, sim.Summary());
            }
        }

        /// <summary>Cutbacks and rebounds: riding back toward the curl and round again, without getting caught.</summary>
        [Test]
        public void CarvesBackAndForth([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Carve))
            {
                sim.Run(30f, x => !x.Riding);
                AssertNoFall(sim, "carving");
                Assert.GreaterOrEqual(sim.DirectionChanges, 2, sim.Summary());
            }
        }

        /// <summary>The way out of a wave (there is no kick-out button): race out onto the shoulder, sit back and steer
        /// up over its top — over the back, or flying off it — and the ride is over, cleanly.</summary>
        [Test]
        public void LeavesTheWaveOverItsShoulder([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(4f);
                var hands = sim.TakeOver();
                hands.trim = 1f;
                sim.Run(12f, x => x.Rider.Sample.PeelDistance > 2.2f * x.Wave.Params.height);
                Assert.Greater(sim.Rider.Sample.PeelDistance, 2.2f * sim.Wave.Params.height, $"never got out on the shoulder: {sim.Summary()}");
                hands.trim = null;
                hands.steer = RideSim.SteerOverTheTop;
                hands.stall = true;
                sim.Run(8f, x => !x.Riding);
                AssertNoFall(sim, "kicking out");
                Assert.IsFalse(sim.Riding, $"still on the wave: {sim.Summary()}\n{string.Join(" | ", sim.Events)}");
            }
        }

        /// <summary>Stall + steer pivots the board round: a U-turn on the wave in well under a second, and the wave
        /// carries the rider back toward the curl with speed (a cutback, not a stop).</summary>
        [Test]
        public void PivotsRoundTightAndKeepsGoing([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(5f);
                var r = sim.Rider;
                Vector3 T = r.Sample.CrestDir, D = r.Sample.TravelDir;
                Assert.Greater(Vector3.Dot(r.HeadingDir, T), 0.8f, "riding down the line before the turn");
                var hands = sim.TakeOver();
                float toBottom = Mathf.Sign(Vector3.SignedAngle(r.HeadingDir, D, Vector3.up));
                hands.steer = _ => toBottom;
                hands.stall = true;
                float t0 = sim.Elapsed;
                sim.Run(2f, x => Vector3.Dot(x.Rider.HeadingDir, T) < -0.8f || !x.Riding);
                float turn = sim.Elapsed - t0;
                Assert.Less(turn, 0.85f, "the pivot is too slow");
                hands.steer = _ => 0f;
                hands.stall = false;
                sim.Run(0.6f);
                AssertNoFall(sim, "pivoting round");
                Assert.Less(Vector3.Dot(r.Velocity, T), -3.5f, $"not carried back toward the curl: {sim.Summary()}");
            }
        }

        /// <summary>Carving hard off the top of the face is a top turn: the board stays on the water (a launch needs the
        /// stick centred, or the crouch).</summary>
        [Test]
        public void CarvingOffTheLipIsATopTurn([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.Cruise))
            {
                sim.Run(5f);
                var r = sim.Rider;
                Vector3 T = r.Sample.CrestDir, D = r.Sample.TravelDir;
                float toTop = Mathf.Sign(Vector3.SignedAngle(r.HeadingDir, -D, Vector3.up));
                sim.TakeOver().steer = _ => toTop;
                sim.Run(2.5f, x => Vector3.Dot(x.Rider.HeadingDir, T) < -0.8f || !x.Riding);
                AssertNoFall(sim, "top turn");
                Assert.AreEqual(RiderState.Ride, r.State, $"left the water in a carve: {string.Join(" | ", sim.Events)}");
                Assert.Less(Vector3.Dot(r.HeadingDir, T), -0.8f, "never came round");
            }
        }

        /// <summary>What the player wants to do again and again: race out of the tube, pivot back to the curl, pivot
        /// round in the mouth of the barrel, sit in it, race out.</summary>
        [Test]
        public void InAndOutOfTheTubeAgainAndAgain([ValueSource(nameof(Sizes))] float size)
        {
            using (var sim = Up(size, RiderBot.Plan.InAndOut))
            {
                sim.Run(30f, x => !x.Riding);
                AssertNoFall(sim, "in and out");
                Assert.GreaterOrEqual(sim.TubeRides, 3, sim.Summary());
                Assert.GreaterOrEqual(sim.TubeExits, 3, sim.Summary());
                Assert.GreaterOrEqual(sim.DirectionChanges, 4, sim.Summary());
            }
        }

        /// <summary>The camera follows each change of direction the rider commits to (it swings round at once) but
        /// never flickers from side to side.</summary>
        [Test]
        public void TheCameraFollowsEachTurnRoundWithoutFlicker()
        {
            using (var sim = Up(1f, RiderBot.Plan.InAndOut))
            {
                var side = new CameraRig.SurfCameraMath.LineSide();
                side.Reset();
                int switches = 0;
                float lastSwitch = -10f, minGap = float.MaxValue;
                sim.Run(30f, x =>
                {
                    var r = x.Rider;
                    if (r.State == RiderState.Ride)
                    {
                        Vector3 T = r.Sample.CrestDir;
                        if (side.Update(Vector3.Dot(r.HeadingDir, T), Vector3.Dot(r.Velocity, T), 1f, 0.15f, RideSim.Dt))
                        {
                            switches++;
                            minGap = Mathf.Min(minGap, x.Elapsed - lastSwitch);
                            lastSwitch = x.Elapsed;
                        }
                    }
                    return !x.Riding;
                });
                Assert.GreaterOrEqual(switches, 4, $"the camera missed the turns: {sim.Summary()}");
                Assert.Greater(minGap, 0.6f, "the camera flickered between sides");
            }
        }

        /// <summary>The sprint button does nothing but sprint: pressed and released while riding, the ride goes on.</summary>
        [Test]
        public void TheSprintButtonNeverEndsARide()
        {
            using (var sim = Up(1f, RiderBot.Plan.Cruise))
            {
                sim.TakeOver().mashSprint = true;
                sim.Run(6f);
                AssertNoFall(sim, "sprint mashed");
                Assert.AreEqual(RiderState.Ride, sim.Rider.State, sim.Summary());
            }
        }
    }
}
