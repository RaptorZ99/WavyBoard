using NUnit.Framework;
using WavyBoard.CameraRig;

namespace WavyBoard.Tests
{
    /// <summary>Which way the ride shot looks along the line: it follows the board once the rider commits, not before.</summary>
    public class CameraDirectionTests
    {
        const float Dt = 0.02f, MinSpeed = 1f, Delay = 0.15f;

        static int Run(ref SurfCameraMath.LineSide side, float headingT, float vT, float seconds)
        {
            int switched = 0;
            for (float t = 0f; t < seconds; t += Dt)
                if (side.Update(headingT, vT, MinSpeed, Delay, Dt)) switched++;
            return switched;
        }

        [Test]
        public void ANoseSwungRoundWhileStillSlidingTheOldWayDoesNotSwingTheCamera()
        {
            var side = new SurfCameraMath.LineSide();
            side.Reset(1f);
            Assert.AreEqual(0, Run(ref side, -0.9f, 4f, 1f), "the slide of a top turn, a revert");
            Assert.AreEqual(1f, side.Side);
        }

        [Test]
        public void ACommittedTurnRoundSwingsTheCameraAtOnce()
        {
            var side = new SurfCameraMath.LineSide();
            side.Reset(1f);
            Assert.AreEqual(0, Run(ref side, -0.9f, -3f, Delay * 0.5f), "not before the delay");
            Assert.AreEqual(1, Run(ref side, -0.9f, -3f, Delay), "right after it");
            Assert.AreEqual(-1f, side.Side);
        }

        [Test]
        public void AWobbleAcrossTheFaceDoesNotSwingTheCamera()
        {
            var side = new SurfCameraMath.LineSide();
            side.Reset(1f);
            Assert.AreEqual(0, Run(ref side, -0.3f, -2f, 1f), "pointed down the face, not along the line");
            Assert.AreEqual(0, Run(ref side, -0.9f, -0.5f, 1f), "too slow to be going anywhere");
        }
    }
}
