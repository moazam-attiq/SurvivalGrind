using System.Collections;
using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// A spike corridor hazard (Barrage kind). Spikes stay retracted/harmless, then telegraph by
    /// rising out of the floor or ceiling, hold lethal for a window, then retract again - looping for
    /// as long as the corridor segment is on screen. Blocked ONLY by the Shield Dome.
    [RequireComponent(typeof(BoxCollider2D))]
    public class SpikeCorridor : MonoBehaviour
    {
        private enum State { Retracted, Rising, Lethal, Retracting }

        [Header("Timing")]
        [SerializeField] private float retractedDuration = 1.4f;
        [SerializeField] private float riseDuration = 0.45f;
        [SerializeField] private float lethalDuration = 1.0f;
        [SerializeField] private float retractDuration = 0.3f;

        [Header("Damage")]
        [SerializeField] private int killBatchSize = 8;
        [SerializeField] private int shieldBlockBonus = 25;
        [Tooltip("Minimum seconds between successive hits while standing in a lethal spike window.")]
        [SerializeField] private float retriggerCooldown = 0.5f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spikeRenderer;
        [SerializeField] private UnityEngine.Rendering.Universal.Light2D warningGlow;
        [Tooltip("Local Y scale while fully retracted (flat, hidden below the floor line).")]
        [SerializeField] private float retractedScaleY = 0.05f;
        [SerializeField] private float extendedScaleY = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip risingSfx;
        [SerializeField] private AudioClip impactSfx;
        [SerializeField] private AudioClip blockSfx;

        private State state = State.Retracted;
        private float timer;
        private BoxCollider2D hitCollider;
        private float lastHit = -999f;
        private bool frozenBlockRegisteredThisWindow;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
            if (spikeRenderer == null) spikeRenderer = GetComponent<SpriteRenderer>();
            SetScaleY(retractedScaleY);
            if (warningGlow != null) warningGlow.intensity = 0f;
            timer = Random.Range(0f, retractedDuration);
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            bool playing = runner != null && runner.State == LevelState.Playing;
            if (!playing) return;

            timer -= Time.deltaTime;
            switch (state)
            {
                case State.Retracted:
                    if (timer <= 0f) { state = State.Rising; timer = riseDuration; AudioManager.PlaySfx(risingSfx); }
                    break;
                case State.Rising:
                    {
                        float t = 1f - Mathf.Clamp01(timer / riseDuration);
                        SetScaleY(Mathf.Lerp(retractedScaleY, extendedScaleY, t));
                        if (warningGlow != null) warningGlow.intensity = Mathf.Lerp(0f, 2.5f, t);
                        if (timer <= 0f)
                        {
                            state = State.Lethal; timer = lethalDuration;
                            hitCollider.enabled = true;
                            frozenBlockRegisteredThisWindow = false;
                            AudioManager.PlaySfx(impactSfx);
                        }
                        break;
                    }
                case State.Lethal:
                    if (timer <= 0f) { state = State.Retracting; timer = retractDuration; hitCollider.enabled = false; }
                    break;
                case State.Retracting:
                    {
                        float t = 1f - Mathf.Clamp01(timer / retractDuration);
                        SetScaleY(Mathf.Lerp(extendedScaleY, retractedScaleY, t));
                        if (warningGlow != null) warningGlow.intensity = Mathf.Lerp(2.5f, 0f, t);
                        if (timer <= 0f) { state = State.Retracted; timer = retractedDuration; }
                        break;
                    }
            }
        }

        private void SetScaleY(float y)
        {
            if (spikeRenderer == null) return;
            var s = spikeRenderer.transform.localScale;
            s.y = y;
            spikeRenderer.transform.localScale = s;
        }

        private void TryHit(Collider2D other)
        {
            if (state != State.Lethal || !other.CompareTag("Leader")) return;
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing) return;

            if (runner.IsHazardNeutralized(HazardKind.Barrage))
            {
                if (frozenBlockRegisteredThisWindow) return;
                frozenBlockRegisteredThisWindow = true;
                runner.AddScore(shieldBlockBonus);
                runner.RegisterFreezeBlock("SHIELDED THE SPIKES");
                AudioManager.PlaySfx(blockSfx);
                return;
            }

            if (Time.time - lastHit < retriggerCooldown) return;
            lastHit = Time.time;
            runner.RegisterHazardHit(killBatchSize);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);
    }
}
