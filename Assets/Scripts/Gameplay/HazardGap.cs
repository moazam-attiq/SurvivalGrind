using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class HazardGap : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private int killBatchSize = 10;
        [SerializeField] private GravityLane dangerLane = GravityLane.Ground;

        [Header("Visibility")]
        [SerializeField] private float pulseSpeed = 1.2f;
        [SerializeField] private float minAlpha = 0.55f;
        [SerializeField] private float maxAlpha = 0.85f;
        [SerializeField] private float glowIntensity = 2f;

        [Header("Refs")]
        [SerializeField] private SpriteRenderer voidRenderer;
        [SerializeField] private UnityEngine.Rendering.Universal.Light2D voidGlow;

        [Header("Audio")]
        [SerializeField] private AudioClip fallSfx;
        [SerializeField] private AudioClip gravityShiftPassClip;

        private BoxCollider2D hitCollider;
        private Color baseColor;
        private bool resolved;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.enabled = true;
            if (voidRenderer == null) voidRenderer = GetComponent<SpriteRenderer>();
            if (voidRenderer != null)
            {
                voidRenderer.enabled = true;
                baseColor = voidRenderer.color;
            }
            if (voidGlow != null)
            {
                voidGlow.enabled = true;
                voidGlow.intensity = glowIntensity;
            }
        }

        private void OnEnable()
        {
            CommanderController.OnLoopReset += ResetState;
        }

        private void OnDisable()
        {
            CommanderController.OnLoopReset -= ResetState;
        }

        private void Update()
        {
            if (voidRenderer == null) return;

            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            var c = baseColor;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            voidRenderer.color = c;
        }

        public void ResetState()
        {
            resolved = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved) return;
            if (!other.CompareTag("Leader")) return;

            var runner = LevelRunner.Instance;
            if (runner == null) return;

            resolved = true;

            if (runner.CurrentGravityLane != dangerLane)
            {
                runner.RegisterGravityShiftPass();
                AudioManager.PlaySfx(gravityShiftPassClip);
            }
            else
            {
                AudioManager.PlaySfx(fallSfx);
                runner.RegisterHazardHit(killBatchSize);
            }
        }
    }
}
