using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    public enum PowerupType { Freeze, Gravity, Shield }

    /// A floating, color-coded pickup: blue = Freeze, green = Gravity Shift, red = Shield Dome.
    /// Grabbing one heals back some crowd/audience and instantly refills its matching ability's
    /// cooldown, so the color tells the player exactly which ability just got cheaper to use again.
    [RequireComponent(typeof(Collider2D))]
    public class PowerupPickup : MonoBehaviour
    {
        private static readonly Color FreezeColor = new Color(0.25f, 0.55f, 1f, 1f);
        private static readonly Color GravityColor = new Color(0.25f, 0.85f, 0.35f, 1f);
        private static readonly Color ShieldColor = new Color(1f, 0.25f, 0.25f, 1f);

        [SerializeField] private PowerupType type = PowerupType.Freeze;
        [SerializeField] private int audienceHeal = 5;

        [Header("Roam / Float")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobAmount = 0.35f;
        [SerializeField] private float spinSpeed = 40f;
        [Tooltip("Breathing scale pulse on top of the bob/spin, so the orb reads as alive and separate from the background instead of a static prop.")]
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmount = 0.18f;

        [Header("Feedback")]
        [SerializeField] private AudioClip pickupSfx;
        [SerializeField] private GameObject pickupVfx;

        private SpriteRenderer sr;
        private float startY;
        private Vector3 baseScale;
        private float pulseSeed;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = type switch
                {
                    PowerupType.Freeze => FreezeColor,
                    PowerupType.Gravity => GravityColor,
                    _ => ShieldColor
                };
                // Random phase so a cluster of pickups doesn't all pulse in lockstep.
                pulseSeed = Random.Range(0f, Mathf.PI * 2f);
            }
            startY = transform.position.y;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            var pos = transform.position;
            pos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.position = pos;
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + pulseSeed) * pulseAmount;
            transform.localScale = baseScale * pulse;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Leader")) return;
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing) return;

            var formation = CrowdFormationController.Instance;
            int healed = formation != null ? formation.Revive(audienceHeal) : audienceHeal;
            runner.HealAudience(healed);

            switch (type)
            {
                case PowerupType.Freeze:
                    FindFirstObjectByType<FreezeAbility>()?.RefillCooldown();
                    break;
                case PowerupType.Gravity:
                    FindFirstObjectByType<GravityShiftAbility>()?.RefillCooldown();
                    break;
                case PowerupType.Shield:
                    FindFirstObjectByType<ShieldDomeAbility>()?.RefillCooldown();
                    break;
            }

            AudioManager.PlaySfxAt(pickupSfx, transform.position);
            if (pickupVfx != null) Instantiate(pickupVfx, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
