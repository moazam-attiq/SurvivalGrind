using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// Streams Barrage hazards (bombs + bees) at the Commander throughout the run so hazards appear
    /// naturally across the whole level instead of being clustered. Difficulty ramps with survival time:
    /// intervals shorten and double-spawns become more likely. Bombs lock onto the player's current X.
    public class HazardEmitter : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private GameObject beePrefab;

        [Header("Timing / Ramp")]
        [SerializeField] private float startDelay = 3f;
        [Tooltip("Seconds between spawns at the start of the level.")]
        [SerializeField] private float baseInterval = 3.5f;
        [Tooltip("Seconds between spawns once difficulty is maxed.")]
        [SerializeField] private float minInterval = 1.1f;
        [Tooltip("Survival seconds over which difficulty ramps from 0 to 1.")]
        [SerializeField] private float rampSeconds = 90f;

        [Header("Bomb")]
        [SerializeField] private float bombTopY = 6.5f;
        [Tooltip("Lead ahead of the Commander so a moving player still gets challenged.")]
        [SerializeField] private float bombLeadX = 1.5f;

        [Header("Bee")]
        [SerializeField] private float beeSpawnAheadX = 12f;
        [SerializeField] private float beeYRange = 4f;

        private Transform commander;
        private float timer;

        private void Start()
        {
            timer = startDelay;
            AcquireCommander();
        }

        private void AcquireCommander()
        {
            if (commander != null) return;
            var l = GameObject.FindGameObjectWithTag("Leader");
            if (l != null) commander = l.transform;
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;
            AcquireCommander();
            if (commander == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            float diff = Mathf.Clamp01(runner.SurvivalTime / Mathf.Max(1f, rampSeconds));
            timer = Mathf.Lerp(baseInterval, minInterval, diff) * Random.Range(0.8f, 1.2f);
            EmitWave(diff);
        }

        private void EmitWave(float diff)
        {
            bool bombFirst = Random.value < 0.5f;
            if (bombFirst) SpawnBomb(); else SpawnBee();

            // At higher difficulty, sometimes fire a second, different hazard in the same wave.
            if (diff > 0.4f && Random.value < diff * 0.6f)
            {
                if (bombFirst) SpawnBee(); else SpawnBomb();
            }
        }

        private void SpawnBomb()
        {
            if (bombPrefab == null) return;
            var go = Instantiate(bombPrefab);
            float tx = commander.position.x + bombLeadX + Random.Range(-0.5f, 1.5f);
            var bomb = go.GetComponent<TargetedBomb>();
            if (bomb != null) bomb.Init(tx, bombTopY);
            else go.transform.position = new Vector3(tx, bombTopY, 0f);
            go.SetActive(true);
        }

        private void SpawnBee()
        {
            if (beePrefab == null) return;
            var go = Instantiate(beePrefab);
            float x = commander.position.x + beeSpawnAheadX;
            float y = commander.position.y + Random.Range(-beeYRange, beeYRange);
            go.transform.position = new Vector3(x, y, 0f);
            go.SetActive(true);
        }
    }
}
