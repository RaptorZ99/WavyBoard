using Unity.Mathematics;

namespace WavyBoard.Ocean
{
    /// <summary>Everything gameplay needs to know about the water at one point. See spec §3.4.</summary>
    public struct WaterSample
    {
        public float Height;          // world y of the surface (face branch when under a lip)
        public float3 Normal;         // unit surface normal
        public float3 Velocity;       // surface water velocity (m/s), includes wave translation
        public float BreakPhase;      // 0 swell, 1 pitching, 2 barrel, 3 whitewater; < 0 = no surf wave here
        public float Energy;          // 0..1 pocket energy
        public float3 TravelDir;      // D
        public float3 CrestDir;       // T
        public float CrestDistance;   // x = xi - crestShift (+ toward the beach)
        public float PeelDistance;    // distance along the crest to the peel point (+ = unbroken side)
        public float PeelSpeed;       // how fast the curl is running along the crest right now (m/s): the pace to race
        public bool InTube;
        public float TubeDepth;       // 0..1
        public float WhitewaterAmount;
        public int WaveId;            // -1 ambient
        public float LipWidth;        // wv (0 when no lip)
        public float LipHeight;       // hv
        public float FaceWidth;       // Lf (front half-width)
        public float WaveHeight;      // H(s)
        public bool HasLipRoof;       // a thrown lip hangs above this point (barrel roof)
        public float LipRoofY;        // world height of the lip underside above this point (valid when HasLipRoof)
        public float FaceTop;         // world height of the top of the rideable face (the lip line; under a curl, the top of the back wall)
        public bool OnBack;           // the point is past the top of the face: on the back of the wave, or on top of the lip
        public float SeaLevel;        // world height of the ambient sea under this point (the foot of the wave)
    }

    /// <summary>The water a rider rides: the game's <see cref="WaterSurfaceComposite"/>, or a simulated sea (tests).</summary>
    public interface IWaterSurface
    {
        WaterSample Sample(float3 worldPos, double time);
        /// <summary>The live surf wave with this id, or null.</summary>
        WavyBoard.Wave.SurfWave FindWave(int id);
    }
}
