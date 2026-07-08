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

        [Header("Animation")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform glowTransform;
        [SerializeField] private SpriteRenderer glowRenderer;
        [SerializeField] private float idleBobSpeed = 2f;
        [SerializeField] private float idleBobAmount = 0.03f;
        [SerializeField] private float runBobSpeed = 10f;
        [SerializeField] private float runBobAmount = 0.08f;
        [SerializeField] private float glowPulseSpeed = 1.5f;
        [SerializeField] private float glowMinAlpha = 0.35f;
        [SerializeField] private float glowMaxAlpha = 0.65f;

        public Vector3 Velocity { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector3 baseScale;
        private float animTime;
        private float spawnX;

        private void Awake()
        {
            baseScale = transform.localScale;
            spawnX = transform.position.x;
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            bool isFrozen = runner != null && runner.IsFrozen;
            bool canMove = runner != null && runner.State == LevelState.Playing && !isFrozen;

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

            animTime += Time.deltaTime * (isFrozen ? 0.15f : (canMove ? runBobSpeed : idleBobSpeed));
            float bobAmount = canMove ? runBobAmount : idleBobAmount;
            float bob = Mathf.Sin(animTime) * bobAmount;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + bob), baseScale.z);

            if (glowRenderer != null)
            {
                float pulseT = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
                float targetAlpha = isFrozen ? glowMinAlpha : Mathf.Lerp(glowMinAlpha, glowMaxAlpha, pulseT);
                var c = glowRenderer.color;
                c.a = targetAlpha;
                glowRenderer.color = c;
            }
            if (glowTransform != null)
            {
                float scalePulse = 1f + Mathf.Sin(Time.time * glowPulseSpeed) * 0.05f;
                glowTransform.localScale = new Vector3(3f * scalePulse, 3f * scalePulse, 1f);
            }
        }
    }
}
