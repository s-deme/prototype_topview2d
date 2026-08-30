using UnityEngine;

namespace VerdantBlade
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }
        public static Sprite PixelSprite { get; private set; }

        private Transform world;

        private void Awake()
        {
            Instance = this;
            if (PixelSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.name = "Runtime Pixel";
                texture.SetPixel(0, 0, Color.white);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Apply();
                PixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
        }

        private void Start()
        {
            world = new GameObject("World").transform;
            new GameObject("Game Manager").AddComponent<GameManager>();
            new GameObject("Sfx Service").AddComponent<SfxService>();
            CreateCamera();
            BuildGround();
            BuildObstacles();
            SpawnActors();
        }

        private void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.4f;
            camera.backgroundColor = new Color(0.08f, 0.14f, 0.18f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<CameraFollow>();
            cameraObject.AddComponent<ObjectiveIndicator>();
        }

        private void BuildGround()
        {
            var ground = new GameObject("Grassland").transform;
            ground.SetParent(world);

            for (var y = -7; y <= 7; y++)
            {
                for (var x = -12; x <= 12; x++)
                {
                    var tint = ((x * 17 + y * 31) & 1) == 0
                        ? new Color(0.29f, 0.55f, 0.31f)
                        : new Color(0.25f, 0.49f, 0.28f);
                    CreateVisual("Grass", new Vector2(x, y), Vector2.one, tint, 0, ground);
                }
            }

            // A quiet river makes the play area legible while the rock banks remain solid.
            for (var x = -10; x <= -7; x++)
            {
                for (var y = 3; y <= 5; y++)
                {
                    CreateVisual("Water", new Vector2(x, y), Vector2.one, new Color(0.12f, 0.45f, 0.65f), 1, ground);
                }
            }
        }

        private void BuildObstacles()
        {
            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(world);

            CreateWall(new Vector2(0f, 7.65f), new Vector2(25f, 0.7f), obstacles);
            CreateWall(new Vector2(0f, -7.65f), new Vector2(25f, 0.7f), obstacles);
            CreateWall(new Vector2(-12.65f, 0f), new Vector2(0.7f, 16f), obstacles);
            CreateWall(new Vector2(12.65f, 0f), new Vector2(0.7f, 16f), obstacles);

            CreateRock(new Vector2(-6.3f, 4.8f), new Vector2(1.3f, 0.75f), obstacles);
            CreateRock(new Vector2(-8.1f, 2.3f), new Vector2(3.3f, 0.55f), obstacles);
            CreateRock(new Vector2(-5.2f, 1.8f), new Vector2(0.8f, 1.5f), obstacles);
            CreateRock(new Vector2(-1.8f, 2.8f), new Vector2(3.2f, 0.55f), obstacles);
            CreateRock(new Vector2(2.3f, 3.4f), new Vector2(0.65f, 2.2f), obstacles);
            CreateRock(new Vector2(6.3f, 1.2f), new Vector2(3.1f, 0.6f), obstacles);
            CreateRock(new Vector2(8.1f, -2.7f), new Vector2(0.6f, 2.3f), obstacles);
            CreateRock(new Vector2(1.2f, -3.2f), new Vector2(3.3f, 0.6f), obstacles);
            CreateRock(new Vector2(-4.9f, -2.7f), new Vector2(0.7f, 2.1f), obstacles);

            CreateTree(new Vector2(-10.5f, -5.4f), obstacles);
            CreateTree(new Vector2(-9.4f, -4.8f), obstacles);
            CreateTree(new Vector2(-10.1f, -3.7f), obstacles);
            CreateTree(new Vector2(10.5f, 5.5f), obstacles);
            CreateTree(new Vector2(9.4f, 4.9f), obstacles);
            CreateTree(new Vector2(10.1f, 3.8f), obstacles);
            CreateTree(new Vector2(-1.2f, -5.7f), obstacles);
            CreateTree(new Vector2(0.2f, -5.5f), obstacles);
            CreateTree(new Vector2(4.7f, 5.6f), obstacles);
        }

        private void SpawnActors()
        {
            var player = CreateCharacter("Hero", new Vector2(-9.5f, -1.5f), new Color(0.94f, 0.83f, 0.33f));
            CreateHeroVisuals(player.transform);
            var hero = player.AddComponent<HeroController>();
            GameManager.Instance.RegisterPlayer(hero);

            SpawnEnemy("Slime", new Vector2(-2.6f, -0.6f), new Color(0.47f, 0.75f, 0.42f), 2, 1.3f);
            SpawnEnemy("Slime", new Vector2(2.3f, -1.3f), new Color(0.42f, 0.82f, 0.63f), 2, 1.45f);
            SpawnEnemy("Slime", new Vector2(5.1f, 4.5f), new Color(0.58f, 0.74f, 0.34f), 3, 1.55f);
            SpawnEnemy("Slime", new Vector2(8.9f, -4.8f), new Color(0.43f, 0.72f, 0.39f), 3, 1.7f);
            SpawnEnemy("Slime", new Vector2(-6.5f, -5.5f), new Color(0.41f, 0.79f, 0.55f), 2, 1.35f);
            SpawnEnemy("Slime", new Vector2(0.2f, 5.6f), new Color(0.57f, 0.82f, 0.44f), 3, 1.5f);
            SpawnRangedEnemy(new Vector2(-3.7f, 4.8f));
            SpawnRangedEnemy(new Vector2(7.1f, 4.6f));
            SpawnGuardian(new Vector2(7.7f, 5.5f));

            SpawnGem(new Vector2(-8.8f, 0.2f));
            SpawnGem(new Vector2(-3.4f, 5.2f));
            SpawnGem(new Vector2(-1.5f, -0.8f));
            SpawnGem(new Vector2(0f, 1.3f));
            SpawnGem(new Vector2(3.8f, -0.6f));
            SpawnGem(new Vector2(4.9f, 5.5f));
            SpawnGem(new Vector2(9.7f, 0.5f));
            SpawnGem(new Vector2(10f, -5.4f));
            SpawnHeart(new Vector2(-7.3f, -1.4f));
            SpawnHeart(new Vector2(6.8f, -4.9f));
            CreatePot(new Vector2(-7.1f, -0.4f), Collectible.Kind.Heart);
            CreatePot(new Vector2(-0.4f, 4.8f), Collectible.Kind.Gem);
            CreatePot(new Vector2(5.2f, -5.2f), Collectible.Kind.Heart);
            CreatePot(new Vector2(8.8f, 1.8f), Collectible.Kind.Gem);
            CreateGoal(new Vector2(10.7f, 5.8f));
        }

        private GameObject CreateCharacter(string objectName, Vector2 position, Color color)
        {
            var actor = new GameObject(objectName);
            actor.transform.SetParent(world);
            actor.transform.position = position;
            CreateVisual("Shadow", new Vector2(0f, -0.1f), new Vector2(0.74f, 0.38f), new Color(0.05f, 0.12f, 0.09f, 0.34f), 3, actor.transform);
            CreateVisual("Body", Vector2.zero, new Vector2(0.72f, 0.72f), color, 5, actor.transform);
            var body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = actor.AddComponent<CircleCollider2D>();
            collider.radius = 0.34f;
            return actor;
        }

        private void CreateHeroVisuals(Transform hero)
        {
            CreateVisual("Cape", new Vector2(0f, -0.13f), new Vector2(0.48f, 0.38f), new Color(0.23f, 0.36f, 0.72f), 4, hero);
            CreateVisual("Blade", new Vector2(0f, -0.43f), new Vector2(0.52f, 0.1f), new Color(0.91f, 0.94f, 0.89f), 7, hero);
            CreateVisual("Blade Hilt", new Vector2(0f, -0.35f), new Vector2(0.1f, 0.22f), new Color(0.36f, 0.21f, 0.1f), 8, hero);
        }

        private void SpawnEnemy(string objectName, Vector2 position, Color color, int health, float speed)
        {
            var enemy = CreateCharacter(objectName, position, color);
            var controller = enemy.AddComponent<EnemyController>();
            controller.Configure(GameRules.EnemyHealth(health, GameManager.Instance.SelectedDifficulty), GameRules.EnemySpeed(speed, GameManager.Instance.SelectedDifficulty));
        }

        private void SpawnRangedEnemy(Vector2 position)
        {
            var wisp = CreateCharacter("Moss Wisp", position, new Color(0.68f, 0.91f, 0.67f));
            var controller = wisp.AddComponent<RangedEnemyController>();
            controller.Configure(GameRules.EnemyHealth(2, GameManager.Instance.SelectedDifficulty), GameRules.EnemySpeed(1.65f, GameManager.Instance.SelectedDifficulty));
        }

        private void SpawnGuardian(Vector2 position)
        {
            var guardian = CreateCharacter("Gate Warden", position, new Color(0.72f, 0.38f, 0.28f));
            CreateVisual("Warden Crown", new Vector2(0f, 0.42f), new Vector2(0.46f, 0.12f), new Color(0.94f, 0.65f, 0.24f), 7, guardian.transform);
            var controller = guardian.AddComponent<GuardianController>();
            controller.Configure(GameRules.EnemyHealth(9, GameManager.Instance.SelectedDifficulty), GameRules.EnemySpeed(2.05f, GameManager.Instance.SelectedDifficulty));
            guardian.AddComponent<ObjectiveTarget>().Configure(ObjectiveTargetKind.Warden);
        }

        private void SpawnGem(Vector2 position)
        {
            var gem = new GameObject("Sun Shard");
            gem.transform.SetParent(world);
            gem.transform.position = position;
            CreateVisual("Gem Glow", Vector2.zero, new Vector2(0.56f, 0.56f), new Color(0.99f, 0.78f, 0.15f, 0.2f), 3, gem.transform);
            var visual = CreateVisual("Gem", Vector2.zero, new Vector2(0.32f, 0.32f), new Color(1f, 0.91f, 0.33f), 6, gem.transform);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var trigger = gem.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.48f;
            gem.AddComponent<Collectible>().Configure(Collectible.Kind.Gem, 1);
            gem.AddComponent<ObjectiveTarget>().Configure(ObjectiveTargetKind.Shard);
        }

        private void SpawnHeart(Vector2 position)
        {
            var heart = new GameObject("Life Bloom");
            heart.transform.SetParent(world);
            heart.transform.position = position;
            CreateVisual("Heart", Vector2.zero, new Vector2(0.38f, 0.38f), new Color(0.94f, 0.27f, 0.35f), 6, heart.transform);
            var trigger = heart.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.42f;
            heart.AddComponent<Collectible>().Configure(Collectible.Kind.Heart, 1);
        }

        public void SpawnPickup(Collectible.Kind kind, Vector2 position)
        {
            if (kind == Collectible.Kind.Gem)
            {
                SpawnGem(position);
            }
            else
            {
                SpawnHeart(position);
            }
        }

        private void CreatePot(Vector2 position, Collectible.Kind dropKind)
        {
            var pot = new GameObject("Clay Pot");
            pot.transform.SetParent(world);
            pot.transform.position = position;
            CreateVisual("Pot Shadow", new Vector2(0.04f, -0.09f), new Vector2(0.52f, 0.18f), new Color(0.07f, 0.1f, 0.08f, 0.32f), 2, pot.transform);
            CreateVisual("Pot Body", Vector2.zero, new Vector2(0.46f, 0.5f), new Color(0.68f, 0.35f, 0.16f), 5, pot.transform);
            CreateVisual("Pot Rim", new Vector2(0f, 0.22f), new Vector2(0.55f, 0.1f), new Color(0.84f, 0.49f, 0.23f), 6, pot.transform);
            var collider = pot.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.44f, 0.46f);
            pot.AddComponent<BreakablePot>().Configure(dropKind, 1);
        }

        private void CreateGoal(Vector2 position)
        {
            var goal = new GameObject("Ancient Gate");
            goal.transform.SetParent(world);
            goal.transform.position = position;
            CreateVisual("Gate Base", Vector2.zero, new Vector2(1.1f, 0.32f), new Color(0.26f, 0.21f, 0.18f), 3, goal.transform);
            CreateVisual("Gate Pillar", new Vector2(-0.42f, 0.43f), new Vector2(0.18f, 1.05f), new Color(0.64f, 0.61f, 0.47f), 4, goal.transform);
            CreateVisual("Gate Pillar", new Vector2(0.42f, 0.43f), new Vector2(0.18f, 1.05f), new Color(0.64f, 0.61f, 0.47f), 4, goal.transform);
            CreateVisual("Gate Crown", new Vector2(0f, 0.91f), new Vector2(1.12f, 0.18f), new Color(0.72f, 0.68f, 0.49f), 4, goal.transform);
            CreateVisual("Gate Glow", new Vector2(0f, 0.42f), new Vector2(0.58f, 0.78f), new Color(1f, 0.82f, 0.25f, 0f), 5, goal.transform);
            var trigger = goal.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.76f;
            trigger.offset = new Vector2(0f, 0.35f);
            goal.AddComponent<GoalGate>();
            goal.AddComponent<ObjectiveTarget>().Configure(ObjectiveTargetKind.Gate);
        }

        private void CreateWall(Vector2 position, Vector2 size, Transform parent)
        {
            var wall = new GameObject("Stone Boundary");
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            CreateVisual("Stone", Vector2.zero, size, new Color(0.22f, 0.29f, 0.31f), 2, wall.transform);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private void CreateRock(Vector2 position, Vector2 size, Transform parent)
        {
            var rock = new GameObject("Rock");
            rock.transform.SetParent(parent);
            rock.transform.position = position;
            CreateVisual("Rock Shadow", new Vector2(0.06f, -0.08f), size, new Color(0.08f, 0.13f, 0.14f, 0.36f), 2, rock.transform);
            CreateVisual("Rock", Vector2.zero, size, new Color(0.34f, 0.38f, 0.38f), 3, rock.transform);
            var collider = rock.AddComponent<BoxCollider2D>();
            collider.size = size * 0.9f;
        }

        private void CreateTree(Vector2 position, Transform parent)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent);
            tree.transform.position = position;
            CreateVisual("Tree Shadow", new Vector2(0.1f, -0.15f), new Vector2(1.1f, 0.86f), new Color(0.05f, 0.12f, 0.09f, 0.3f), 2, tree.transform);
            CreateVisual("Trunk", new Vector2(0f, -0.25f), new Vector2(0.24f, 0.6f), new Color(0.34f, 0.22f, 0.12f), 3, tree.transform);
            CreateVisual("Canopy", new Vector2(0f, 0.15f), new Vector2(0.98f, 0.84f), new Color(0.13f, 0.37f, 0.2f), 4, tree.transform);
            var collider = tree.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            collider.offset = new Vector2(0f, 0.04f);
        }

        public static GameObject CreateVisual(string objectName, Vector2 localPosition, Vector2 size, Color color, int sortingOrder, Transform parent)
        {
            var visual = new GameObject(objectName);
            visual.transform.SetParent(parent);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }
    }
}
