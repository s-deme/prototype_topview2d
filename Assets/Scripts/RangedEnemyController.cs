using System.Collections;
using UnityEngine;

namespace VerdantBlade
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RangedEnemyController : MonoBehaviour
    {
        private Rigidbody2D body;
        private SpriteRenderer bodyVisual;
        private WorldHealthBar healthBar;
        private HeroController hero;
        private int health;
        private float speed;
        private float nextShotTime;
        private float strafeSign;
        private bool defeated;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyVisual = transform.Find("Body").GetComponent<SpriteRenderer>();
            healthBar = gameObject.AddComponent<WorldHealthBar>();
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            nextShotTime = Time.time + Random.Range(0.45f, 1.1f);
        }

        public void Configure(int startingHealth, float movementSpeed)
        {
            health = startingHealth;
            speed = movementSpeed;
            healthBar.Configure(health);
        }

        private void FixedUpdate()
        {
            if (defeated || GameManager.Instance == null || GameManager.Instance.IsGameplayLocked)
            {
                body.linearVelocity = Vector2.zero;
                return;
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
            var towardPlayer = offset.normalized;
            Vector2 desired;
            if (distance < 2.55f)
            {
                desired = -towardPlayer;
            }
            else if (distance > 4.75f)
            {
                desired = towardPlayer;
            }
            else
            {
                desired = new Vector2(-towardPlayer.y, towardPlayer.x) * strafeSign;
            }
            body.linearVelocity = desired * speed;

            if (distance < 6.2f && Time.time >= nextShotTime)
            {
                Shoot(towardPlayer);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var player = collision.collider.GetComponent<HeroController>();
            if (player != null)
            {
                player.TakeDamage(1, transform.position);
            }
            else
            {
                strafeSign *= -1f;
            }
        }

        public void TakeHit(int damage, Vector2 sourcePosition)
        {
            if (defeated)
            {
                return;
            }

            health -= damage;
            SfxService.Instance.Play(health <= 0 ? SoundCue.EnemyDefeat : SoundCue.EnemyHit);
            healthBar.SetValue(health);
            FloatingCombatText.Spawn(transform.parent, (Vector2)transform.position + Vector2.up * 0.62f, "-" + damage, new Color(1f, 0.78f, 0.49f));
            var recoil = ((Vector2)transform.position - sourcePosition).normalized;
            body.AddForce(recoil * 3.2f, ForceMode2D.Impulse);
            StartCoroutine(HitFlash());
            if (health <= 0)
            {
                defeated = true;
                GameManager.Instance.RegisterEnemyDefeated(75);
                body.linearVelocity = Vector2.zero;
                GetComponent<Collider2D>().enabled = false;
                StartCoroutine(DefeatSequence());
            }
        }

        private void Shoot(Vector2 direction)
        {
            nextShotTime = Time.time + 1.5f;
            SfxService.Instance.Play(SoundCue.EnemyShot);
            var projectile = new GameObject("Wisp Bolt");
            projectile.transform.SetParent(transform.parent);
            projectile.transform.position = transform.position + (Vector3)direction * 0.55f;
            GameBootstrap.CreateVisual("Bolt Glow", Vector2.zero, new Vector2(0.46f, 0.46f), new Color(0.58f, 0.94f, 0.74f, 0.35f), 6, projectile.transform);
            GameBootstrap.CreateVisual("Bolt", Vector2.zero, new Vector2(0.22f, 0.22f), new Color(0.69f, 1f, 0.78f), 8, projectile.transform);
            var collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.24f;
            var projectileBody = projectile.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;
            projectile.AddComponent<EnemyProjectile>().Configure(direction, 6f);
        }

        private IEnumerator HitFlash()
        {
            if (GameManager.Instance.ReduceFlashing)
            {
                yield break;
            }
            var normal = bodyVisual.color;
            bodyVisual.color = new Color(1f, 0.63f, 0.73f);
            yield return new WaitForSeconds(0.1f);
            if (!defeated)
            {
                bodyVisual.color = normal;
            }
        }

        private IEnumerator DefeatSequence()
        {
            for (var progress = 0f; progress < 1f; progress += Time.deltaTime * 5f)
            {
                transform.localScale = Vector3.one * (1f - progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
