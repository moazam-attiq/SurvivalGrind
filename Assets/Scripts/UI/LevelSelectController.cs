using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalGrind.UI
{
    public class LevelSelectController : MonoBehaviour
    {
        [Serializable]
        public class LevelEntry
        {
            public int levelNumber = 1;
            public string sceneName;
            public Button button;
            public GameObject lockOverlay;
            public CanvasGroup contentGroup;
        }

        private const string UnlockedLevelsKey = "SG_UnlockedLevels";
        private const float DimContentAlpha = 0.45f;

        [SerializeField] private LevelEntry[] levels;

        private void Awake()
        {
            foreach (var entry in levels)
            {
                if (entry.button == null) continue;
                var captured = entry;
                entry.button.onClick.AddListener(() => TrySelectLevel(captured));
            }
        }

        private void OnEnable()
        {
            RefreshLockStates();
        }

        /// unlockedCount is "how far you've gotten": levels below it are beaten (completed), the
        /// level AT unlockedCount is open but not yet beaten, anything past it is still locked.
        public void RefreshLockStates()
        {
            int unlockedCount = PlayerPrefs.GetInt(UnlockedLevelsKey, 1);
            foreach (var entry in levels)
            {
                bool completed = entry.levelNumber < unlockedCount;
                bool locked = entry.levelNumber > unlockedCount;
                bool playable = !locked;

                if (entry.button != null) entry.button.interactable = playable;
                if (entry.lockOverlay != null) entry.lockOverlay.SetActive(locked);
                if (entry.contentGroup != null) entry.contentGroup.alpha = completed ? 1f : DimContentAlpha;
            }
        }

        private void TrySelectLevel(LevelEntry entry)
        {
            if (entry.button != null && !entry.button.interactable) return;
            if (string.IsNullOrEmpty(entry.sceneName))
            {
                Debug.LogWarning($"LevelSelectController: no scene assigned for level {entry.levelNumber}.");
                return;
            }
            SceneManager.LoadScene(entry.sceneName);
        }

        public static void SetUnlockedLevelCount(int count)
        {
            PlayerPrefs.SetInt(UnlockedLevelsKey, Mathf.Max(1, count));
        }
    }
}
