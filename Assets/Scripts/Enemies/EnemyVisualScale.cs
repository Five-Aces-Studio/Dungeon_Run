using DungeonRun.UI.Presentation;
using UnityEngine;

namespace DungeonRun.Enemies
{
    /// <summary>Uniform presentation size of an enemy prefab. The prefab root stays at scale 1; only the
    /// visual root (model, weapons, sockets, anchors and collider) is scaled, so size can be tuned from the
    /// Inspector without re-exporting art. No gameplay value depends on it.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVisualScale : MonoBehaviour
    {
        [Tooltip("Scaled child holding the model, sockets, anchors and collider.")]
        public Transform visualRoot;

        [Range(EnemyVisualScaleRules.Min, EnemyVisualScaleRules.Max)]
        [Tooltip("Uniform size of the visual root. Tune against the protagonist with the combat camera.")]
        public float visualScale = 1f;

        public float Applied => EnemyVisualScaleRules.Resolve(visualScale);

        private void Awake() => Apply();

        private void OnValidate() => Apply();

        public void Apply()
        {
            if (!visualRoot || visualRoot == transform) return;
            visualRoot.localScale = Vector3.one * Applied;
#if UNITY_EDITOR
            var lods = GetComponent<LODGroup>();
            if (lods) lods.RecalculateBounds();
#endif
        }
    }
}
