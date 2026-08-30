using System.Collections;
using UnityEngine;

namespace VerdantBlade
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyController : MonoBehaviour
    {
        private Rigidbody2D body;
        private SpriteRenderer bodyVisual;
        private WorldHealthBar healthBar;
        private HeroController hero;
        private int health;
        private float speed;
        private Vector2 wanderDirection;
        private float nextWanderChange;
        private bool defeated;
        private string entityId;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyVisual = transform.Find("Body").GetComponent<SpriteRenderer>();
            healthBar = gameObject.AddComponent<WorldHealthBar>();
            wanderDirection = Random.insideUnitCircle.normalized;
            nextWanderChange = Time.time + Random.Range(0.7f, 1.8f);
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

            if (hero == null)
            {
                hero = GameManager.Instance.Player;
                if (hero == null)
                {
                    return;
                }
            }

            var offset = (Vector2)hero.transform.position - body.position;
            Vector2 desired;
            if (offset.sqrMagnitude < 30f)
            {
                desired = offset.normalized;
            }
            else
            {
                if (Time.time >= nextWanderChange)
                {
                    wanderDirection = Random.insideUnitCircle.normalized;
                    nextWanderChange = Time.time + Random.Range(1.1f, 2.8f);
                }
                desired = wanderDirection;
            }
            body.linearVelocity = desired * speed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (defeated)
            {
                return;
            }

            var player = collision.collider.GetComponent<HeroController>();
            if (player != null)
            {
                player.TakeDamage(1, transform.position);
            }
            else
            {
                wanderDirection = -body.linearVelocity.normalized;
                nextWanderChange = Time.time + Random.Range(0.3f, 0.8f);
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
            StartCoroutine(HitFlash());
            var recoil = ((Vector2)transform.position - sourcePosition).normalized;
            body.AddForce(recoil * 4f, ForceMode2D.Impulse);
            if (health <= 0)
            {
                defeated = true;
                GameManager.Instance.RegisterEnemyDefeated(50);
                GameManager.Instance.RegisterDestroyedEntity(entityId);
                body.linearVelocity = Vector2.zero;
                GetComponent<Collider2D>().enabled = false;
                StartCoroutine(DefeatSequence());
            }
        }

        private IEnumerator HitFlash()
        {
            if (GameManager.Instance.ReduceFlashing)
            {
                yield break;
            }
            var normal = bodyVisual.color;
            bodyVisual.color = new Color(1f, 0.65f, 0.65f);
            yield return new WaitForSeconds(0.1f);
            if (!defeated)
            {
                bodyVisual.color = normal;
            }
        }

        private IEnumerator DefeatSequence()
        {
            var baseScale = transform.localScale;
            for (var progress = 0f; progress < 1f; progress += Time.deltaTime * 5f)
            {
                transform.localScale = Vector3.Lerp(baseScale, Vector3.zero, progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
