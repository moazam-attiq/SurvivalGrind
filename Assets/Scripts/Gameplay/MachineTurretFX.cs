using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// Idle ambience layer for a hazard-machine prop: continuously sweeps its barrel ~180 degrees
    /// and, for bomb-type machines, periodically lobs small cosmetic bomb projectiles from the
    /// current barrel angle. Purely visual -- the actual hit/damage hazard is the paired HazardLaser beam.
    public class MachineTurretFX : MonoBehaviour
    {
        [Header("Sweep")]
        [SerializeField] private float sweepHalfAngle = 90f;
        [SerializeField] private float sweepSpeed = 0.6f;
        [SerializeField] private float sweepPhase = 0f;

        [Header("Bomb Lobbing")]
        [SerializeField] private bool lobBombs = true;
        [SerializeField] private float fireInterval = 2.2f;
        [SerializeField] private float fireIntervalJitter = 0.6f;
        [SerializeField] private float minBombSize = 0.25f;
        [SerializeField] private float maxBombSize = 0.6f;
        [SerializeField] private float bombSpeed = 9f;
        [SerializeField] private float bombLifetime = 1.4f;
        [SerializeField] private float directionSpreadDegrees = 25f;
        [SerializeField] private Color bombColor = new Color(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private AudioClip fireSfx;

        private static Sprite proceduralBombSprite;

        private float baseZ;
        private float timer;
        private AudioSource audioSource;

        private static Sprite GetBombSprite()
        {
            if (proceduralBombSprite != null) return proceduralBombSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.42f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float edge = Mathf.Clamp01(radius - dist);
                    byte alpha = (byte)(Mathf.Clamp01(edge) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha > 250 ? (byte)255 : alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            proceduralBombSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return proceduralBombSprite;
        }

        private void Awake()
        {
            baseZ = transform.localEulerAngles.z;
            timer = Random.Range(0f, fireInterval);
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            float sweep = Mathf.Sin(Time.time * sweepSpeed + sweepPhase) * sweepHalfAngle;
            var e = transform.localEulerAngles;
            e.z = baseZ + sweep;
            transform.localEulerAngles = e;

            if (!lobBombs) return;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = fireInterval + Random.Range(-fireIntervalJitter, fireIntervalJitter);
                FireBomb();
            }
        }

        private void FireBomb()
        {
            var go = new GameObject("Bomb_Lob");
            go.transform.position = transform.position;
            float size = Random.Range(minBombSize, maxBombSize);
            go.transform.localScale = Vector3.one * size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetBombSprite();
            sr.color = bombColor;
            sr.sortingOrder = 20;

            float angleRad = transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 baseDir = new Vector2(-Mathf.Sin(angleRad), Mathf.Cos(angleRad));
            float jitterRad = Random.Range(-directionSpreadDegrees, directionSpreadDegrees) * Mathf.Deg2Rad;
            float cj = Mathf.Cos(jitterRad), sj = Mathf.Sin(jitterRad);
            Vector2 dir = new Vector2(baseDir.x * cj - baseDir.y * sj, baseDir.x * sj + baseDir.y * cj).normalized;

            var projectile = go.AddComponent<LobbedBombProjectile>();
            projectile.Init(dir * bombSpeed, bombLifetime);

            if (audioSource != null && fireSfx != null) audioSource.PlayOneShot(fireSfx);
        }
    }
}
