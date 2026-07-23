using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// Streams every Level 4 hazard at the Commander throughout the run so they appear naturally
    /// across the whole level instead of being clustered: bombs (Level 3), laser walls (Level 1),
    /// gravity gaps (Level 2), stationary bomb-launcher props (Level 3), and the occasional Bee Horde.
    /// Each hazard type has its own independent, difficulty-ramped timer (intervals shorten as
    /// survival time approaches the target); a shared minimum gap between ANY two spawns keeps them
    /// from ever piling up on top of each other. Spawned instances self-destruct after a lifetime so
    /// they don't linger once the Commander has looped past them.
    public class HazardEmitter : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private GameObject beePrefab;
        [SerializeField] private GameObject laserPrefab;
        [SerializeField] private GameObject gapPrefab;
        [SerializeField] private GameObject bombLauncherPrefab;

        [Header("Timing / Ramp")]
        [SerializeField] private float startDelay = 3f;
        [Tooltip("Seconds between bomb spawns at the start of the level.")]
        [SerializeField] private float baseInterval = 3.5f;
        [Tooltip("Seconds between bomb spawns once difficulty is maxed.")]
        [SerializeField] private float minInterval = 1.1f;
        [Tooltip("Survival seconds over which difficulty ramps from 0 to 1.")]
        [SerializeField] private float rampSeconds = 90f;
        [Tooltip("Minimum real-time gap enforced between ANY two hazard spawns (of any type), so " +
                 "different hazard families never pile up on the same reaction window.")]
        [SerializeField] private float globalMinGap = 2.5f;

        [Header("Bomb (Level 3)")]
        [SerializeField] private float bombTopY = 6.5f;
        [Tooltip("Lead ahead of the Commander so a moving player still gets challenged.")]
        [SerializeField] private float bombLeadX = 1.5f;
        [Tooltip("Chance of a second bomb in the same wave once difficulty is past 40%.")]
        [Range(0f, 1f)]
        [SerializeField] private float doubleBombChance = 0.6f;

        [Header("Bomb Launcher Props (Level 3)")]
        [SerializeField] private float launcherBaseInterval = 13f;
        [SerializeField] private float launcherMinInterval = 8f;
        [SerializeField] private float launcherLeadX = 30f;
        [SerializeField] private float launcherLifetime = 26f;

        [Header("Laser (Level 1)")]
        [SerializeField] private float laserBaseInterval = 7.5f;
        [SerializeField] private float laserMinInterval = 4.5f;
        [SerializeField] private float laserLeadX = 16f;
        [SerializeField] private float laserLifetime = 20f;

        [Header("Gravity Gap (Level 2)")]
        [SerializeField] private float gapBaseInterval = 9f;
        [SerializeField] private float gapMinInterval = 5.5f;
        [SerializeField] private float gapLeadX = 14f;
        [SerializeField] private float gapLifetime = 20f;

        [Header("Bee Horde")]
        [Tooltip("Bees now only ever appear as a horde, spawned occasionally - never a constant obstacle.")]
        [SerializeField] private float hordeMinInterval = 12f;
        [SerializeField] private float hordeMaxInterval = 20f;
        [SerializeField] private int hordeSizeMin = 4;
        [SerializeField] private int hordeSizeMax = 7;
        [SerializeField] private float hordeSpread = 1.6f;
        [SerializeField] private float beeSpawnAheadX = 12f;
        [SerializeField] private float beeYRange = 4f;

        private Transform commander;
        private float bombTimer;
        private float launcherTimer;
        private float laserTimer;
        private float gapTimer;
        private float hordeTimer;
        private float lastAnySpawnTime = -999f;

        private void Start()
        {
            bombTimer = startDelay;
            launcherTimer = startDelay + 6f;
            laserTimer = startDelay + 4f;
            gapTimer = startDelay + 9f;
            hordeTimer = startDelay + 14f;
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

            float diff = Mathf.Clamp01(runner.SurvivalTime / Mathf.Max(1f, rampSeconds));

            bombTimer -= Time.deltaTime;
            if (bombTimer <= 0f)
            {
                bombTimer = Mathf.Lerp(baseInterval, minInterval, diff) * Random.Range(0.8f, 1.2f);
                EmitBombWave(diff);
            }

            launcherTimer -= Time.deltaTime;
            if (launcherTimer <= 0f)
            {
                launcherTimer = Mathf.Lerp(launcherBaseInterval, launcherMinInterval, diff) * Random.Range(0.85f, 1.15f);
                SpawnBombLauncher();
            }

            laserTimer -= Time.deltaTime;
            if (laserTimer <= 0f)
            {
                laserTimer = Mathf.Lerp(laserBaseInterval, laserMinInterval, diff) * Random.Range(0.85f, 1.15f);
                SpawnLaser();
            }

            gapTimer -= Time.deltaTime;
            if (gapTimer <= 0f)
            {
                gapTimer = Mathf.Lerp(gapBaseInterval, gapMinInterval, diff) * Random.Range(0.85f, 1.15f);
                SpawnGap();
            }

            hordeTimer -= Time.deltaTime;
            if (hordeTimer <= 0f)
            {
                hordeTimer = Random.Range(hordeMinInterval, hordeMaxInterval);
                SpawnBeeHorde();
            }
        }

        /// True (and reserves the slot) if enough real time has passed since the last hazard spawn of
        /// ANY type - keeps different hazard families from ever landing in the same reaction window.
        private bool TryReserveSpawnSlot()
        {
            if (Time.time - lastAnySpawnTime < globalMinGap) return false;
            lastAnySpawnTime = Time.time;
            return true;
        }

        private void EmitBombWave(float diff)
        {
            if (!TryReserveSpawnSlot()) return;
            SpawnBomb();

            // At higher difficulty, sometimes fire a second bomb in the same wave.
            if (diff > 0.4f && Random.value < diff * doubleBombChance)
            {
                SpawnBomb();
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

        private void SpawnBombLauncher()
        {
            if (bombLauncherPrefab == null) return;
            if (!TryReserveSpawnSlot()) return;
            var go = SpawnAheadOfCommander(bombLauncherPrefab, launcherLeadX);
            if (go != null) Destroy(go, launcherLifetime);
        }

        private void SpawnLaser()
        {
            if (laserPrefab == null) return;
            if (!TryReserveSpawnSlot()) return;
            var go = SpawnAheadOfCommander(laserPrefab, laserLeadX);
            if (go != null) Destroy(go, laserLifetime);
        }

        private void SpawnGap()
        {
            if (gapPrefab == null) return;
            if (!TryReserveSpawnSlot()) return;
            var go = SpawnAheadOfCommander(gapPrefab, gapLeadX);
            if (go != null) Destroy(go, gapLifetime);
        }

        private void SpawnBeeHorde()
        {
            if (beePrefab == null) return;
            if (!TryReserveSpawnSlot()) return;
            float x = commander.position.x + beeSpawnAheadX;
            float y = commander.position.y + Random.Range(-beeYRange, beeYRange);
            int count = Random.Range(hordeSizeMin, hordeSizeMax + 1);
            BeeHorde.Spawn(beePrefab, new Vector3(x, y, 0f), count, hordeSpread);
        }

        private GameObject SpawnAheadOfCommander(GameObject prefab, float leadX)
        {
            float x = commander.position.x + leadX + Random.Range(-1f, 1f);
            float y = commander.position.y;
            var go = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            go.SetActive(true);
            return go;
        }
    }
}
