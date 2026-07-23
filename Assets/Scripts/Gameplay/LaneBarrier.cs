using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// A stationary solid obstacle occupying one lane band over a stretch of track.
    /// Unlike HazardLaser (a timed beam), a barrier is always "live": running into it
    /// costs crowd, so the player must weave into an open lane to get past it. The Shield
    /// Dome / Freeze channel (LevelRunner.IsFrozen) still lets the team pass unharmed,
    /// matching every other hazard in the game.
    [RequireComponent(typeof(BoxCollider2D))]
    public class LaneBarrier : MonoBehaviour
    {
        [Tooltip("Max crowd removed per retrigger tick. Actual count is however many were really standing in the barrier.")]
        [SerializeField] private int killBatchSize = 6;
        [Tooltip("Width (world units) of the real kill zone around the Commander at the moment of impact -- kept narrow (roughly one crowd row) since the barrier's own trigger box is sized for its visual length, not blast radius.")]
        [SerializeField] private float impactWidth = 1f;
        [Tooltip("Minimum seconds between successive hits while the Commander stays inside the barrier, " +
                 "so brushing it costs a bite of crowd rather than instantly wiping it.")]
        [SerializeField] private float retriggerCooldown = 0.5f;

        [Header("Ability Gating")]
        [Tooltip("Which ability (and ONLY that ability) lets the team through unharmed. Barriers/gaps/broken " +
                 "roads = Gravity Shift. Freeze and Shield do NOT help here.")]
        [SerializeField] private HazardKind requiredAbility = HazardKind.Gravity;

        private float lastHit = -999f;
        private BoxCollider2D hitCollider;
        private int pendingKilled;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            HazardIndicator.Spawn(transform, requiredAbility, 1.8f);
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void TryHit(Collider2D other)
        {
            if (!other.CompareTag("Leader")) return;

            var runner = LevelRunner.Instance;
            if (runner == null) return;
            if (runner.State != LevelState.Playing) return;
            // ONLY a Gravity Shift lets the team through a barrier - Freeze/Shield do nothing here.
            if (runner.IsHazardNeutralized(requiredAbility)) return;

            // The barrier sits at a fixed spot; keep sampling every physics tick the Leader stays
            // inside, independent of the retrigger cooldown below, so a member scrolling through
            // between ticks still counts.
            if (pendingKilled < killBatchSize)
            {
                var formation = CrowdFormationController.Instance;
                if (formation != null) pendingKilled += formation.KillNear(hitCollider.bounds.center, impactWidth, killBatchSize - pendingKilled);
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
