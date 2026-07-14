using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// A stationary machine prop mounted beside the road that actively tracks the Commander, rotating
    /// its barrel to face the player, then winds up with a pulsing warning flash for ~1-1.5s before
    /// launching a TargetedBomb at the player's predicted position. Owns its own rotation - any sibling
    /// MachineTurretFX (idle cosmetic sweep) is disabled on Awake so the two never fight over the same
    /// transform. The actual hit/collision/Shield gating logic lives in TargetedBomb itself.
    public class BombLauncherMachine : MonoBehaviour
    {
        [Header("Firing")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private float fireInterval = 4f;
        [SerializeField] private float fireIntervalJitter = 0.8f;
        [SerializeField] private float bombTopY = 6.5f;
        [Tooltip("Lead ahead of the Commander's current position so a moving player is still challenged.")]
        [SerializeField] private float bombLeadX = 1.5f;
        [Tooltip("Only starts firing once the Commander is within this distance ahead of the machine.")]
        [SerializeField] private float activationDistance = 20f;
        [Tooltip("Stops firing for good once the Commander has moved this far past the machine, so a passed " +
                 "launcher doesn't keep lobbing bombs at the player for the rest of the level.")]
        [SerializeField] private float disengageDistance = 45f;

        [Header("Aiming")]
        [Tooltip("Degrees/sec the barrel turns to track the Commander while idle.")]
        [SerializeField] private float idleTurnSpeed = 160f;
        [Tooltip("Degrees/sec the barrel snaps toward the locked fire target during warmup - faster, so the aim reads as deliberate.")]
        [SerializeField] private float warmupTurnSpeed = 320f;

        [Header("Warning Telegraph")]
        [Tooltip("Seconds the machine visibly winds up (rotation locks on, body flashes) before it fires. " +
                 "Keep roughly in sync with TargetedBomb's own ground-reticle telegraphDuration.")]
        [SerializeField] private float warmupDuration = 1.3f;
        [SerializeField] private Color warmupFlashColor = new Color(1f, 0.35f, 0.1f, 1f);
        [SerializeField] private float warmupPulseSpeed = 14f;
        [SerializeField] private float warmupScaleBump = 0.12f;
        [SerializeField] private AudioClip warmupSfx;

        private Transform commander;
        private float timer;
        private bool armed;
        private bool disengaged;
        private bool warmingUp;
        private float lockedTargetX;
        private SpriteRenderer bodyRenderer;
        private Color bodyBaseColor;
        private Vector3 bodyBaseScale;
        private AudioSource audioSource;

        private void Awake()
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer != null) bodyBaseColor = bodyRenderer.color;
            bodyBaseScale = transform.localScale;
            audioSource = GetComponent<AudioSource>();

            // This script owns rotation exclusively - a decorative MachineTurretFX on the same prop
            // would otherwise fight it for transform.eulerAngles.z every frame.
            var cosmeticSweep = GetComponent<MachineTurretFX>();
            if (cosmeticSweep != null) cosmeticSweep.enabled = false;
        }

        private void Start()
        {
            timer = Random.Range(0.5f, fireInterval);
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;
            if (disengaged) return;

            if (commander == null)
            {
                var l = GameObject.FindGameObjectWithTag("Leader");
                if (l != null) commander = l.transform;
                else return;
            }

            if (!armed)
            {
                if (commander.position.x < transform.position.x - activationDistance) return;
                armed = true;
                // Give the first shot a clean, bounded lead-in instead of possibly inheriting a stale
                // multi-second value that was quietly aging while the machine sat unarmed.
                timer = Mathf.Min(timer, fireInterval);
            }

            if (commander.position.x > transform.position.x + disengageDistance)
            {
                disengaged = true;
                warmingUp = false;
                if (bodyRenderer != null)
                {
                    bodyRenderer.color = bodyBaseColor;
                    transform.localScale = bodyBaseScale;
                }
                return;
            }

            if (!warmingUp)
            {
                timer -= Time.deltaTime;
                RotateToward(commander.position, idleTurnSpeed);

                if (timer <= warmupDuration)
                {
                    warmingUp = true;
                    lockedTargetX = commander.position.x + bombLeadX + Random.Range(-0.5f, 1.5f);
                    if (warmupSfx != null && audioSource != null) audioSource.PlayOneShot(warmupSfx);
                }
            }
            else
            {
                timer -= Time.deltaTime;
                RotateToward(new Vector3(lockedTargetX, bombTopY, transform.position.z), warmupTurnSpeed);

                float remaining = Mathf.Max(0f, timer);
                float pulse = (Mathf.Sin(Time.time * warmupPulseSpeed) + 1f) * 0.5f;
                float intensity = 1f - Mathf.Clamp01(remaining / warmupDuration);
                if (bodyRenderer != null)
                {
                    bodyRenderer.color = Color.Lerp(bodyBaseColor, warmupFlashColor, intensity * pulse);
                }
                transform.localScale = bodyBaseScale * (1f + warmupScaleBump * intensity * pulse);

                if (timer <= 0f)
                {
                    Fire();
                    warmingUp = false;
                    if (bodyRenderer != null) bodyRenderer.color = bodyBaseColor;
                    transform.localScale = bodyBaseScale;
                    timer = fireInterval + Random.Range(-fireIntervalJitter, fireIntervalJitter);
                }
            }
        }

        private void RotateToward(Vector3 worldTarget, float turnSpeed)
        {
            Vector2 dir = ((Vector2)worldTarget - (Vector2)transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            // Sprite convention matches MachineTurretFX: at z=0 the sprite's "up" points along +Y, so the
            // up vector after a z-rotation is (-sin z, cos z) - solve that for the target direction.
            float targetZ = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
            var e = transform.localEulerAngles;
            e.z = Mathf.MoveTowardsAngle(e.z, targetZ, turnSpeed * Time.deltaTime);
            transform.localEulerAngles = e;
        }

        private void Fire()
        {
            if (bombPrefab == null) return;
            var go = Instantiate(bombPrefab);
            var bomb = go.GetComponent<TargetedBomb>();
            if (bomb != null) bomb.Init(lockedTargetX, bombTopY);
            go.SetActive(true);
        }
    }
}
