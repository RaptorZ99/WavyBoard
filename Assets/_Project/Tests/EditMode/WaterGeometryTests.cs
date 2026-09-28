using NUnit.Framework;
using WavyBoard.CameraRig;
using WavyBoard.Wave;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WavyBoard.Tests
{
    /// <summary>The water-volume test the camera relies on, and the spring arm built on it.</summary>
    public class WaterGeometryTests
    {
        NativeArray<float2> keys, vmap, pts;

        [SetUp]
        public void SetUp()
        {
            keys = WaveProfile.CreateKeys(Allocator.Temp);
            vmap = WaveProfile.CreateVertexMap(Allocator.Temp);
            pts = new NativeArray<float2>(WaveProfile.VertexCount, Allocator.Temp);
        }

        [TearDown]
        public void TearDown()
        {
            keys.Dispose(); vmap.Dispose(); pts.Dispose();
        }

        /// <summary>A heavy open barrel of a 3.4 m wave, half-way through its life.</summary>
        WaveProfile.Landmarks Barrel()
        {
            var ri = new WaveProfile.RowInput
            {
                tau = 0.5f * (WaveProfile.TBarrel + WaveProfile.TBarrel2), shoal = 1f, heavy = 1f, lipless = 0f,
                tubeScale = 1.3f, xScale = 3.4f, yScale = 3.4f, xiMin = -44f, xiMax = 30f,
            };
            var slice = new NativeSlice<float2>(pts);
            WaveProfile.Section(keys, vmap, ri, slice);
            return WaveProfile.Find(slice);
        }

        [Test]
        public void InsideOfTheTubeIsAir_UnderTheFaceAndInsideTheLipIsWater()
        {
            var L = Barrel();
            var slice = new NativeSlice<float2>(pts);
            Assert.IsTrue(L.curl, "an open barrel overhangs");
            float x = math.lerp(L.xRef, L.xTip, 0.5f);
            float floor = WaveProfile.HeightAt(slice, L, x, out bool roof, out float roofY, out _);
            Assert.IsTrue(roof, "a lip hangs over the middle of the tube");

            Assert.IsFalse(WaveProfile.IsInside(slice, x, 0.5f * (floor + roofY)), "the middle of the tube is air");
            Assert.IsTrue(WaveProfile.IsInside(slice, x, floor - 0.3f), "under the face is water");
            Assert.IsTrue(WaveProfile.IsInside(slice, x, roofY + 0.05f), "just above the ceiling is inside the lip");
            Assert.IsFalse(WaveProfile.IsInside(slice, x, L.yTop + 1f), "above the wave is air");
            Assert.IsTrue(WaveProfile.IsInside(slice, 20f, -0.5f), "under the flat sea in front is water");
            Assert.IsFalse(WaveProfile.IsInside(slice, 20f, 0.5f), "above the flat sea in front is air");
        }

        /// <summary>Water below y = 0, plus a thin wall of water 0.3 m thick at x in [2, 2.3].</summary>
        sealed class FakeWater : IWaterProbe
        {
            public NativeArray<float3> Pts = new NativeArray<float3>(64, Allocator.Temp);
            public NativeArray<WaterProbe> Res = new NativeArray<WaterProbe>(64, Allocator.Temp);
            public int Capacity => 64;
            public NativeArray<float3> ProbePoints => Pts;
            public NativeArray<WaterProbe> ProbeResults => Res;
            public void Probe(int count, double time)
            {
                for (int i = 0; i < count; i++)
                {
                    float3 p = Pts[i];
                    Res[i] = new WaterProbe { inside = p.y < 0f || (p.x >= 2f && p.x <= 2.3f), height = 0f };
                }
            }
        }

        [Test]
        public void SpringArmStopsShortOfAThinWall()
        {
            var water = new FakeWater();
            try
            {
                float free = SurfCameraMath.FreeLength(water, new Vector3(0f, 1f, 0f), new Vector3(6f, 1f, 0f), 0.0);
                Assert.Less(free, 2f, "the camera never ends up past (or inside) the wall");
                Assert.Greater(free, 1.3f, "but comes in only as far as needed");
                float clear = SurfCameraMath.FreeLength(water, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 6f), 0.0);
                Assert.AreEqual(6f, clear, 1e-4f, "a clear line keeps its full length");
            }
            finally { water.Pts.Dispose(); water.Res.Dispose(); }
        }
    }
}
