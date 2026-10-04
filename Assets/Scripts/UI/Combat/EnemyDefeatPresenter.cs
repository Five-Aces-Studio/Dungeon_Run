using System.Collections.Generic;
using DG.Tweening;
using DungeonRun.Combat;
using UnityEngine;

namespace DungeonRun.UI
{
    /// <summary>
    /// V4 presentation-only defeat staging: on the authoritative Defeated event the enemy's anchor model sinks, tilts and darkens
    /// (MaterialPropertyBlock, never the material asset). Every touched transform and renderer is restored on Unbind.
    /// Never reads or writes BattleSession state.
    /// </summary>
    public sealed class EnemyDefeatPresenter : MonoBehaviour
    {
        private sealed class Staged
        {
            public Renderer renderer; public Transform transform;
            public Vector3 position, scale; public Quaternion rotation;
            public bool enabled, hadBlock; public MaterialPropertyBlock original;
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly List<Staged> staged = new List<Staged>();
        private CombatHUD hud;
        private CombatBattleController battle;
        private MaterialPropertyBlock block;

        public void Bind(CombatHUD owner, CombatBattleController controller)
        {
            Unbind(); hud = owner; battle = controller;
            if (battle) battle.EventRaised += OnEvent;
        }

        public void Unbind()
        {
            if (battle) battle.EventRaised -= OnEvent;
            battle = null;
            foreach (var entry in staged) Restore(entry);
            staged.Clear();
        }

        private void OnEvent(BattleEvent value)
        {
            if (value.Kind != BattleEventKind.Defeated || value.ActorId == 0 || !hud || !Application.isPlaying) return;
            var style = hud.theme ? hud.theme.v4Style : null;
            if (!style) return;
            foreach (var enemy in hud.enemies)
                if (enemy && enemy.ActorId == value.ActorId) { if (enemy.Anchor) Sink(enemy.Anchor, style); return; }
        }

        private void Sink(Renderer target, CombatHUDStyleV4 style)
        {
            foreach (var entry in staged) if (entry.renderer == target) return;
            var t = target.transform;
            var item = new Staged
            {
                renderer = target, transform = t, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                enabled = target.enabled, hadBlock = target.HasPropertyBlock()
            };
            if (item.hadBlock) { item.original = new MaterialPropertyBlock(); target.GetPropertyBlock(item.original); }
            staged.Add(item);

            float duration = Mathf.Max(.01f, style.defeatSinkSeconds);
            Vector3 down = Vector3.down * target.bounds.size.y * style.defeatSinkDepth;
            Vector3 localDown = t.parent ? t.parent.InverseTransformVector(down) : down;
            var sequence = DOTween.Sequence().SetTarget(t).SetUpdate(true)
                .Join(t.DOLocalMove(item.position + localDown, duration).SetEase(Ease.InCubic))
                .Join(t.DOLocalRotateQuaternion(item.rotation * Quaternion.Euler(0, 0, style.defeatTiltDegrees), duration).SetEase(Ease.InQuad))
                .Join(t.DOScaleY(item.scale.y * .85f, duration).SetEase(Ease.InQuad));
            var material = target.sharedMaterial;
            if (material && material.HasProperty(BaseColor))
            {
                // Read the asset colour once; darken only through a per-renderer property block.
                Color from = material.GetColor(BaseColor);
                Color to = Color.Lerp(from, style.defeatTint, style.defeatDarken); to.a = from.a;
                sequence.Join(DOVirtual.Float(0, 1, duration, k =>
                {
                    if (!target) return;
                    if (block == null) block = new MaterialPropertyBlock();
                    target.GetPropertyBlock(block); block.SetColor(BaseColor, Color.Lerp(from, to, k)); target.SetPropertyBlock(block);
                }));
            }
            // The sunk, darkened body stays visible as the defeated state; Unbind restores it.
        }

        private static void Restore(Staged entry)
        {
            if (entry.transform)
            {
                entry.transform.DOKill();
                entry.transform.localPosition = entry.position; entry.transform.localRotation = entry.rotation; entry.transform.localScale = entry.scale;
            }
            if (!entry.renderer) return;
            entry.renderer.enabled = entry.enabled;
            entry.renderer.SetPropertyBlock(entry.hadBlock ? entry.original : null);
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
