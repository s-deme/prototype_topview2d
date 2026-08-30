using UnityEngine;

namespace VerdantBlade
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private Transform fill;
        private int maximum;

        private void Awake()
        {
            GameBootstrap.CreateVisual("Health Bar Back", new Vector2(0f, 0.62f), new Vector2(0.78f, 0.08f), new Color(0.09f, 0.1f, 0.09f, 0.82f), 9, transform);
            fill = GameBootstrap.CreateVisual("Health Bar Fill", new Vector2(0f, 0.62f), new Vector2(0.7f, 0.035f), new Color(0.92f, 0.35f, 0.31f), 10, transform).transform;
        }

        public void Configure(int maximumHealth)
        {
            maximum = Mathf.Max(1, maximumHealth);
            SetValue(maximum);
        }

        public void SetValue(int currentHealth)
        {
            var ratio = Mathf.Clamp01((float)currentHealth / maximum);
            fill.localScale = new Vector3(0.7f * ratio, 0.035f, 1f);
            fill.localPosition = new Vector3(-0.35f * (1f - ratio), 0.62f, 0f);
        }
    }

    public sealed class FloatingCombatText : MonoBehaviour
    {
        private TextMesh textMesh;
        private Color baseColor;
        private float expiresAt;

        public static void Spawn(Transform parent, Vector2 position, string text, Color color)
        {
            var popup = new GameObject("Combat Text");
            popup.transform.SetParent(parent);
            popup.transform.position = position;
            var component = popup.AddComponent<FloatingCombatText>();
            component.Configure(text, color);
        }

        private void Configure(string text, Color color)
        {
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.09f;
            textMesh.color = color;
            textMesh.GetComponent<MeshRenderer>().sortingOrder = 12;
            baseColor = color;
            expiresAt = Time.time + 0.55f;
        }

        private void Update()
        {
            transform.position += Vector3.up * (0.72f * Time.deltaTime);
            var remaining = Mathf.Clamp01((expiresAt - Time.time) / 0.55f);
            textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, remaining);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }
    }
}
