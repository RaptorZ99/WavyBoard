using UnityEngine;

namespace WavyBoard.Rider
{
    /// <summary>How a board rides: multipliers on the rider tuning's drag, rail grip, turn rate and pop.</summary>
    [CreateAssetMenu(menuName = "WavyBoard/Board Spec", fileName = "BoardSpec")]
    public class BoardSpec : ScriptableObject
    {
        public float dragMultiplier = 1f;
        public float gripMultiplier = 1f;
        public float yawMultiplier = 1f;
        public float popMultiplier = 1f;
    }
}
