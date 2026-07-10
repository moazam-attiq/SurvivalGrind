using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    public class FreezeAbility : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float freezeDuration = 1.3f;
        [SerializeField] private float cooldown = 3.5f;

        [Header("Feedback")]
        [SerializeField] private AudioSource freezeLoopAudio;
        [SerializeField] private AudioSource freezeReleaseAudio;
        [SerializeField] private GameObject shieldDomeVfx;
        [SerializeField] private GameObject freezeActiveLabel;

        [Header("UI (optional)")]
        [SerializeField] private Button freezeButton;

        private bool isFrozenByAbility;
        private float freezeTimer;
        private float cooldownTimer;

        /// 0 = ready to use, 1 = actively frozen or fully on cooldown.
        public float CooldownFraction => isFrozenByAbility ? 1f : Mathf.Clamp01(cooldownTimer / cooldown);
        public bool IsReady => cooldownTimer <= 0f && !isFrozenByAbility;

        private void Awake()
        {
            if (freezeButton != null) freezeButton.onClick.AddListener(TryActivate);
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (isFrozenByAbility)
            {
                freezeTimer -= Time.deltaTime;
                if (freezeTimer <= 0f) EndFreeze(runner);
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // The freeze mechanic is only usable until the survival goal is complete.
            if (freezeButton != null) freezeButton.interactable = !runner.SurvivalComplete;

            if (runner.State != LevelState.Playing) return;

            bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (pressed) TryActivate();
        }

        public void TryActivate()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;
            if (runner.State != LevelState.Playing) return;
            if (runner.SurvivalComplete) return;
            if (!IsReady) return;

            BeginFreeze(runner);
        }

        private void BeginFreeze(LevelRunner runner)
        {
            isFrozenByAbility = true;
            freezeTimer = freezeDuration;
            runner.SetFrozen(true);

            if (freezeLoopAudio != null) freezeLoopAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(true);
            if (freezeActiveLabel != null) freezeActiveLabel.SetActive(true);
        }

        private void EndFreeze(LevelRunner runner)
        {
            isFrozenByAbility = false;
            cooldownTimer = cooldown;
            runner.SetFrozen(false);

            if (freezeLoopAudio != null) freezeLoopAudio.Stop();
            if (freezeReleaseAudio != null) freezeReleaseAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(false);
            if (freezeActiveLabel != null) freezeActiveLabel.SetActive(false);
        }
    }
}
