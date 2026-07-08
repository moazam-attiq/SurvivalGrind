using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    public class FinishPortalTrigger : MonoBehaviour
    {
        private bool triggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered) return;
            if (!other.CompareTag("Leader")) return;

            var runner = LevelRunner.Instance;
            if (runner == null) return;
            if (runner.SurvivalTime < runner.SurvivalTarget) return;

            triggered = true;
            runner.GoToVictory();
        }
    }
}
