using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public class CameraFramingController : MonoBehaviour
    {
        [SerializeField] private Transform commander;
        [SerializeField] private CrowdFormationController formation;

        [Header("Framing")]
        [SerializeField] private float minOrthoSize = 6f;
        [SerializeField] private float maxOrthoSize = 7.5f;
        [SerializeField] private float sizePadding = 1f;
        [SerializeField] private float trailBias = 0.4f;
        [SerializeField] private float positionSmoothTime = 0.4f;
        [SerializeField] private float sizeSmoothTime = 0.8f;

        private Camera cam;
        private Vector3 positionVelocity;
        private float sizeVelocity;
        private float fixedY;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            fixedY = transform.position.y;
        }

        private void Start()
        {
            if (commander == null)
            {
                var leaderGo = GameObject.FindGameObjectWithTag("Leader");
                if (leaderGo != null) commander = leaderGo.transform;
            }
            if (formation == null)
            {
                formation = FindFirstObjectByType<CrowdFormationController>();
            }
        }

        private void LateUpdate()
        {
            if (commander == null) return;

            float backDistance = formation != null ? formation.BackRowDistance : 4f;
            float aspect = cam.aspect;

            Vector3 targetPos = transform.position;
            targetPos.x = commander.position.x - backDistance * trailBias;
            targetPos.y = fixedY;
            targetPos.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, positionSmoothTime);

            float neededHalfWidth = (backDistance * 0.5f + sizePadding) / Mathf.Max(aspect, 0.01f);
            float targetSize = Mathf.Clamp(neededHalfWidth + sizePadding, minOrthoSize, maxOrthoSize);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, sizeSmoothTime);
        }
    }
}
