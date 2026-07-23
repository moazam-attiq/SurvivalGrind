using System.Collections;
using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class HazardGap : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("Max crowd members this hazard can kill per hit. Actual count is however many were really standing in the gap.")]
        [SerializeField] private int killBatchSize = 10;
        [Tooltip("Seconds to keep watching this gap after the Commander crosses it, so the crowd trailing behind can actually fall in as they catch up.")]
        [SerializeField] private float crowdCatchUpWindow = 1.2f;
        [Tooltip("Width (world units) of the real kill zone at the gap -- kept narrow (roughly one crowd row) since the gap's own trigger box is sized for its visual length, not blast radius.")]
        [SerializeField] private float impactWidth = 1f;
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
            HazardIndicator.Spawn(transform, HazardKind.Gravity, 1.8f);
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
                StartCoroutine(CatchTrailingCrowd(runner));
            }
        }

        private IEnumerator CatchTrailingCrowd(LevelRunner runner)
        {
            int totalKilled = 0;
            float elapsed = 0f;
            while (elapsed < crowdCatchUpWindow)
            {
                elapsed += Time.deltaTime;
                if (totalKilled < killBatchSize)
                {
                    var formation = CrowdFormationController.Instance;
                    if (formation != null) totalKilled += formation.KillNear(hitCollider.bounds.center, impactWidth, killBatchSize - totalKilled);
                }
                yield return null;
            }
            runner.RegisterHazardHit(totalKilled);
        }
    }
}
