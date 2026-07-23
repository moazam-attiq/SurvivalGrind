using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// A flying bee hazard (Barrage kind). Spawns from anywhere, actively chases the Commander,
    /// and - unlike a one-hit hazard - keeps ticking damage into the crowd every time it overlaps
    /// the Commander for as long as it stays on the Commander, until either it peels off after its
    /// chase window or the Shield Dome (and only the Shield Dome) is raised, which instantly repels it.
    [RequireComponent(typeof(Collider2D))]
    public class ChasingBee : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("Crowd lost per damage tick while the bee is on the Commander and Shield is not up.")]
        [SerializeField] private int tickKillSize = 3;
        [Tooltip("Seconds between damage ticks while overlapping.")]
        [SerializeField] private float tickInterval = 0.4f;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 5.5f;
        [Tooltip("How long the bee actively homes on the Commander before it gives up and peels away.")]
        [SerializeField] private float chaseDuration = 3.5f;
        [SerializeField] private float wanderAmplitude = 1.4f;
        [SerializeField] private float wanderFrequency = 6f;
        [SerializeField] private float lifetime = 8f;

        [Header("Shield Repel")]
        [SerializeField] private int shieldBlockBonus = 20;
        [Tooltip("Width (world units) of the real kill zone around the Commander when the bee stings, matching LaneBarrier's narrow per-hit zone so the crowd visibly shrinks by the same amount the audience count does.")]
        [SerializeField] private float impactWidth = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip beeLoopSfx;
        [SerializeField] private AudioClip stingSfx;
        [SerializeField] private AudioClip blockSfx;

        /// Set by BeeHorde right after spawn. A horde bee instantly kills the player on unblocked
        /// contact instead of ticking crowd damage - Shield/Freeze still repel/kill it individually
        /// exactly as before, only unblocked contact is different.
        public bool IsHordeMember { get; set; }

        private Transform target;
        private float age;
        private float wanderSeed;
        private bool peeledOff;
        private bool repelled;
        private float tickTimer;
        private Vector3 driftDir;
        private bool overlapping;

        private void Awake()
        {
            wanderSeed = Random.value * 100f;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            if (beeLoopSfx != null) AudioManager.PlaySfxAt(beeLoopSfx, transform.position);
        }

        private void OnEnable()
        {
            var l = GameObject.FindGameObjectWithTag("Leader");
            if (l != null) target = l.transform;
            driftDir = new Vector3(Random.Range(-1f, -0.3f), Random.Range(-0.5f, 0.5f), 0f).normalized;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var runner = LevelRunner.Instance;
            bool playing = runner != null && runner.State == LevelState.Playing;

            if (repelled)
            {
                transform.position += driftDir * chaseSpeed * 1.6f * Time.deltaTime;
                if (age >= lifetime) Destroy(gameObject);
                return;
            }

            if (!peeledOff && target != null && age < chaseDuration && playing)
            {
                Vector3 to = target.position - transform.position; to.z = 0f;
                Vector3 dir = to.sqrMagnitude > 0.001f ? to.normalized : Vector3.left;
                float w = Mathf.Sin((Time.time + wanderSeed) * wanderFrequency) * wanderAmplitude;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * w;
                transform.position += (dir * chaseSpeed + perp) * Time.deltaTime;
            }
            else
            {
                peeledOff = true;
                transform.position += driftDir * chaseSpeed * 1.2f * Time.deltaTime;
            }

            if (playing && overlapping)
            {
                tickTimer -= Time.deltaTime;
                if (tickTimer <= 0f)
                {
                    tickTimer = tickInterval;
                    ApplyTick(runner);
                }
            }

            if (age >= lifetime) Destroy(gameObject);
        }

        private void ApplyTick(LevelRunner runner)
        {
            if (runner.IsHazardNeutralized(HazardKind.Barrage))
            {
                // Shield instantly repels the bee - it doesn't just tolerate it, it drives it off.
                repelled = true;
                runner.AddScore(shieldBlockBonus);
                runner.RegisterFreezeBlock("SHIELDED THE SWARM");
                AudioManager.PlaySfxAt(blockSfx, transform.position);
                return;
            }

            if (runner.IsFreezeActive)
            {
                // Freeze doubles as an emergency bee-killer: touching a bee while frozen kills it
                // outright instead of just ticking damage, giving Freeze real teeth against a swarm
                // even in levels with no laser walls for it to counter.
                runner.AddScore(shieldBlockBonus);
                runner.RegisterFreezeBlock("FROZE THE SWARM");
                AudioManager.PlaySfxAt(blockSfx, transform.position);
                Destroy(gameObject);
                return;
            }

            if (IsHordeMember)
            {
                // Bee Horde rule: contact instantly ends the run and does not touch score.
                AudioManager.PlaySfxAt(stingSfx, transform.position);
                runner.GameOver();
                return;
            }

            var formation = CrowdFormationController.Instance;
            int killed = formation != null ? formation.KillNear(transform.position, impactWidth, tickKillSize) : tickKillSize;
            runner.RegisterHazardHit(killed);
            AudioManager.PlaySfxAt(stingSfx, transform.position);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Leader")) return;
            overlapping = true;
            tickTimer = 0f; // deal the first tick immediately on contact
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Leader")) overlapping = false;
        }
    }
}
