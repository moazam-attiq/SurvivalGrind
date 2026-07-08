using UnityEngine;
using UnityEngine.InputSystem;

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

        private bool isFrozenByAbility;
        private float freezeTimer;
        private float cooldownTimer;

        /// 0 = ready to use, 1 = just used / fully on cooldown.
        public float CooldownFraction => Mathf.Clamp01(cooldownTimer / cooldown);
        public bool IsReady => cooldownTimer <= 0f && !isFrozenByAbility;

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

            if (runner.State != LevelState.Playing) return;

            bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (pressed && IsReady)
            {
                BeginFreeze(runner);
            }
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
