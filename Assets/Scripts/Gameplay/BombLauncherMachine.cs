using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// A stationary machine prop mounted beside the road that periodically launches a TargetedBomb
    /// at the Commander's predicted position. Purely a spawner - the actual hit/collision/Shield
    /// gating logic lives in TargetedBomb itself.
    public class BombLauncherMachine : MonoBehaviour
    {
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private float fireInterval = 4f;
        [SerializeField] private float fireIntervalJitter = 0.8f;
        [SerializeField] private float bombTopY = 6.5f;
        [Tooltip("Lead ahead of the Commander's current position so a moving player is still challenged.")]
        [SerializeField] private float bombLeadX = 1.5f;
        [Tooltip("Only starts firing once the Commander is within this distance, so machines far down the road stay dormant until relevant.")]
        [SerializeField] private float activationDistance = 20f;

        private Transform commander;
        private float timer;
        private bool armed;

        private void Start()
        {
            timer = Random.Range(0.5f, fireInterval);
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;

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
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = fireInterval + Random.Range(-fireIntervalJitter, fireIntervalJitter);
                Fire();
            }
        }

        private void Fire()
        {
            if (bombPrefab == null || commander == null) return;
            var go = Instantiate(bombPrefab);
            float tx = commander.position.x + bombLeadX + Random.Range(-0.5f, 1.5f);
            var bomb = go.GetComponent<TargetedBomb>();
            if (bomb != null) bomb.Init(tx, bombTopY);
            go.SetActive(true);
        }
    }
}
