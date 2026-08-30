using UnityEngine;

namespace VerdantBlade
{
    public sealed class GoalGate : MonoBehaviour
    {
        private SpriteRenderer glow;
        private bool active;

        private void Awake()
        {
            glow = transform.Find("Gate Glow").GetComponent<SpriteRenderer>();
            glow.color = new Color(1f, 0.82f, 0.25f, 0f);
        }

        private void Update()
        {
            if (active || GameManager.Instance == null || !GameManager.Instance.CanEnterGate)
            {
                return;
            }

            active = true;
            glow.color = new Color(1f, 0.82f, 0.25f, 0.5f);
            SfxService.Instance.Play(SoundCue.GateOpen);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<HeroController>() == null)
            {
                return;
            }

            if (active)
            {
                GameManager.Instance.Victory();
            }
            else if (GameManager.Instance.HasAllShards)
            {
                GameManager.Instance.ShowToast("門の守護者が行く手を阻んでいます。", 2f);
            }
        }
    }
}
