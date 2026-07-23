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
        [Tooltip("Full-body tint applied to every member while shielded by Freeze.")]
        [SerializeField] private Color freezeTintColor = new Color(0.35f, 0.65f, 1f, 1f);

        public static CrowdFormationController Instance { get; private set; }

        public float BackRowDistance { get; private set; }
        public Vector3 FormationCenter { get; private set; }
        /// Full lateral (side-to-side) span of the formation, so a hazard can size its kill zone to
        /// cover every column without needing to know the formation's own layout constants.
        public float FormationWidth => columns * columnSpacing;

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
            public Color baseColor;
        }

        private readonly List<CrowdMember> active = new List<CrowdMember>();
        private bool wasFrozen;
        private float flinchElapsed = -1f;

        private void Awake()
        {
            Instance = this;
        }

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

            for (int i = 0; i < count; i++)
            {
                SpawnMember(i);
            }

            CommanderController.OnLoopReset += HandleLoopReset;
        }

        private void OnDestroy()
        {
            CommanderController.OnLoopReset -= HandleLoopReset;
            if (Instance == this) Instance = null;
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
            member.baseColor = member.sr != null ? member.sr.color : Color.white;
            go.transform.position = commander != null
                ? commander.position + GetSlotOffset(slotIndex)
                : GetSlotOffset(slotIndex);
            active.Add(member);
        }

        /// Spawns back up to count crowd members (capped at maxFollowers), e.g. from a powerup
        /// pickup. Returns how many were actually revived, so the caller can report the real number.
        public int Revive(int count)
        {
            int revived = 0;
            while (revived < count && active.Count < maxFollowers)
            {
                SpawnMember(active.Count);
                revived++;
            }
            // Reuse the freeze-reaction pulse so a heal is unmistakable even when the new member
            // blends into a formation of dozens.
            if (revived > 0) flinchElapsed = 0f;
            return revived;
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

                // Freeze is a shield, not a stop -- the team keeps following through the hazard.
                m.t.position = Vector3.SmoothDamp(m.t.position, target, ref m.velocity, m.smoothTime);

                m.t.localScale = Vector3.one * flinchFactor;
                if (m.sr != null) m.sr.color = isFrozen ? freezeTintColor : m.baseColor;

                float depth = -GetSlotOffset(i).x;
                if (depth > maxRowDepth) maxRowDepth = depth;
                sum += m.t.position;
            }

            BackRowDistance = maxRowDepth;
            FormationCenter = active.Count > 0 ? sum / active.Count : commander.position;
        }

        /// How many members are actually visible right now (excludes ones mid-death-fade). The
        /// authoritative "how many are really left" figure -- LevelRunner.AliveCount is clamped to
        /// this so the audience number can never outlive the crowd actually on screen.
        public int AliveVisualCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < active.Count; i++) if (!active[i].dying) count++;
                return count;
            }
        }

        /// Kills up to maxKills alive members actually standing inside worldBounds and returns how
        /// many were killed, so a hazard reports real casualties instead of a flat guess.
        public int KillOverlapping(Bounds worldBounds, int maxKills)
        {
            int killed = 0;
            for (int i = active.Count - 1; i >= 0 && killed < maxKills; i--)
            {
                var m = active[i];
                if (m.dying || !Contains2D(worldBounds, m.t.position)) continue;
                StartCoroutine(KillMember(m));
                killed++;
            }
            return killed;
        }

        /// Kills up to maxKills members within impactWidth of a hit point (e.g. the Commander's
        /// position), spanning the full formation width so it catches every column at that row.
        /// A hazard's own trigger box is sized for gameplay detection, not blast radius, so this
        /// gives a real, narrow kill zone instead of one wide enough to always hit the whole crowd.
        public int KillNear(Vector3 point, float impactWidth, int maxKills)
        {
            var zone = new Bounds(point, new Vector3(impactWidth, FormationWidth + columnSpacing * 2f, 1f));
            return KillOverlapping(zone, maxKills);
        }

        // X/Y only -- Bounds.Contains also checks Z, which a 2D BoxCollider2D's near-zero depth
        // would fail against sprites sitting off that exact plane.
        private static bool Contains2D(Bounds b, Vector3 p)
        {
            return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
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
