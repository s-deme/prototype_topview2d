using UnityEngine;

namespace VerdantBlade
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private float speed;
        private float expiresAt;

        public void Configure(Vector2 travelDirection, float travelSpeed)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            expiresAt = Time.time + 2f;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsGameplayLocked)
            {
                return;
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponent<HeroController>();
            if (player != null)
            {
                player.TakeDamage(1, transform.position);
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
