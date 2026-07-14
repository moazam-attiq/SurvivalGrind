using System.Collections;
using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// An aerial strike hazard (Barrage kind). A ground reticle telegraphs the strike zone, then a
    /// fast vertical beam of energy crashes down through it for a brief lethal window. Blocked ONLY
    /// by the Shield Dome. Triggers once when the Commander enters the zone, so it reads as a
    /// hand-placed set-piece rather than a looping trap.
    [RequireComponent(typeof(BoxCollider2D))]
    public class AerialStrikeZone : MonoBehaviour
    {
        private enum State { Armed, Telegraph, Strike, Spent }

        [Header("Timing")]
        [SerializeField] private float triggerDistance = 7f;
        [SerializeField] private float telegraphDuration = 1.0f;
        [SerializeField] private float strikeDuration = 0.35f;

        [Header("Damage")]
        [SerializeField] private int killBatchSize = 10;
        [SerializeField] private int shieldBlockBonus = 30;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer reticleRenderer;
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private ParticleSystem impactVfx;

        [Header("Audio")]
        [SerializeField] private AudioClip warningSfx;
        [SerializeField] private AudioClip strikeSfx;
        [SerializeField] private AudioClip blockSfx;

        private State state = State.Armed;
        private float timer;
        private BoxCollider2D hitCollider;
        private Transform commander;
        private bool hitRegistered;
        private bool frozenBlockRegistered;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
            if (reticleRenderer != null) reticleRenderer.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = false;
        }

        private void Update()
        {
            if (state != State.Armed) return;

            if (commander == null)
            {
                var l = GameObject.FindGameObjectWithTag("Leader");
                if (l != null) commander = l.transform;
                else return;
            }

            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing) return;

            if (commander.position.x >= transform.position.x - triggerDistance)
            {
                state = State.Telegraph;
                StartCoroutine(RunSequence());
            }
        }

        private IEnumerator RunSequence()
        {
            if (reticleRenderer != null) reticleRenderer.enabled = true;
            AudioManager.PlaySfx(warningSfx);

            float elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                elapsed += Time.deltaTime;
                if (reticleRenderer != null)
                {
                    float pulse = (Mathf.Sin(elapsed * 16f) + 1f) * 0.5f;
                    var c = reticleRenderer.color;
                    c.a = Mathf.Lerp(0.4f, 0.95f, pulse);
                    reticleRenderer.color = c;
                }
                yield return null;
            }

            state = State.Strike;
            hitRegistered = false;
            frozenBlockRegistered = false;
            if (reticleRenderer != null) reticleRenderer.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = true;
            if (impactVfx != null) impactVfx.Play();
            hitCollider.enabled = true;
            AudioManager.PlaySfx(strikeSfx);

            yield return new WaitForSeconds(strikeDuration);

            state = State.Spent;
            hitCollider.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = false;
            if (impactVfx != null) impactVfx.Stop();

            if (!hitRegistered && !frozenBlockRegistered)
            {
                LevelRunner.Instance?.RegisterSafePass();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (state != State.Strike || !other.CompareTag("Leader")) return;
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (runner.IsHazardNeutralized(HazardKind.Barrage))
            {
                if (frozenBlockRegistered) return;
                frozenBlockRegistered = true;
                runner.AddScore(shieldBlockBonus);
                runner.RegisterFreezeBlock("SHIELDED THE STRIKE");
                AudioManager.PlaySfx(blockSfx);
                return;
            }

            if (hitRegistered) return;
            hitRegistered = true;
            runner.RegisterHazardHit(killBatchSize);
        }
    }
}
