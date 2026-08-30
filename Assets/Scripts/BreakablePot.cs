using UnityEngine;

namespace VerdantBlade
{
    public sealed class BreakablePot : MonoBehaviour
    {
        private Collectible.Kind dropKind;
        private int dropValue;
        private bool broken;

        public void Configure(Collectible.Kind collectibleKind, int collectibleValue)
        {
            dropKind = collectibleKind;
            dropValue = collectibleValue;
        }

        public void TakeHit(int damage, Vector2 sourcePosition)
        {
            if (broken || damage <= 0)
            {
                return;
            }

            broken = true;
            SfxService.Instance.Play(SoundCue.PotBreak);
            GameManager.Instance.RegisterPotBroken();
            GetComponent<Collider2D>().enabled = false;
            var potPosition = (Vector2)transform.position;
            GameBootstrap.Instance.SpawnPickup(dropKind, potPosition + Random.insideUnitCircle * 0.12f);

            var fragmentA = GameBootstrap.CreateVisual("Pot Fragment", potPosition + new Vector2(-0.14f, 0.04f), new Vector2(0.2f, 0.16f), new Color(0.69f, 0.39f, 0.18f), 7, transform.parent);
            var fragmentB = GameBootstrap.CreateVisual("Pot Fragment", potPosition + new Vector2(0.14f, -0.08f), new Vector2(0.18f, 0.22f), new Color(0.58f, 0.3f, 0.14f), 7, transform.parent);
            Destroy(fragmentA, 0.4f);
            Destroy(fragmentB, 0.4f);
            Destroy(gameObject);
        }
    }
}
