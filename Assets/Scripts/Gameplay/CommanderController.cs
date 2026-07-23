using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalGrind.Gameplay
{
    public class CommanderController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float forwardSpeed = 4.5f;
        [SerializeField] private float nudgeSpeedUpMultiplier = 1.4f;
        [SerializeField] private float nudgeSlowDownMultiplier = 0.55f;
        [SerializeField] private float laneShiftDistance = 6f;
        [SerializeField] private float laneShiftSpeed = 14f;

        [Header("Gravity Lane Control")]
        [Tooltip("Disable for Freeze-only levels where gravity lanes shouldn't be player-controlled.")]
        [SerializeField] private bool enableGravityLaneControl = true;
        [Tooltip("When true, this controller stops driving the Commander's vertical position entirely -- " +
                 "another component (e.g. GravityShiftAbility's flip logic) owns Y. Forward motion, animation " +
                 "and freeze tint still run here.")]
        [SerializeField] private bool relinquishVertical = false;

        [Header("Time Loop")]
        [Tooltip("When enabled, the run rewinds to a snapshot position instead of resetting to spawn on an interval.")]
        [SerializeField] private bool useSnapshotLoop = false;
        [SerializeField] private float snapshotTime = 3f;
        [SerializeField] private float loopTriggerTime = 25f;

        [Header("Position Loop (seamless)")]
        [Tooltip("When enabled, the run hard-snaps back to loopResetPosition the instant the player reaches loopTriggerX. " +
                 "Meant for a periodic level so the snap is visually undetectable. Keep loopResetPosition.x safely " +
                 "before the first hazard's warning zone (hazard.x - triggerDistance), never on top of or past it.")]
        [SerializeField] private bool usePositionLoop = true;
        [SerializeField] private Vector2 loopResetPosition = new Vector2(6f, -0.6f);
        [SerializeField] private float loopTriggerX = 122.6f;

        public static event System.Action OnLoopReset;

        [Header("Animation")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [Tooltip("Static neon glow child (e.g. LeaderGlow) that used to render permanently. Hidden by default now -- " +
                 "ShieldDomeAbility reuses this same object, expanding it into the visible dome only while S is held.")]
        [SerializeField] private GameObject permanentGlow;
        [SerializeField] private float idleBobSpeed = 2f;
        [SerializeField] private float idleBobAmount = 0.03f;
        [SerializeField] private float runBobSpeed = 10f;
        [SerializeField] private float runBobAmount = 0.08f;
        [Header("Freeze Visual")]
        [Tooltip("Full-body tint applied while shielded by Freeze, so it's unmistakable the team is invulnerable.")]
        [SerializeField] private Color freezeTintColor = new Color(0.35f, 0.65f, 1f, 1f);

        public Vector3 Velocity { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector3 baseScale;
        private float animTime;
        private float spawnX;
        private float spawnY;
        private int lastLoopIndex;
        private Vector3 snapshotPosition;
        private bool snapshotCaptured;
        private Color normalBodyColor;

        private void Awake()
        {
            baseScale = transform.localScale;
            spawnX = transform.position.x;
            spawnY = transform.position.y;
            if (bodyRenderer != null) normalBodyColor = bodyRenderer.color;
            if (permanentGlow != null) permanentGlow.SetActive(false);

            // Movement here is a direct transform assignment, not physics-driven, so Unity's default
            // Discrete detection can let a fast-enough pass (e.g. the right-arrow speed boost) tunnel
            // straight through a thin hazard trigger without ever firing OnTriggerEnter2D. Continuous
            // mode makes the physics engine sweep the path instead of just checking the endpoint.
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            bool isFrozen = runner != null && runner.IsFrozen;
            // Freeze is a shield, not a stop -- the team keeps running through the hazard while invulnerable.
            bool canMove = runner != null && runner.State == LevelState.Playing;

            float speedMultiplier = 1f;
            if (canMove && Keyboard.current != null)
            {
                if (Keyboard.current.rightArrowKey.isPressed) speedMultiplier = nudgeSpeedUpMultiplier;
                else if (Keyboard.current.leftArrowKey.isPressed) speedMultiplier = nudgeSlowDownMultiplier;
            }
            IsMoving = canMove;

            if (canMove)
            {
                Vector3 pos = transform.position;
                float speed = forwardSpeed * speedMultiplier;
                pos.x += speed * Time.deltaTime;
                if (pos.x < spawnX) pos.x = spawnX;
                Velocity = new Vector3(speed, 0f, 0f);
                transform.position = pos;
            }
            else
            {
                Velocity = Vector3.zero;
            }

            if (runner != null && runner.State == LevelState.Playing)
            {
                if (usePositionLoop)
                {
                    // Seamless position loop: the instant the player crosses the trigger X, hard-snap
                    // straight back to the reset position. On a periodic level the camera + crowd snap
                    // together (via OnLoopReset) so the jump is visually undetectable.
                    if (transform.position.x >= loopTriggerX)
                    {
                        transform.position = new Vector3(loopResetPosition.x, loopResetPosition.y, transform.position.z);
                        Velocity = Vector3.zero;
                        OnLoopReset?.Invoke();
                    }
                }
                else if (useSnapshotLoop)
                {
                    if (!snapshotCaptured && runner.SurvivalTime >= snapshotTime)
                    {
                        snapshotCaptured = true;
                        snapshotPosition = transform.position;
                    }

                    if (snapshotCaptured)
                    {
                        int currentLoopIndex = Mathf.FloorToInt(runner.SurvivalTime / loopTriggerTime);
                        if (currentLoopIndex > lastLoopIndex)
                        {
                            lastLoopIndex = currentLoopIndex;
                            transform.position = snapshotPosition;
                            OnLoopReset?.Invoke();
                        }
                    }
                }
                // else: neither loop mode is enabled -- run straight through with no periodic reset.
            }

            // When another component owns Y (e.g. Level 2's gravity-flip), skip all vertical/lane control here.
            if (runner != null && !relinquishVertical)
            {
                GravityLane lane = GravityLane.Ground;
                if (enableGravityLaneControl && canMove && Keyboard.current != null)
                {
                    if (Keyboard.current.upArrowKey.isPressed) lane = GravityLane.Upper;
                    else if (Keyboard.current.downArrowKey.isPressed) lane = GravityLane.Lower;
                }
                runner.SetGravityLane(lane);

                float targetY = lane switch
                {
                    GravityLane.Upper => spawnY + laneShiftDistance,
                    GravityLane.Lower => spawnY - laneShiftDistance,
                    _ => spawnY
                };
                Vector3 pos = transform.position;
                pos.y = Mathf.MoveTowards(pos.y, targetY, laneShiftSpeed * Time.deltaTime);
                transform.position = pos;
            }

            animTime += Time.deltaTime * (isFrozen ? 0.15f : (canMove ? runBobSpeed : idleBobSpeed));
            float bobAmount = canMove ? runBobAmount : idleBobAmount;
            float bob = Mathf.Sin(animTime) * bobAmount;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + bob), baseScale.z);

            if (bodyRenderer != null)
            {
                bodyRenderer.color = isFrozen ? freezeTintColor : normalBodyColor;
            }
        }
    }
}