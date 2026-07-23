using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// A cluster of ChasingBee instances spawned together as one swarm event. Each bee still respects
    /// its own Shield-repel / Freeze-kill rules individually, but the swarm as a whole is only wiped out
    /// in one shot by the Super Ability (any two abilities active at once - see
    /// LevelRunner.OnSuperAbilityActivated), which also pays a big score bonus and grants the Commander
    /// a few seconds of invulnerability. Unblocked contact with a horde bee instantly ends the run
    /// (see ChasingBee.IsHordeMember).
    public class BeeHorde : MonoBehaviour
    {
        [SerializeField] private int bonusScore = 300;
        [SerializeField] private float invulnerabilitySeconds = 3f;
        [SerializeField] private float burstDuration = 0.6f;

        private readonly List<ChasingBee> members = new List<ChasingBee>();
        private bool subscribed;

        public static BeeHorde Spawn(GameObject beePrefab, Vector3 origin, int count, float spread)
        {
            var go = new GameObject("BeeHorde");
            go.transform.position = origin;
            var horde = go.AddComponent<BeeHorde>();
            horde.Populate(beePrefab, origin, count, spread);
            return horde;
        }

        private void Populate(GameObject beePrefab, Vector3 origin, int count, float spread)
        {
            if (beePrefab == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f);
                var beeGo = Instantiate(beePrefab, origin + offset, Quaternion.identity);
                var bee = beeGo.GetComponent<ChasingBee>();
                if (bee != null)
                {
                    bee.IsHordeMember = true;
                    members.Add(bee);
                }
                beeGo.SetActive(true);
            }
        }

        private void OnEnable()
        {
            if (!subscribed && LevelRunner.Instance != null)
            {
                LevelRunner.Instance.OnSuperAbilityActivated += OnSuperAbility;
                subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (subscribed && LevelRunner.Instance != null)
            {
                LevelRunner.Instance.OnSuperAbilityActivated -= OnSuperAbility;
            }
            subscribed = false;
        }

        private void Update()
        {
            members.RemoveAll(b => b == null);
            // Every bee already died individually (Shield repel / Freeze kill / lifetime) - nothing
            // left for the Super Ability to clear, so this horde is done.
            if (members.Count == 0) Destroy(gameObject);
        }

        private void OnSuperAbility()
        {
            members.RemoveAll(b => b == null);
            if (members.Count == 0) return;

            Vector3 center = transform.position;
            foreach (var bee in members)
            {
                center = bee.transform.position;
                Destroy(bee.gameObject);
            }
            members.Clear();

            var runner = LevelRunner.Instance;
            if (runner != null)
            {
                runner.RegisterBonus(bonusScore, "SWARM OBLITERATED");
                runner.GrantSuperInvulnerability(invulnerabilitySeconds);
            }

            SpawnBurstVfx(center);
        }

        private void SpawnBurstVfx(Vector3 center)
        {
            var go = new GameObject("SwarmBurstVfx");
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 1.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = TargetedBomb.GetRingSprite();
            sr.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            sr.sortingOrder = 100;
            StartCoroutine(BurstRoutine(go, sr));
        }

        private IEnumerator BurstRoutine(GameObject go, SpriteRenderer sr)
        {
            Vector3 startScale = go.transform.localScale;
            Vector3 endScale = startScale * 6f;
            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / burstDuration;
                go.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                var c = sr.color;
                c.a = Mathf.Lerp(0.9f, 0f, t);
                sr.color = c;
                yield return null;
            }
            Destroy(go);
        }
    }
}
