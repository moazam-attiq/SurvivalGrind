using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// A bomb (Barrage kind) that locks onto the Commander's position and falls on it.
    /// Shows a pulsing ground warning reticle at the predicted impact point BEFORE the bomb
    /// appears and falls, so the player has time to react. Blocked ONLY by the Shield Dome;
    /// otherwise it costs ALIVE on impact.
    [RequireComponent(typeof(Collider2D))]
    public class TargetedBomb : MonoBehaviour
    {
        private enum State { Telegraph, Falling, Done }

        [Header("Telegraph")]
        [Tooltip("Seconds the ground warning reticle pulses at the predicted impact point before the bomb starts falling.")]
        [SerializeField] private float telegraphDuration = 1.3f;
        [SerializeField] private float reticleRadius = 0.9f;
        [SerializeField] private Color reticleColor = new Color(1f, 0.35f, 0.1f, 0.85f);

        [Header("Fall")]
        [SerializeField] private int killBatchSize = 8;
        [SerializeField] private float fallSpeed = 9f;
        [Tooltip("Y at which the bomb detonates on the ground if it misses the crowd.")]
        [SerializeField] private float groundY = -5f;
        [SerializeField] private float spinSpeed = 300f;
        [SerializeField] private int shieldBlockBonus = 30;
        [Tooltip("Width (world units) of the real kill zone at the blast point, matching LaneBarrier's narrow per-hit zone so the crowd visibly shrinks by the same amount the audience count does.")]
        [SerializeField] private float impactWidth = 1.5f;

        [Header("Feedback")]
        [SerializeField] private GameObject explosionVfx;
        [SerializeField] private AudioClip warningSfx;
        [SerializeField] private AudioClip explodeSfx;
        [SerializeField] private AudioClip blockSfx;

        private State state = State.Telegraph;
        private float telegraphTimer;
        private float targetX;
        private float topY;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer reticleRenderer;
        private Transform reticle;
        private Collider2D col;

        private static Sprite proceduralRingSprite;

        private static Sprite GetRingSprite()
        {
            if (proceduralRingSprite != null) return proceduralRingSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerR = size * 0.46f;
            float innerR = size * 0.34f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float a = 0f;
                    if (dist <= outerR && dist >= innerR) a = 1f;
                    else if (dist < innerR) a = Mathf.Clamp01(1f - (innerR - dist));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            proceduralRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
            return proceduralRingSprite;
        }

        /// Lock the bomb to a target X (the Commander's predicted position) and a spawn height.
        /// The ground reticle appears immediately; the bomb itself only starts falling after the telegraph.
        public void Init(float targetX, float topY)
        {
            this.targetX = targetX;
            this.topY = topY;
        }

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            if (col != null) { col.isTrigger = true; col.enabled = false; }
            bodyRenderer = GetComponent<SpriteRenderer>();

            var reticleGo = new GameObject("WarningReticle");
            reticleGo.transform.SetParent(transform, false);
            reticleRenderer = reticleGo.AddComponent<SpriteRenderer>();
            reticleRenderer.sprite = GetRingSprite();
            reticleRenderer.color = reticleColor;
            reticleRenderer.sortingLayerName = bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default";
            reticleRenderer.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 0) - 1;
            reticleGo.transform.localScale = Vector3.one * (reticleRadius * 2f);
            reticle = reticleGo.transform;
        }

        private void Start()
        {
            if (bodyRenderer != null) bodyRenderer.enabled = false;
            transform.position = new Vector3(targetX, groundY, transform.position.z);
            reticle.position = new Vector3(targetX, groundY, 0f);
            AudioManager.PlaySfxAt(warningSfx, transform.position);
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner != null && runner.State != LevelState.Playing) return;

            if (state == State.Telegraph)
            {
                telegraphTimer += Time.deltaTime;
                float pulse = (Mathf.Sin(telegraphTimer * 14f) + 1f) * 0.5f;
                var c = reticleColor;
                c.a = Mathf.Lerp(0.35f, 0.9f, pulse);
                reticleRenderer.color = c;
                float scaleT = Mathf.Clamp01(telegraphTimer / telegraphDuration);
                reticle.localScale = Vector3.one * (reticleRadius * 2f) * Mathf.Lerp(1.6f, 1f, scaleT);

                if (telegraphTimer >= telegraphDuration) BeginFall();
                return;
            }

            if (state == State.Falling)
            {
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
                if (transform.position.y <= groundY) Explode(false);
            }
        }

        private void BeginFall()
        {
            state = State.Falling;
            reticle.gameObject.SetActive(false);
            if (bodyRenderer != null) bodyRenderer.enabled = true;
            if (col != null) col.enabled = true;
            transform.position = new Vector3(targetX, topY, transform.position.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (state == State.Falling && other.CompareTag("Leader")) Explode(true);
        }

        private void Explode(bool hitCrowd)
        {
            if (state == State.Done) return;
            state = State.Done;

            var runner = LevelRunner.Instance;
            if (hitCrowd && runner != null && runner.State == LevelState.Playing)
            {
                if (runner.IsHazardNeutralized(HazardKind.Barrage))
                {
                    runner.AddScore(shieldBlockBonus);
                    runner.RegisterFreezeBlock("SHIELDED THE BOMB");
                    AudioManager.PlaySfxAt(blockSfx, transform.position);
                }
                else
                {
                    var formation = CrowdFormationController.Instance;
                    int killed = formation != null ? formation.KillNear(transform.position, impactWidth, killBatchSize) : killBatchSize;
                    runner.RegisterHazardHit(killed);
                    AudioManager.PlaySfxAt(explodeSfx, transform.position);
                }
            }
            else
            {
                AudioManager.PlaySfxAt(explodeSfx, transform.position);
            }

            if (explosionVfx != null)
            {
                explosionVfx.transform.SetParent(null, true);
                explosionVfx.SetActive(true);
                Destroy(explosionVfx, 1.2f);
            }
            Destroy(gameObject);
        }
    }
}
