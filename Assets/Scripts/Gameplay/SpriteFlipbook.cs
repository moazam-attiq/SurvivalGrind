using System.Collections;
using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float fps = 12f;
        [SerializeField] private bool loop = false;
        [SerializeField] private bool playOnEnable = false;

        private SpriteRenderer sr;
        private Coroutine routine;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (playOnEnable) Play();
        }

        public void Play()
        {
            if (frames == null || frames.Length == 0) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator Run()
        {
            float frameTime = 1f / Mathf.Max(1f, fps);
            do
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    sr.sprite = frames[i];
                    yield return new WaitForSeconds(frameTime);
                }
            } while (loop);
            routine = null;
        }
    }
}
