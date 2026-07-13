using UnityEngine;

namespace SurvivalGrind.Gameplay
{
    /// Purely cosmetic projectile lobbed by a MachineTurretFX -- no collision/damage,
    /// just a self-destructing visual to sell the machine actively hurling bombs.
    public class LobbedBombProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private float life;
        private float spinSpeed;

        public void Init(Vector2 velocity, float lifetime)
        {
            this.velocity = velocity;
            life = lifetime;
            spinSpeed = Random.Range(-540f, 540f);
        }

        private void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
