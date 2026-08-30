using System.Collections;
using UnityEngine;

namespace VerdantBlade
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class HeroController : MonoBehaviour
    {
        private const float MoveSpeed = 4.1f;
        private const float AttackCooldown = 0.33f;
        private const float DashSpeed = 10.5f;
        private const float DashDuration = 0.14f;
        private const float DashCooldown = 0.72f;

        private Rigidbody2D body;
        private SpriteRenderer bodyVisual;
        private Transform bodyTransform;
        private Transform bladeTransform;
        private Transform hiltTransform;
        private Vector3 bodyBaseScale;
        private Vector2 moveInput;
        private Vector2 facing = Vector2.down;
        private float nextAttackTime;
        private float dashEndsAt;
        private float nextDashTime;
        private float invulnerableUntil;
        private int health;
        private int maxHealth = 6;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public Vector2 Facing => facing;
        public bool IsDashing => Time.time < dashEndsAt;
        public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

        private void Awake()
        {
            maxHealth = GameManager.Instance == null ? 6 : GameRules.PlayerMaxHealth(GameManager.Instance.SelectedDifficulty);
            health = maxHealth;
            body = GetComponent<Rigidbody2D>();
            bodyTransform = transform.Find("Body");
            bodyVisual = bodyTransform.GetComponent<SpriteRenderer>();
            bodyBaseScale = bodyTransform.localScale;
            bladeTransform = transform.Find("Blade");
            hiltTransform = transform.Find("Blade Hilt");
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsGameplayLocked)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = GameInput.Move;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                facing = moveInput.normalized;
            }

            if (GameInput.PrimaryPressed)
            {
                Attack();
            }

            if (GameInput.DashPressed)
            {
                Dash();
            }

            UpdatePresentation();
        }

        private void FixedUpdate()
        {
            var velocity = IsDashing ? facing * DashSpeed : moveInput * MoveSpeed;
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
        }

        public void TakeDamage(int amount, Vector2 sourcePosition)
        {
            if (Time.time < invulnerableUntil || GameManager.Instance.IsGameplayLocked)
            {
                return;
            }

            health = Mathf.Max(0, health - amount);
            GameManager.Instance.RegisterDamageTaken(amount);
            SfxService.Instance.Play(SoundCue.PlayerHit);
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.Shake(0.18f, 0.18f);
            }
            invulnerableUntil = Time.time + 0.85f;
            StartCoroutine(HitFlash());
            if (health == 0)
            {
                GameManager.Instance.Defeat();
            }
        }

        public void Heal(int amount)
        {
            var previousHealth = health;
            health = Mathf.Min(MaxHealth, health + amount);
            if (health > previousHealth)
            {
                FloatingCombatText.Spawn(transform.parent, (Vector2)transform.position + Vector2.up * 0.7f, "+" + (health - previousHealth), new Color(0.66f, 1f, 0.72f));
                GameManager.Instance.NotifyPlayerHealed(health - previousHealth);
            }
        }

        private void Attack()
        {
            if (Time.time < nextAttackTime || GameManager.Instance.IsGameplayLocked)
            {
                return;
            }

            if (GameInput.TryGetPointerDirection(transform.position, out var pointerDirection))
            {
                facing = pointerDirection.normalized;
            }

            nextAttackTime = Time.time + AttackCooldown;
            SfxService.Instance.Play(SoundCue.Attack);
            var center = (Vector2)transform.position + facing * 0.72f;
            var slash = GameBootstrap.CreateVisual("Blade Arc", center, new Vector2(0.95f, 0.62f), new Color(1f, 0.94f, 0.53f, 0.82f), 9, transform.parent);
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 30f);
            Destroy(slash, 0.11f);

            var targets = Physics2D.OverlapCircleAll(center, 0.74f);
            var hitCount = 0;
            foreach (var target in targets)
            {
                var enemy = target.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeHit(1, transform.position);
                    hitCount++;
                }

                var rangedEnemy = target.GetComponent<RangedEnemyController>();
                if (rangedEnemy != null)
                {
                    rangedEnemy.TakeHit(1, transform.position);
                    hitCount++;
                }

                var pot = target.GetComponent<BreakablePot>();
                if (pot != null)
                {
                    pot.TakeHit(1, transform.position);
                }

                var guardian = target.GetComponent<GuardianController>();
                if (guardian != null)
                {
                    guardian.TakeHit(1, transform.position);
                    hitCount++;
                }
            }
            if (hitCount > 0)
            {
                GameManager.Instance.RegisterCombatHit(hitCount);
            }
        }

        private void Dash()
        {
            if (Time.time < nextDashTime || GameManager.Instance.IsGameplayLocked)
            {
                return;
            }

            if (moveInput.sqrMagnitude > 0.01f)
            {
                facing = moveInput.normalized;
            }

            dashEndsAt = Time.time + DashDuration;
            nextDashTime = Time.time + DashCooldown;
            invulnerableUntil = Mathf.Max(invulnerableUntil, dashEndsAt);
            SfxService.Instance.Play(SoundCue.Dash);

            var trailPosition = (Vector2)transform.position - facing * 0.28f;
            var trail = GameBootstrap.CreateVisual("Dash Trail", trailPosition, new Vector2(0.72f, 0.72f), new Color(0.56f, 0.88f, 0.86f, 0.46f), 4, transform.parent);
            Destroy(trail, 0.16f);
        }

        private void UpdatePresentation()
        {
            var moving = moveInput.sqrMagnitude > 0.01f;
            var bob = moving ? Mathf.Sin(Time.time * 14f) * 0.035f : 0f;
            bodyTransform.localPosition = new Vector3(0f, bob, 0f);

            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var bladePosition = facing * 0.43f;
            bladeTransform.localPosition = bladePosition;
            hiltTransform.localPosition = facing * 0.35f;
            bladeTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            hiltTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            bodyVisual.transform.localScale = IsDashing ? Vector3.Scale(bodyBaseScale, new Vector3(1.1f, 0.86f, 1f)) : bodyBaseScale;
        }

        private IEnumerator HitFlash()
        {
            if (GameManager.Instance.ReduceFlashing)
            {
                yield break;
            }
            var normal = bodyVisual.color;
            while (Time.time < invulnerableUntil)
            {
                bodyVisual.color = Color.white;
                yield return new WaitForSeconds(0.08f);
                bodyVisual.color = normal;
                yield return new WaitForSeconds(0.08f);
            }
            bodyVisual.color = normal;
        }
    }
}
