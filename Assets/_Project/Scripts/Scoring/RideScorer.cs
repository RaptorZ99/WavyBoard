using System.Collections.Generic;
using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.Scoring
{
    /// <summary>Names and scores tricks, tube time and waves (spec §2.6-2.7, simplified v1).</summary>
    public class RideScorer : MonoBehaviour
    {
        public RiderController rider;

        public float WaveRawPoints { get; private set; }
        public float PendingPoints { get; private set; }
        public float WaveScore { get; private set; }          // 0..10 of the last committed wave
        public float Best1 { get; private set; }
        public float Best2 { get; private set; }
        public float SessionPoints { get; private set; }
        public int ChainCount { get; private set; }
        /// <summary>Multiplier the current chain of linked manoeuvres pays.</summary>
        public float ChainMultiplier => Mathf.Min(2f, Mathf.Pow(1.1f, ChainCount));
        public string LastTrick { get; private set; } = "";
        public float LastTrickPoints { get; private set; }
        public float LastTrickTime { get; private set; } = -10f;
        public float LastWaveScoreTime { get; private set; } = -10f;
        public List<string> WaveTricks { get; } = new List<string>();

        readonly Dictionary<string, int> repeats = new Dictionary<string, int>();
        float chainTimer;
        float tubeAccum;
        bool wasInTube;

        void OnEnable()
        {
            if (rider == null) return;
            rider.OnEvent += OnRiderEvent;
            rider.OnTrick += OnTrick;
        }

        void OnDisable()
        {
            if (rider == null) return;
            rider.OnEvent -= OnRiderEvent;
            rider.OnTrick -= OnTrick;
        }

        /// <summary>A completed manoeuvre (air, or one drawn on the water). Repeating the same one pays less.</summary>
        void OnTrick(string name, float basePts)
        {
            int rep = repeats.TryGetValue(name, out var r) ? r : 0;
            repeats[name] = rep + 1;
            Commit(name, basePts * Mathf.Pow(0.7f, rep), chain: true);
        }

        void Update()
        {
            if (rider == null) return;
            float dt = Time.deltaTime;
            if (rider.State == RiderState.Ride && rider.InTube)
            {
                float pts = 40f * dt * (1f + 2f * Mathf.Clamp01(rider.Sample.TubeDepth));
                tubeAccum += pts;
                PendingPoints += pts;
                if (!wasInTube) { wasInTube = true; }
            }
            else if (wasInTube)
            {
                wasInTube = false;
                if (rider.TubeTime <= 0f && tubeAccum > 20f)
                {
                    Commit("Tube " + (tubeAccum / 40f).ToString("0.0") + "s", tubeAccum, chain: true);
                    tubeAccum = 0f;
                }
            }
            if (chainTimer > 0f) { chainTimer -= dt; if (chainTimer <= 0f) ChainCount = 0; }
        }

        void OnRiderEvent(string e)
        {
            switch (e)
            {
                case "Pump":
                    break;
                case "Kick-out":
                case "Wave over":
                case "Over the back":
                    CommitWave(false);
                    break;
                case "Back to the lineup":
                    break;
                default:
                    if (e.StartsWith("Wipeout")) CommitWave(true);
                    break;
            }
        }

        void Commit(string name, float pts, bool chain)
        {
            if (chain)
            {
                if (chainTimer > 0f) ChainCount = Mathf.Min(ChainCount + 1, 7);
                chainTimer = 2.5f;
                pts *= ChainMultiplier;
            }
            WaveRawPoints += pts;
            PendingPoints = Mathf.Max(0f, PendingPoints - pts);
            LastTrick = name;
            LastTrickPoints = pts;
            LastTrickTime = Time.time;
            WaveTricks.Add(name);
        }

        void CommitWave(bool wipeout)
        {
            if (wasInTube && tubeAccum > 20f && !wipeout) { Commit("Tube " + (tubeAccum / 40f).ToString("0.0") + "s", tubeAccum, chain: false); }
            wasInTube = false; tubeAccum = 0f; PendingPoints = 0f;
            float raw = WaveRawPoints;
            if (!wipeout) raw += Mathf.Min(150f, rider.RideTime * 6f);   // length of ride bonus
            if (wipeout) raw *= 0.8f;
            if (raw <= 1f && rider.RideTime < 2f) { ResetWave(); return; }
            WaveScore = 10f * raw / (raw + 900f);
            SessionPoints += raw;
            if (WaveScore > Best1) { Best2 = Best1; Best1 = WaveScore; }
            else if (WaveScore > Best2) Best2 = WaveScore;
            LastWaveScoreTime = Time.time;
            ResetWave();
        }

        void ResetWave()
        {
            WaveRawPoints = 0f; ChainCount = 0; chainTimer = 0f;
            WaveTricks.Clear(); repeats.Clear();
        }
    }
}
