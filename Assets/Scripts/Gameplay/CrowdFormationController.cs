using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    public class CrowdFormationController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private GameObject crowdUnitPrefab;
        [SerializeField] private Transform commander;
        [SerializeField] private int maxFollowers = 50;

        [Header("Formation")]
        [SerializeField] private int columns = 8;
        [SerializeField] private float columnSpacing = 0.75f;
        [SerializeField] private float rowSpacing = 0.85f;
        [SerializeField] private float rowStartOffset = 1.1f;

        [Header("Follow Feel")]
        [SerializeField] private float baseSmoothTime = 0.18f;
        [SerializeField] private float smoothTimeVariance = 0.06f;
        [SerializeField] private float swaySpeed = 1.5f;
        [SerializeField] private float swayAmount = 0.08f;

        [Header("Death")]
        [SerializeField] private float deathDuration = 0.25f;

        [Header("Freeze Reaction")]
        [SerializeField] private float flinchScale = 1.15f;
        [SerializeField] private float flinchDuration = 0.2f;

        public float BackRowDistance { get; private set; }
        public Vector3 FormationCenter { get; private set; }

        private class CrowdMember
        {
            public Transform t;
            public SpriteRenderer sr;
            public ParticleSystem hitVfx;
            public AudioSource deathAudio;
            public Vector3 velocity;
            public float swayPhase;
            public float smoothTime;
            public bool dying;
        }

        private readonly List<CrowdMember> active = new List<CrowdMember>();
        private int previousAlive;
        private bool wasFrozen;
        private float flinchElapsed = -1f;

        private void Start()
        {
            if (commander == null)
            {
                var leaderGo = GameObject.FindGameObjectWithTag("Leader");
                if (leaderGo != null) commander = leaderGo.transform;
            }

            var runner = LevelRunner.Instance;
            int startCount = runner != null ? runner.StartingCrowdCount : maxFollowers;
            int count = Mathf.Clamp(startCount, 0, maxFollowers);
            previousAlive = count;

            for (int i = 0; i < count; i++)
            {
                SpawnMember(i);
            }

            if (runner != null)
            {
                runner.OnAliveChanged += HandleAliveChanged;
            }

            CommanderController.OnLoopReset += HandleLoopReset;
        }

        private void OnDestroy()
        {
            var runner = LevelRunner.Instance;
            if (runner != null)
            {
                runner.OnAliveChanged -= HandleAliveChanged;
            }

            CommanderController.OnLoopReset -= HandleLoopReset;
        }

        private void HandleLoopReset()
        {
            if (commander == null) return;

            for (int i = 0; i < active.Count; i++)
            {
                var m = active[i];
                if (m.dying) continue;

                m.t.position = commander.position + GetSlotOffset(i);
                m.velocity = Vector3.zero;
            }
        }

        private void SpawnMember(int slotIndex)
        {
            if (crowdUnitPrefab == null) return;

            var go = Instantiate(crowdUnitPrefab, transform);
            var member = new CrowdMember
            {
                t = go.transform,
                sr = go.GetComponentInChildren<SpriteRenderer>(),
                hitVfx = go.GetComponentInChildren<ParticleSystem>(true),
                deathAudio = go.GetComponent<AudioSource>(),
                swayPhase = Random.Range(0f, Mathf.PI * 2f),
                smoothTime = baseSmoothTime + Random.Range(-smoothTimeVariance, smoothTimeVariance)
            };
            go.transform.position = commander != null
                ? commander.position + GetSlotOffset(slotIndex)
                : GetSlotOffset(slotIndex);
            active.Add(member);
        }

        private Vector3 GetSlotOffset(int slotIndex)
        {
            int row = slotIndex / columns;
            int col = slotIndex % columns;
            float centerOffset = (columns - 1) * 0.5f;
            float x = -(rowStartOffset + row * rowSpacing);
            float y = (col - centerOffset) * columnSpacing;
            return new Vector3(x, y, 0f);
        }

        private void Update()
        {
            if (commander == null) return;

            var runner = LevelRunner.Instance;
            bool isFrozen = runner != null && runner.IsFrozen;

            if (isFrozen && !wasFrozen)
            {
                flinchElapsed = 0f;
            }
            wasFrozen = isFrozen;

            if (flinchElapsed >= 0f)
            {
                flinchElapsed += Time.deltaTime;
                if (flinchElapsed > flinchDuration) flinchElapsed = -1f;
            }

            float flinchT = flinchElapsed >= 0f ? flinchElapsed / flinchDuration : -1f;
            float flinchFactor = flinchT >= 0f ? 1f + Mathf.Sin(flinchT * Mathf.PI) * (flinchScale - 1f) : 1f;

            float maxRowDepth = 0f;
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < active.Count; i++)
            {
                var m = active[i];
                if (m.dying) continue;

                Vector3 target = commander.position + GetSlotOffset(i);
                target.y += Mathf.Sin(Time.time * swaySpeed + m.swayPhase) * swayAmount;

                if (!isFrozen)
                {
                    m.t.position = Vector3.SmoothDamp(m.t.position, target, ref m.velocity, m.smoothTime);
                }

                m.t.localScale = Vector3.one * flinchFactor;

                float depth = -GetSlotOffset(i).x;
                if (depth > maxRowDepth) maxRowDepth = depth;
                sum += m.t.position;
            }

            BackRowDistance = maxRowDepth;
            FormationCenter = active.Count > 0 ? sum / active.Count : commander.position;
        }

        private void HandleAliveChanged(int newAliveCount)
        {
            int lost = Mathf.Max(0, previousAlive - newAliveCount);
            previousAlive = newAliveCount;

            for (int i = 0; i < lost; i++)
            {
                CrowdMember victim = null;
                for (int j = active.Count - 1; j >= 0; j--)
                {
                    if (!active[j].dying) { victim = active[j]; break; }
                }
                if (victim == null) break;
                StartCoroutine(KillMember(victim));
            }
        }

        private IEnumerator KillMember(CrowdMember member)
        {
            member.dying = true;

            if (member.hitVfx != null)
            {
                member.hitVfx.transform.SetParent(transform, true);
                member.hitVfx.Play();
                Destroy(member.hitVfx.gameObject, member.hitVfx.main.duration + member.hitVfx.main.startLifetime.constantMax + 0.5f);
            }
            if (member.deathAudio != null && member.deathAudio.clip != null)
            {
                AudioSource.PlayClipAtPoint(member.deathAudio.clip, member.t.position, member.deathAudio.volume);
            }

            float elapsed = 0f;
            Vector3 startScale = member.t.localScale;
            Color startColor = member.sr != null ? member.sr.color : Color.white;

            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / deathDuration;
                member.t.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                if (member.sr != null)
                {
                    var c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, t);
                    member.sr.color = c;
                }
                yield return null;
            }

            active.Remove(member);
            Destroy(member.t.gameObject);
        }
    }
}
