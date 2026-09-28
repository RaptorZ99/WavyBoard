using NUnit.Framework;
using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.Tests
{
    /// <summary>How a wave is placed when the scheduler has to start it further out to keep the spacing.</summary>
    public class WaveSpawnTests
    {
        GameObject go;
        SurfSpotConfig spot;

        // hidden and never saved: the test must not touch (or dirty) the scene open in the Editor
        [SetUp] public void SetUp()
        {
            go = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("WaveSpawnTests", HideFlags.HideAndDontSave, typeof(SurfSpotConfig));
            spot = go.GetComponent<SurfSpotConfig>();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(go); }

        [Test]
        public void ExtraLeadStartsTheCrestFurtherOutButBreaksAtTheSamePlace()
        {
            var aim = new Vector3(3f, 0f, 40f);
            var a = spot.BuildParams(1, 1f, aim);
            var b = spot.BuildParams(2, 1f, aim, -1f, 30f);
            Assert.AreEqual(a.crestStartOffset - 30f, b.crestStartOffset, 1e-3f, "the crest starts 30 m further out");
            Assert.AreEqual(0f, b.CrestOffset(b.firstBreakTime), 1e-3f, "and reaches the break line when it breaks");
            Assert.AreEqual(a.firstBreakTime + 30f / b.celerity, b.firstBreakTime, 1e-3f, "later by the time it takes to cover them");
            Assert.AreEqual(a.endTime - a.firstBreakTime, b.endTime - b.firstBreakTime, 1e-3f, "its life after the break is unchanged");
        }

        [Test]
        public void EveryWaveTravelsAtTheSameSpeedByDefault()
        {
            Assert.AreEqual(0f, spot.celeritySizeExponent, "sets would catch each other up otherwise");
            Assert.AreEqual(spot.CelerityFor(0.68f), spot.CelerityFor(1.45f), 1e-4f);
        }
    }
}
