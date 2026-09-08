using UnityEngine;

namespace Biscotte.Rider
{
    /// <summary>Board definition. The visual mesh is swappable: assign boardModel (e.g. the Blender export) to replace the procedural board.</summary>
    [CreateAssetMenu(menuName = "Biscotte/Board Spec", fileName = "BoardSpec")]
    public class BoardSpec : ScriptableObject
    {
        public string displayName = "Standard";
        public float length = 1.05f;
        public float width = 0.55f;
        public float thickness = 0.055f;
        [Tooltip("Optional external model (e.g. Blender export). When set, the procedural mesh is not used.")]
        public GameObject boardModel;
        public Vector3 boardModelOffset = Vector3.zero;
        public Vector3 boardModelEuler = Vector3.zero;
        public float boardModelScale = 1f;
        public Color deckColor = new Color(0.95f, 0.85f, 0.2f);
        public Color slickColor = new Color(0.1f, 0.1f, 0.12f);
        public float dragMultiplier = 1f;
        public float gripMultiplier = 1f;
        public float yawMultiplier = 1f;
        public float popMultiplier = 1f;
    }
}
