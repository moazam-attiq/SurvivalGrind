using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    public class GravityShiftAbility : MonoBehaviour
    {
        [Header("Feedback")]
        [SerializeField] private AudioSource gravityShiftLoopAudio;
        [SerializeField] private AudioSource gravityShiftReleaseAudio;
        [SerializeField] private GameObject gravityShiftVfx;
        [SerializeField] private GameObject gravityShiftActiveLabel;

        private GravityLane lastLane = GravityLane.Ground;

        /// Held-key control has no cooldown; HUD cooldown fill just stays full/ready.
        public float CooldownFraction => 0f;
        public bool IsReady => true;

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            GravityLane lane = runner.CurrentGravityLane;
            if (lane == lastLane) return;

            bool wasShifted = lastLane != GravityLane.Ground;
            bool nowShifted = lane != GravityLane.Ground;

            if (nowShifted && !wasShifted)
            {
                if (gravityShiftLoopAudio != null) gravityShiftLoopAudio.Play();
                if (gravityShiftVfx != null) gravityShiftVfx.SetActive(true);
                if (gravityShiftActiveLabel != null) gravityShiftActiveLabel.SetActive(true);
            }
            else if (!nowShifted && wasShifted)
            {
                if (gravityShiftLoopAudio != null) gravityShiftLoopAudio.Stop();
                if (gravityShiftReleaseAudio != null) gravityShiftReleaseAudio.Play();
                if (gravityShiftVfx != null) gravityShiftVfx.SetActive(false);
                if (gravityShiftActiveLabel != null) gravityShiftActiveLabel.SetActive(false);
            }

            lastLane = lane;
        }
    }
}
