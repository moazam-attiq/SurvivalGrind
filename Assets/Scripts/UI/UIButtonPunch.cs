using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalGrind.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class UIButtonPunch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float pressedScale = 0.94f;
        [SerializeField] private float animationSpeed = 12f;

        private RectTransform _rect;
        private Button _button;
        private Vector3 _baseScale;
        private Coroutine _routine;
        private bool _hovering;
        private bool _pressed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _button = GetComponent<Button>();
            _baseScale = _rect.localScale;
        }

        private bool Interactable => _button == null || _button.interactable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Interactable) return;
            _hovering = true;
            Restart();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            _pressed = false;
            Restart();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            _pressed = true;
            AudioManager.PlayUiClick();
            Restart();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Restart();
        }

        private void Restart()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(AnimateTo(TargetScale()));
        }

        private float TargetScale()
        {
            if (!Interactable) return 1f;
            if (_pressed) return pressedScale;
            if (_hovering) return hoverScale;
            return 1f;
        }

        private IEnumerator AnimateTo(float scaleMultiplier)
        {
            Vector3 target = _baseScale * scaleMultiplier;
            while (Vector3.Distance(_rect.localScale, target) > 0.001f)
            {
                _rect.localScale = Vector3.Lerp(_rect.localScale, target, Time.unscaledDeltaTime * animationSpeed);
                yield return null;
            }
            _rect.localScale = target;
        }

        private void OnDisable()
        {
            _hovering = false;
            _pressed = false;
            if (_rect != null) _rect.localScale = _baseScale;
        }
    }
}
