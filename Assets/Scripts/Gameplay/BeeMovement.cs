using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    public class BeeMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 4f;
        [SerializeField] private int damage = 10;

        private void Update()
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Leader"))
                return;

            if (LevelRunner.Instance != null)
            {
                LevelRunner.Instance.RegisterHazardHit(damage);
            }

            Destroy(gameObject);
        }

        // private void OnBecameInvisible()
        // {
        //     Destroy(gameObject);
        // }
    }
}