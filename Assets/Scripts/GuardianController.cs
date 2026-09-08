using System.Collections;
using UnityEngine;

namespace VerdantBlade
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GuardianController : MonoBehaviour
    {
        private Rigidbody2D body;
        private SpriteRenderer bodyVisual;
        private WorldHealthBar healthBar;
        private HeroController hero;
        private Color activeColor;
        private int health;
        private float speed;
        private float nextVolleyAt;
        private bool active;
        private bool defeated;
        private string entityId;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyVisual = transform.Find("Body").GetComponent<SpriteRenderer>();
            activeColor = bodyVisual.color;
            bodyVisual.color = new Color(activeColor.r * 0.45f, activeColor.g * 0.45f, activeColor.b * 0.45f);
            healthBar = gameObject.AddComponent<WorldHealthBar>();
        }

        public void Configure(int startingHealth, float movementSpeed, string stableEntityId = null)
        {
            health = startingHealth;
            speed = movementSpeed;
            entityId = stableEntityId;
            healthBar.Configure(health);
        }

        private void FixedUpdate()
        {
            if (defeated || GameManager.Instance == null || GameManager.Instance.IsGameplayLocked)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (!active)
            {
                if (!GameManager.Instance.HasAllShards)
                {
                    return;
                }

                active = true;
                bodyVisual.color = activeColor;
                nextVolleyAt = Time.time + 1f;
                GameManager.Instance.ShowToast("門の守護者が目覚めた。倒して門を開こう。", 3.5f);
            }

            if (hero == null)
            {
                hero = GameManager.Instance.Player;
                if (hero == null)
                {
                    return;
                }
            }

            var offset = (Vector2)hero.transform.position - body.position;
            var distance = offset.magnitude;
            body.linearVelocity = distance > 2.1f ? offset.normalized * speed : Vector2.zero;

            if (distance < 6.8f && Time.time >= nextVolleyAt)
            {
                FireVolley();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (active && !defeated)
            {
                var player = collision.collider.GetComponent<HeroController>();
                if (player != null)
                {
                    player.TakeDamage(1, transform.position);
                }
            }
        }

        public void TakeHit(int damage, Vector2 sourcePosition)
        {
            if (!active || defeated)
            {
                return;
            }

            health -= damage;
            healthBar.SetValue(health);
            SfxService.Instance.Play(health <= 0 ? SoundCue.EnemyDefeat : SoundCue.EnemyHit);
            FloatingCombatText.Spawn(transform.parent, (Vector2)transform.position + Vector2.up * 0.72f, "-" + damage, new Color(1f, 0.72f, 0.4f));
            body.AddForce(((Vector2)transform.position - sourcePosition).normalized * 2.5f, ForceMode2D.Impulse);
            if (health <= 0)
            {
                defeated = true;
                GameManager.Instance.RegisterEnemyDefeated(500);
                GameManager.Instance.RegisterDestroyedEntity(entityId);
                GetComponent<Collider2D>().enabled = false;
                body.linearVelocity = Vector2.zero;
                GameManager.Instance.MarkGuardianDefeated();
                StartCoroutine(DefeatSequence());
            }
        }

        private void FireVolley()
        {
            nextVolleyAt = Time.time + 1.85f;
            SfxService.Instance.Play(SoundCue.EnemyShot);
            var aimedDirection = ((Vector2)hero.transform.position - body.position).normalized;
            for (var index = 0; index < 3; index++)
            {
                var angle = (index - 1) * 18f;
                var rotatedDirection = Quaternion.Euler(0f, 0f, angle) * (Vector3)aimedDirection;
                var direction = new Vector2(rotatedDirection.x, rotatedDirection.y);
                var projectile = new GameObject("Warden Bolt");
                projectile.transform.SetParent(transform.parent);
                projectile.transform.position = transform.position + (Vector3)direction * 0.65f;
                GameBootstrap.CreateVisual("Bolt Glow", Vector2.zero, new Vector2(0.54f, 0.54f), new Color(0.98f, 0.42f, 0.25f, 0.32f), 6, projectile.transform);
                GameBootstrap.CreateVisual("Bolt", Vector2.zero, new Vector2(0.24f, 0.24f), new Color(1f, 0.64f, 0.34f), 8, projectile.transform);
                var trigger = projectile.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 0.25f;
                var projectileBody = projectile.AddComponent<Rigidbody2D>();
                projectileBody.bodyType = RigidbodyType2D.Kinematic;
                projectile.AddComponent<EnemyProjectile>().Configure(direction, 6.6f);
            }
        }

        private IEnumerator DefeatSequence()
        {
            for (var progress = 0f; progress < 1f; progress += Time.deltaTime * 2.8f)
            {
                transform.localScale = Vector3.one * (1f - progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
