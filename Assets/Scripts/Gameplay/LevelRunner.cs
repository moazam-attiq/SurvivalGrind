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

        [Header("Progression")]
        [SerializeField] private string nextLevelSceneName = "Level 2";
        [SerializeField] private int levelNumber = 1;

        [Header("Panels")]
        [SerializeField] private GameObject victoryPanel;
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
        public bool IsFrozen { get; private set; }
        public GravityLane CurrentGravityLane { get; private set; } = GravityLane.Ground;
        public bool SurvivalComplete { get; private set; }
        public float SurvivalTarget => survivalTarget;
        public int StartingCrowdCount => startingCrowdCount;
        public float ScrollSpeed => scrollSpeed;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnAliveChanged;
        public event Action<float, float> OnTimeChanged;
        public event Action<string> OnNotification;
        public event Action OnPortalReady;

        private int consecutiveSafePasses;

        private void Awake()
        {
            Instance = this;
            AliveCount = startingCrowdCount;
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

            SurvivalTime += Time.deltaTime;
            OnTimeChanged?.Invoke(SurvivalTime, survivalTarget);

            if (!SurvivalComplete && SurvivalTime >= survivalTarget)
            {
                SurvivalComplete = true;
                OnPortalReady?.Invoke();
                GoToVictory();
            }
        }

        public void SetFrozen(bool frozen)
        {
            IsFrozen = frozen;
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

        public void RegisterFreezeBlock()
        {
            if (State != LevelState.Playing) return;

            AddScore(freezeBlockBonus);
            OnNotification?.Invoke($"FROZE THE THREAT +{freezeBlockBonus}");
            AudioManager.PlaySfx(scoreSfx);
        }

        public void RegisterGravityShiftPass()
        {
            if (State != LevelState.Playing) return;

            AddScore(gravityShiftPassBonus);
            OnNotification?.Invoke($"GRAVITY SHIFT +{gravityShiftPassBonus}");
            AudioManager.PlaySfx(scoreSfx);
        }

        public void RegisterHazardHit(int killCount)
        {
            if (State != LevelState.Playing) return;

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
            AddScore(-hazardPenalty);
            OnAliveChanged?.Invoke(AliveCount);
            OnNotification?.Invoke(AliveCount > 0 ? $"-{killCount} CROWD LOST!" : "CROWD WIPED OUT!");
            AudioManager.PlaySfx(crowdLossSfx);
        }

        public void GoToVictory()
        {
            if (State != LevelState.Playing) return;

            State = LevelState.Victory;
            Time.timeScale = 1f;
            if (musicSource != null) musicSource.Stop();
            AudioManager.PlaySfx(victoryStingerSfx);
            if (victoryPanel != null) victoryPanel.SetActive(true);

            SurvivalGrind.UI.LevelSelectController.SetUnlockedLevelCount(levelNumber + 1);
        }

        public void GameOver()
        {
            if (State != LevelState.Playing) return;

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
