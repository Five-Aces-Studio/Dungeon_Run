using DG.Tweening;
using UnityEngine;

namespace DungeonRun.UI
{
    public static class UIAnimationHelpers
    {
        public static void Pose(RectTransform rect, Vector2 position, Vector3 rotation, float scale, float duration)
        {
            rect.DOKill();
            if (duration <= 0) { rect.anchoredPosition = position; rect.localEulerAngles = rotation; rect.localScale = Vector3.one * scale; return; }
            DOTween.Sequence().SetTarget(rect).SetUpdate(true)
                .Join(rect.DOAnchorPos(position, duration).SetEase(Ease.OutCubic))
                .Join(rect.DOLocalRotate(rotation, duration).SetEase(Ease.OutCubic))
                .Join(rect.DOScale(scale, duration).SetEase(Ease.OutCubic));
        }

        public static void Pulse(RectTransform rect, bool instant)
        {
            rect.DOKill(); rect.localScale = Vector3.one;
            if (!instant) rect.DOPunchScale(Vector3.one * .06f, .24f, 1, .3f).SetUpdate(true);
        }
    }
}
