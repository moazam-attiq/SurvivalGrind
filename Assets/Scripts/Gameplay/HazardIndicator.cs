using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// A small colored marker every hazard spawns above itself so the required ability is readable
    /// at a glance, from idle range, before any warning telegraph starts. Color is the only signal
    /// that needs to stay consistent everywhere: ice blue = Freeze, violet = Gravity Shift, amber =
    /// Shield Dome. Built from a plain white texture at runtime so it never depends on scene/prefab
    /// art being wired up correctly.
    public static class HazardIndicator
    {
        private static Sprite dotSprite;

        public static Color GetColor(HazardKind kind) => kind switch
        {
            HazardKind.Laser => new Color(0.3f, 0.75f, 1f),
            HazardKind.Gravity => new Color(0.78f, 0.35f, 1f),
            HazardKind.Barrage => new Color(1f, 0.55f, 0.15f),
            _ => Color.white
        };

        public static void Spawn(Transform parent, HazardKind kind, float heightAbove)
        {
            if (dotSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                dotSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
            }

            var go = new GameObject("AbilityIndicator");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, heightAbove, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.color = GetColor(kind);
            sr.sortingOrder = 100;

            go.AddComponent<IndicatorPulse>();
        }

        private class IndicatorPulse : MonoBehaviour
        {
            private Vector3 baseScale;
            private float phase;

            private void Awake()
            {
                baseScale = transform.localScale;
                phase = Random.Range(0f, Mathf.PI * 2f);
            }

            private void Update()
            {
                float s = 1f + Mathf.Sin(Time.time * 2.5f + phase) * 0.18f;
                transform.localScale = baseScale * s;
            }
        }
    }
}
