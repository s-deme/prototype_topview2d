using UnityEngine;

namespace VerdantBlade
{
    public enum ObjectiveTargetKind
    {
        Shard,
        Warden,
        Gate
    }

    public sealed class ObjectiveTarget : MonoBehaviour
    {
        public ObjectiveTargetKind Kind { get; private set; }

        public void Configure(ObjectiveTargetKind kind)
        {
            Kind = kind;
        }
    }

    /// <summary>Screen-edge compass that points to the next objective without obscuring play.</summary>
    public sealed class ObjectiveIndicator : MonoBehaviour
    {
        private Transform arrow;
        private SpriteRenderer arrowRenderer;
        private float nextTargetLookup;
        private Transform target;

        private void Awake()
        {
            arrow = GameBootstrap.CreateVisual("Objective Arrow", Vector2.up * 4.65f, new Vector2(0.3f, 0.52f), new Color(1f, 0.88f, 0.28f), 40, transform).transform;
            arrowRenderer = arrow.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            {
                arrowRenderer.enabled = false;
                return;
            }

            if (Time.unscaledTime >= nextTargetLookup || target == null)
            {
                target = FindTarget();
                nextTargetLookup = Time.unscaledTime + 0.25f;
            }

            if (target == null || Camera.main == null)
            {
                arrowRenderer.enabled = false;
                return;
            }

            var cameraPosition = Camera.main.transform.position;
            var direction = (Vector2)target.position - (Vector2)cameraPosition;
            if (direction.sqrMagnitude < 0.01f)
            {
                arrowRenderer.enabled = false;
                return;
            }

            arrowRenderer.enabled = true;
            arrow.localPosition = direction.normalized * 4.8f;
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        private static Transform FindTarget()
        {
            var desiredKind = !GameManager.Instance.HasAllShards
                ? ObjectiveTargetKind.Shard
                : (!GameManager.Instance.IsGuardianDefeated ? ObjectiveTargetKind.Warden : ObjectiveTargetKind.Gate);
            var targets = FindObjectsByType<ObjectiveTarget>(FindObjectsSortMode.None);
            var closestDistance = float.MaxValue;
            Transform closest = null;
            var playerPosition = GameManager.Instance.Player == null ? Vector2.zero : (Vector2)GameManager.Instance.Player.transform.position;
            foreach (var candidate in targets)
            {
                if (candidate.Kind != desiredKind || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distance = ((Vector2)candidate.transform.position - playerPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate.transform;
                }
            }
            return closest;
        }
    }
}
