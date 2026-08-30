using UnityEngine;

namespace VerdantBlade
{
    public sealed class Collectible : MonoBehaviour
    {
        public enum Kind
        {
            Gem,
            Heart
        }

        private Kind kind;
        private int value;
        private Vector3 initialPosition;

        public void Configure(Kind collectibleKind, int collectibleValue)
        {
            kind = collectibleKind;
            value = collectibleValue;
            initialPosition = transform.position;
        }

        private void Update()
        {
            transform.position = initialPosition + Vector3.up * (Mathf.Sin(Time.time * 3f + initialPosition.x) * 0.08f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponent<HeroController>();
            if (player == null || GameManager.Instance.IsGameplayLocked)
            {
                return;
            }

            if (kind == Kind.Gem)
            {
                GameManager.Instance.AddShard(value);
                SfxService.Instance.Play(SoundCue.CollectShard);
            }
            else
            {
                player.Heal(value * 2);
                SfxService.Instance.Play(SoundCue.CollectHeart);
            }
            Destroy(gameObject);
        }
    }

    public sealed class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        private float shakeEndsAt;
        private float shakeStrength;

        private void Awake()
        {
            Instance = this;
        }

        public void Shake(float strength, float duration)
        {
            if (GameManager.Instance == null || !GameManager.Instance.ScreenShakeEnabled)
            {
                return;
            }

            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeEndsAt = Mathf.Max(shakeEndsAt, Time.unscaledTime + duration);
        }

        private void LateUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null)
            {
                return;
            }

            var target = GameManager.Instance.Player.transform.position;
            var position = new Vector3(
                Mathf.Clamp(target.x, -6.2f, 6.2f),
                Mathf.Clamp(target.y, -1.2f, 1.2f),
                -10f);
            if (Time.unscaledTime < shakeEndsAt)
            {
                position += (Vector3)(Random.insideUnitCircle * shakeStrength);
                position.z = -10f;
            }
            else
            {
                shakeStrength = 0f;
            }
            transform.position = position;
        }
    }
}
