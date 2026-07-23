using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    public enum LevelState
    {
        Intro,
        Playing,
        Paused,
        Victory,
        Defeat
    }

    public enum GravityLane
    {
        Ground,
        Upper,
        Lower
    }

    /// The category a hazard belongs to. Each category is neutralized by exactly ONE ability -
    /// there is no shared invulnerability. See <see cref="LevelRunner.IsHazardNeutralized"/>.
    public enum HazardKind
    {
        /// Laser walls - neutralized ONLY by Freeze (F).
        Laser,
        /// Gaps, moving platforms, broken roads, moving barriers - cleared ONLY by Gravity Shift (G).
        Gravity,
        /// Bombs, spike corridors, bees, aerial strikes - blocked ONLY by Shield Dome (S).
        Barrage
    }

    public class LevelRunner : MonoBehaviour
    {
        public static LevelRunner Instance { get; private set; }
        public static string LastPlayedLevelScene { get; private set; } = "Level 1";

        [Header("Config")]
        [SerializeField] private float survivalTarget = 120f;
        [SerializeField] private int safePassBonus = 50;
        [SerializeField] private float comboMultiplierStep = 0.2f;
        [SerializeField] private float comboMultiplierCap = 2f;
        [SerializeField] private int hazardPenalty = 80;
        [SerializeField] private int startingCrowdCount = 50;
        [SerializeField] private float scrollSpeed = 4f;
        [SerializeField] private int freezeBlockBonus = 40;
        [SerializeField] private int gravityShiftPassBonus = 40;
        [Tooltip("If the crowd is wiped out but the Commander survives to the timer, a score at or above this still counts as a win instead of a defeat.")]
        [SerializeField] private int minScoreToSurviveWithoutCrowd = 100;
        [Tooltip("When enabled, reaching the timer always requires Score >= minScoreToSurviveWithoutCrowd to win, even if the crowd is still fully alive.")]
        [SerializeField] private bool requireMinScoreToWin = false;

        [Header("Progression")]
        [SerializeField] private string nextLevelSceneName = "Level 2";
        [SerializeField] private int levelNumber = 1;
        [Tooltip("Minimum Score the player must have when they reach the finish for the NEXT level to unlock. " +
                 "Per design this is 150 for Levels 1-3. Winning with a lower score still shows Victory but does " +
                 "not unlock the following level.")]
        [SerializeField] private int unlockScoreThreshold = 150;
        [Tooltip("Mark the final level (Level 4). On victory this shows finalVictoryPanel instead of the normal " +
                 "victory panel and does not try to unlock a non-existent next level.")]
        [SerializeField] private bool isFinalLevel = false;

        [Header("Panels")]
        [SerializeField] private GameObject victoryPanel;
        [Tooltip("Optional. Shown instead of victoryPanel when isFinalLevel is true (the Final Victory / congratulations screen).")]
        [SerializeField] private GameObject finalVictoryPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip crowdLossSfx;
        [SerializeField] private AudioClip scoreSfx;
        [SerializeField] private AudioClip victoryStingerSfx;
        [SerializeField] private AudioClip gameOverStingerSfx;

        public LevelState State { get; private set; } = LevelState.Intro;
        public int Score { get; private set; }
        public int AliveCount { get; private set; }
        public float SurvivalTime { get; private set; }
        // Strict, independent ability states. Each hazard checks ONLY the one that neutralizes it.
        public bool IsFreezeActive { get; private set; }
        public bool IsGravityActive { get; private set; }
        public bool IsShieldActive { get; private set; }
        /// Convenience for visuals only (Commander invuln tint / animation) - never used to neutralize a hazard.
        public bool IsFrozen => IsFreezeActive || IsGravityActive || IsShieldActive;
        public GravityLane CurrentGravityLane { get; private set; } = GravityLane.Ground;
        public bool SurvivalComplete { get; private set; }
        public float SurvivalTarget => survivalTarget;
        public int UnlockScoreThreshold => unlockScoreThreshold;
        /// Set once GoToVictory runs: whether this win's score cleared unlockScoreThreshold and
        /// actually unlocked the next level. Lets the victory screen explain why the next trial
        /// might still be locked despite winning.
        public bool NextLevelUnlocked { get; private set; }
        /// 0 at the start of the run, 1 once the survival target is reached. Hazards use this to
        /// shrink their telegraph/reaction window over time so late-run play demands quicker reflexes.
        public float DifficultyRamp01 => survivalTarget > 0f ? Mathf.Clamp01(SurvivalTime / survivalTarget) : 0f;
        public int StartingCrowdCount => startingCrowdCount;
        public float ScrollSpeed => scrollSpeed;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnAliveChanged;
        public event Action<float, float> OnTimeChanged;
        public event Action<string> OnNotification;
        public event Action OnPortalReady;
        /// Fired by a hazard the instant it enters its warning/telegraph phase, so the HUD can flash
        /// the one ability that actually neutralizes it -- telling the player "use it now".
        public event Action<HazardKind> OnHazardWarning;

        /// Fired the instant a SECOND ability becomes active while another is still active (any pair --
        /// Freeze+Gravity, Freeze+Shield, Gravity+Shield). This is the Level 4 Bee Horde "Super Ability":
        /// any two abilities running at once, not a fixed combo. Only fires once per overlap (not every
        /// frame the overlap continues) - see RecomputeSuperAbility.
        public event Action OnSuperAbilityActivated;

        public void NotifyHazardWarning(HazardKind kind) => OnHazardWarning?.Invoke(kind);

        private int consecutiveSafePasses;
        private bool superAbilityWasActive;

        /// True for a few seconds after the Super Ability fires. While true, every hazard hit and the
        /// Commander-exposed instant-kill path are no-ops - the single choke points every hazard already
        /// funnels through (RegisterHazardHit / GameOver), so no individual hazard script needs to know
        /// about it.
        public bool IsSuperInvulnerable { get; private set; }
        private float superInvulnTimer;

        public void GrantSuperInvulnerability(float seconds)
        {
            superInvulnTimer = Mathf.Max(superInvulnTimer, seconds);
            IsSuperInvulnerable = true;
        }

        private void RecomputeSuperAbility()
        {
            int active = (IsFreezeActive ? 1 : 0) + (IsGravityActive ? 1 : 0) + (IsShieldActive ? 1 : 0);
            if (active >= 2)
            {
                if (!superAbilityWasActive)
                {
                    superAbilityWasActive = true;
                    OnSuperAbilityActivated?.Invoke();
                }
            }
            else
            {
                superAbilityWasActive = false;
            }
        }

        private void Awake()
        {
            // Guard against a duplicate runner (e.g. a leftover from an additive load) stealing the singleton.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("LevelRunner: a second instance was found; destroying the duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AliveCount = Mathf.Max(0, startingCrowdCount);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginGameplay()
        {
            State = LevelState.Playing;
            OnAliveChanged?.Invoke(AliveCount);
            OnScoreChanged?.Invoke(Score);
            OnTimeChanged?.Invoke(0f, survivalTarget);
        }

        private void Update()
        {
            if (State != LevelState.Playing) return;

            if (IsSuperInvulnerable)
            {
                superInvulnTimer -= Time.deltaTime;
                if (superInvulnTimer <= 0f) IsSuperInvulnerable = false;
            }

            SurvivalTime += Time.deltaTime;
            OnTimeChanged?.Invoke(SurvivalTime, survivalTarget);

            if (!SurvivalComplete && SurvivalTime >= survivalTarget)
            {
                SurvivalComplete = true;

                bool scoreTooLow = Score < minScoreToSurviveWithoutCrowd;
                if ((requireMinScoreToWin && scoreTooLow) || (AliveCount <= 0 && scoreTooLow))
                {
                    // Either this level always requires the score threshold, or the crowd
                    // didn't make it and the score doesn't make up for it - not a win.
                    GameOver();
                }
                else
                {
                    // Either the crowd made it, or the Commander survived alone with a decent score.
                    OnPortalReady?.Invoke();
                    GoToVictory();
                }
            }
        }

        public void SetFreezeActive(bool active) { IsFreezeActive = active; RecomputeSuperAbility(); }
        public void SetGravityActive(bool active) { IsGravityActive = active; RecomputeSuperAbility(); }
        public void SetShieldActive(bool active) { IsShieldActive = active; RecomputeSuperAbility(); }

        /// True only when the ability matching this hazard's kind is currently active.
        /// This is the single gate every hazard must use - there is no shared invulnerability.
        public bool IsHazardNeutralized(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Laser: return IsFreezeActive;
                case HazardKind.Gravity: return IsGravityActive;
                case HazardKind.Barrage: return IsShieldActive;
                default: return false;
            }
        }

        public void SetGravityLane(GravityLane lane)
        {
            CurrentGravityLane = lane;
        }

        public void AddScore(int delta)
        {
            Score = Mathf.Max(0, Score + delta);
            OnScoreChanged?.Invoke(Score);
        }

        public void RegisterSafePass()
        {
            if (State != LevelState.Playing) return;

            consecutiveSafePasses++;
            float multiplier = Mathf.Min(1f + comboMultiplierStep * (consecutiveSafePasses - 1), comboMultiplierCap);
            int bonus = Mathf.RoundToInt(safePassBonus * multiplier);
            AddScore(bonus);

            string label = consecutiveSafePasses > 1
                ? $"SAFE PASS +{bonus} (x{multiplier:0.0})"
                : $"SAFE PASS +{bonus}";
            OnNotification?.Invoke(label);
            AudioManager.PlaySfx(scoreSfx);
        }

        public void RegisterFreezeBlock(string label = "FROZE THE THREAT")
        {
            if (State != LevelState.Playing) return;

            AddScore(freezeBlockBonus);
            OnNotification?.Invoke($"{label} +{freezeBlockBonus}");
            AudioManager.PlaySfx(scoreSfx);
        }

        public void RegisterGravityShiftPass()
        {
            if (State != LevelState.Playing) return;

            AddScore(gravityShiftPassBonus);
            OnNotification?.Invoke($"GRAVITY SHIFT +{gravityShiftPassBonus}");
            AudioManager.PlaySfx(scoreSfx);
        }

        /// Generic scoring event for bonuses whose amount varies per-occurrence (e.g. the Shield
        /// ultimate's per-target blast bonus), unlike RegisterFreezeBlock/RegisterGravityShiftPass
        /// which always award their own fixed configured amount.
        public void RegisterBonus(int amount, string label)
        {
            if (State != LevelState.Playing) return;

            AddScore(amount);
            OnNotification?.Invoke($"{label} +{amount}");
            AudioManager.PlaySfx(scoreSfx);
        }

        /// Restores crowd/audience, e.g. from a powerup pickup. Capped at the starting count so a
        /// heal can't out-grow the formation's original layout.
        public void HealAudience(int amount)
        {
            if (State != LevelState.Playing || amount <= 0) return;

            int healed = Mathf.Min(amount, startingCrowdCount - AliveCount);
            if (healed <= 0) return;

            AliveCount += healed;
            OnAliveChanged?.Invoke(AliveCount);
            OnNotification?.Invoke($"+{healed} AUDIENCE");
        }

        public void RegisterHazardHit(int killCount)
        {
            if (State != LevelState.Playing) return;
            if (IsSuperInvulnerable) return;

            bool commanderExposed = AliveCount <= 0;
            consecutiveSafePasses = 0;

            if (commanderExposed)
            {
                // The crowd is already gone - this hit strikes the Commander directly.
                AddScore(-hazardPenalty);
                OnNotification?.Invoke("COMMANDER DOWN!");
                AudioManager.PlaySfx(crowdLossSfx);
                GameOver();
                return;
            }

            AliveCount = Mathf.Max(0, AliveCount - killCount);
            // Defensive clamp: never show more audience than is actually visible on screen. A hit
            // whose blast zone doesn't quite line up with where the (smoothed, trailing) crowd
            // physically is can under-report the visual kill; this keeps the number honest instead
            // of getting stuck above zero after the crowd has visibly run out.
            var formation = CrowdFormationController.Instance;
            if (formation != null) AliveCount = Mathf.Min(AliveCount, formation.AliveVisualCount);
            AddScore(-hazardPenalty);
            OnAliveChanged?.Invoke(AliveCount);
            string label = killCount > 0
                ? (AliveCount > 0 ? $"-{killCount} CROWD LOST!" : "CROWD WIPED OUT!")
                : "HAZARD HIT!";
            OnNotification?.Invoke(label);
            AudioManager.PlaySfx(crowdLossSfx);
        }

        public void GoToVictory()
        {
            // Guard: only a live, playing level can transition to Victory. This prevents a duplicate
            // Victory (or a Victory firing after a Defeat) from ever running twice.
            if (State != LevelState.Playing) return;

            State = LevelState.Victory;
            Time.timeScale = 1f;
            if (musicSource != null) musicSource.Stop();
            AudioManager.PlaySfx(victoryStingerSfx);

            // Show the correct end screen. The final level gets its own congratulations panel.
            GameObject panelToShow = (isFinalLevel && finalVictoryPanel != null) ? finalVictoryPanel : victoryPanel;
            if (panelToShow != null) panelToShow.SetActive(true);

            // Unlock progression. Reaching the finish only unlocks the next level if the score cleared the
            // threshold (150 for Levels 1-3). The final level has nothing after it to unlock.
            NextLevelUnlocked = !isFinalLevel && Score >= unlockScoreThreshold;
            if (NextLevelUnlocked)
            {
                SurvivalGrind.UI.LevelSelectController.SetUnlockedLevelCount(levelNumber + 1);
            }
        }

        public void GameOver()
        {
            if (State != LevelState.Playing) return;
            if (IsSuperInvulnerable) return;

            State = LevelState.Defeat;
            Time.timeScale = 1f;
            if (musicSource != null) musicSource.Stop();
            AudioManager.PlaySfx(gameOverStingerSfx);
            LastPlayedLevelScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene("GameOver");
        }

        public void Pause()
        {
            if (State != LevelState.Playing) return;

            State = LevelState.Paused;
            Time.timeScale = 0f;
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        public void Resume()
        {
            if (State != LevelState.Paused) return;

            State = LevelState.Playing;
            Time.timeScale = 1f;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoNextLevel()
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(nextLevelSceneName) || Application.CanStreamedLevelBeLoaded(nextLevelSceneName) == false)
            {
                Debug.LogWarning($"LevelRunner: next level scene '{nextLevelSceneName}' is missing or not in Build Settings; returning Home.");
                SceneManager.LoadScene("MainMenu");
                return;
            }
            SceneManager.LoadScene(nextLevelSceneName);
        }

        public void GoHome()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
