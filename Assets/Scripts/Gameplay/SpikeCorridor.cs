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
        [Tooltip("Max crowd members this hazard can kill per hit. Actual count is however many were really standing in the spikes.")]
        [SerializeField] private int killBatchSize = 8;
        [SerializeField] private int shieldBlockBonus = 25;
        [Tooltip("Minimum seconds between successive hits while standing in a lethal spike window.")]
        [SerializeField] private float retriggerCooldown = 0.5f;
        [Tooltip("Rise duration (the reaction window before spikes go lethal) is multiplied down toward this fraction as the survival timer approaches its target.")]
        [SerializeField] private float minWarningFactor = 0.5f;
        [Tooltip("Width (world units) of the real kill zone around the Commander at the moment of impact -- kept narrow (roughly one crowd row) since the spike corridor's own trigger box is sized for its visual length, not blast radius.")]
        [SerializeField] private float impactWidth = 1f;

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
        private float currentRiseDuration;
        private BoxCollider2D hitCollider;
        private float lastHit = -999f;
        private bool frozenBlockRegisteredThisWindow;
        private int pendingKilled;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
            if (spikeRenderer == null) spikeRenderer = GetComponent<SpriteRenderer>();
            SetScaleY(retractedScaleY);
            if (warningGlow != null) warningGlow.intensity = 0f;
            currentRiseDuration = riseDuration;
            timer = Random.Range(0f, retractedDuration);
            HazardIndicator.Spawn(transform, HazardKind.Barrage, 1.8f);
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
                    if (timer <= 0f)
                    {
                        state = State.Rising;
                        float ramp = LevelRunner.Instance != null ? LevelRunner.Instance.DifficultyRamp01 : 0f;
                        currentRiseDuration = Mathf.Lerp(riseDuration, riseDuration * minWarningFactor, ramp);
                        timer = currentRiseDuration;
                        AudioManager.PlaySfx(risingSfx);
                        runner.NotifyHazardWarning(HazardKind.Barrage);
                    }
                    break;
                case State.Rising:
                    {
                        float t = 1f - Mathf.Clamp01(timer / currentRiseDuration);
                        SetScaleY(Mathf.Lerp(retractedScaleY, extendedScaleY, t));
                        if (warningGlow != null) warningGlow.intensity = Mathf.Lerp(0f, 2.5f, t);
                        if (timer <= 0f)
                        {
                            state = State.Lethal; timer = lethalDuration;
                            hitCollider.enabled = true;
                            frozenBlockRegisteredThisWindow = false;
                            pendingKilled = 0;
                            AudioManager.PlaySfx(impactSfx);
                        }
                        break;
                    }
                case State.Lethal:
                    // The spikes sit at a fixed spot; keep sampling every frame they're live,
                    // independent of the score retrigger cooldown below, so a member scrolling
                    // through between ticks still counts.
                    if (!runner.IsHazardNeutralized(HazardKind.Barrage) && pendingKilled < killBatchSize)
                    {
                        var formation = CrowdFormationController.Instance;
                        if (formation != null) pendingKilled += formation.KillNear(hitCollider.bounds.center, impactWidth, killBatchSize - pendingKilled);
                    }
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

            int killed = pendingKilled;
            pendingKilled = 0;
            runner.RegisterHazardHit(killed);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);
    }
}
