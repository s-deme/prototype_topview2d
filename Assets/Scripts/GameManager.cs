using System.Collections.Generic;
using UnityEngine;

namespace VerdantBlade
{
    public enum RunState
    {
        Title,
        Playing,
        Paused,
        Won,
        Defeated
    }

    /// <summary>
    /// Owns the game-state machine, presentation, local progression, and recovery paths.
    /// It deliberately uses Unity's built-in IMGUI so this sample stays asset-free and self-contained.
    /// </summary>
    public sealed partial class GameManager : MonoBehaviour
    {
        private enum TitlePage { Main, HowTo, Settings, Profile }

        public static GameManager Instance { get; private set; }
        public static readonly string[] AchievementIds = { "first_steps", "shard_seeker", "warden_slayer", "gatewalker", "unbroken", "swift_blade", "forest_hunter", "potter" };
        public const string ProductVersion = "1.0.0";

        private static bool startRunOnSceneLoad;

        public HeroController Player { get; private set; }
        public RunState State => state;
        public bool IsFinished => state == RunState.Won || state == RunState.Defeated;
        public bool IsPaused => state == RunState.Paused;
        public bool IsPlaying => state == RunState.Playing;
        public bool IsGameplayLocked => !IsPlaying;
        public bool HasAllShards => shards >= GameRules.ShardGoal;
        public bool IsGuardianDefeated { get; private set; }
        public bool CanEnterGate => HasAllShards && IsGuardianDefeated;
        public bool ReduceFlashing { get; private set; }
        public bool ScreenShakeEnabled { get; private set; } = true;
        public bool HighContrast { get; private set; }
        public bool LargeText { get; private set; }
        public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Adventurer;
        public float SfxVolume { get; private set; } = 0.62f;
        public float MusicVolume { get; private set; } = 0.38f;
        public DisplayMode SelectedDisplayMode { get; private set; } = DisplayMode.Borderless;
        public int ResolutionIndex { get; private set; }
        public bool VSyncEnabled { get; private set; }
        public int WorldVariant { get; private set; }
        public int ShardCount => shards;
        public int EnemiesDefeated { get; private set; }
        public int PotsBroken { get; private set; }
        public int DamageTaken { get; private set; }
        public int Combo => combo;
        public int BestCombo => bestCombo;
        public float ElapsedRunSeconds => state == RunState.Title ? 0f : runClock.ElapsedSeconds;
        public int CurrentScore => GameRules.CalculateScore(shards, combatScore, PotsBroken, Player == null ? 0 : Player.Health, bestCombo, state == RunState.Won, ElapsedRunSeconds);

        private const float ComboWindowSeconds = 3.5f;
        private RunState state = RunState.Title;
        private TitlePage titlePage;
        private int shards;
        private bool won;
        private readonly RunClock runClock = new RunClock();
        private float toastEndsAt;
        private string toastMessage;
        private int combatScore;
        private int combo;
        private int bestCombo;
        private float comboEndsAt;
        private bool newBestScore;
        private bool runSubmitted;
        private bool autoStartRun;
        private bool newRunConfirmation;
        private bool pausedByFocusLoss;
        private bool pauseSettingsOpen;
        private bool bindingsOpen;
        private GameAction pendingBinding;
        private bool gamepadBindingsOpen;
        private GameAction pendingGamepadBinding;
        private int titleMenuIndex = 1;
        private int settingsMenuIndex;
        private int keyboardBindingsMenuIndex;
        private int gamepadBindingsMenuIndex;
        private int profileMenuIndex;
        private int pauseMenuIndex;
        private int resultMenuIndex;
        private bool profileAchievementsOpen;
        private Difficulty profileDifficulty = Difficulty.Adventurer;
        private bool resetConfirmation;
        private bool resetSettingsConfirmation;
        private string profileNotice;
        private string settingsNotice;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool showDiagnostics;
#endif
        private readonly List<string> newAchievements = new List<string>();
        private readonly HashSet<string> destroyedEntityIds = new HashSet<string>();
        private RunSnapshot resumeSnapshot;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle subtleStyle;
        private GUIStyle centerStyle;
        private GUIStyle bodyCenterStyle;
        private GUIStyle titleCenterStyle;
        private GUIStyle buttonStyle;
        private Font uiFont;
        private bool stylesUseLargeText;
        private bool stylesUseHighContrast;
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private float uiScale = 1f;
        private Vector2 uiOffset;

        private float ViewWidth => ReferenceWidth;
        private float ViewHeight => ReferenceHeight;

        private int ComboBonus => bestCombo < 2 ? 0 : bestCombo * 10;

        private void Awake()
        {
            Instance = this;
            LoadAudioAndAccessibilitySettings();
            SelectedDifficulty = PlayerProfile.LoadDifficulty();
            LoadDisplayPreferences();
            autoStartRun = startRunOnSceneLoad;
            startRunOnSceneLoad = false;
            if (!autoStartRun)
            {
                resumeSnapshot = PlayerProfile.LoadRunSnapshot();
                if (resumeSnapshot != null)
                {
                    SelectedDifficulty = (Difficulty)resumeSnapshot.difficulty;
                    WorldVariant = resumeSnapshot.worldVariant;
                    RestoreDestroyedEntityIds(resumeSnapshot.destroyedEntityIds);
                }
            }
            if (resumeSnapshot == null)
            {
                WorldVariant = UnityEngine.Random.Range(0, 3);
            }
            ApplyDisplaySettings();
            Time.timeScale = 0f;
        }

        private void Start()
        {
            if (autoStartRun)
            {
                StartRun();
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandleDevelopmentShortcuts();
#endif
            if (CapturePendingBinding() || CapturePendingGamepadBinding()) return;
            if (state == RunState.Title)
            {
                HandleTitleInput();
                return;
            }

            if (IsFinished)
            {
                HandleResultInput();
                return;
            }

            runClock.Tick(Time.unscaledDeltaTime, IsPlaying);

            if (GameInput.PausePressed && !pauseSettingsOpen)
            {
                TogglePause();
                // The default pause binding is Escape, which also means “back” in menus.
                // Do not process that same key press again as a pause-menu action.
                return;
            }

            if (IsPlaying && combo > 0 && Time.time > comboEndsAt)
            {
                combo = 0;
            }

            if (IsPaused)
            {
                HandlePauseInput();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void OnApplicationQuit()
        {
            SaveRunSnapshot();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) PauseForInterruption();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PauseForInterruption();
        }

        public void RegisterPlayer(HeroController player)
        {
            Player = player;
            if (resumeSnapshot != null)
            {
                player.RestoreState(resumeSnapshot.playerHealth, new Vector2(resumeSnapshot.playerX, resumeSnapshot.playerY));
            }
        }

        public void StartRun()
        {
            if (state != RunState.Title)
            {
                return;
            }

            state = RunState.Playing;
            Time.timeScale = 1f;
            runSubmitted = false;
            if (resumeSnapshot != null)
            {
                shards = resumeSnapshot.shards;
                combatScore = resumeSnapshot.combatScore;
                EnemiesDefeated = resumeSnapshot.enemiesDefeated;
                PotsBroken = resumeSnapshot.potsBroken;
                DamageTaken = resumeSnapshot.damageTaken;
                bestCombo = resumeSnapshot.bestCombo;
                runClock.Reset(resumeSnapshot.elapsedSeconds);
                ShowToast("中断した冒険を再開しました。", 2.8f);
            }
            else
            {
                runClock.Reset();
                ShowToast("森に散らばる太陽の欠片を集めよう。", 4f);
            }
            UnlockAchievement("first_steps");
            SaveRunSnapshot();
        }

        public void StartFreshRun()
        {
            if (state != RunState.Title)
            {
                return;
            }
            if (resumeSnapshot != null)
            {
                if (!newRunConfirmation)
                {
                    newRunConfirmation = true;
                    ShowToast("保存中の冒険があります。もう一度決定すると破棄して新しく始めます。", 3.5f);
                    return;
                }
                SubmitSnapshotAsAbandoned();
                PlayerProfile.ClearRunSnapshot();
                resumeSnapshot = null;
                destroyedEntityIds.Clear();
                startRunOnSceneLoad = true;
                ReloadCurrentScene();
                return;
            }
            StartRun();
        }

        public void ResumeRun()
        {
            if (state == RunState.Title && resumeSnapshot != null)
            {
                StartRun();
            }
        }

        public bool ShouldSpawnEntity(string entityId)
        {
            return string.IsNullOrEmpty(entityId) || !destroyedEntityIds.Contains(entityId);
        }

        public void RegisterDestroyedEntity(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                return;
            }
            destroyedEntityIds.Add(entityId);
            SaveRunSnapshot();
        }

        public void AddShard(int amount)
        {
            var previous = shards;
            shards = GameRules.ClampShards(shards, amount);
            if (shards > previous && HasAllShards)
            {
                UnlockAchievement("shard_seeker");
            }
            ShowToast(HasAllShards ? "太陽の欠片がそろった。守護者が目覚めた。" : "太陽の欠片を発見  " + shards + " / " + GameRules.ShardGoal, 2.2f);
            SaveRunSnapshot();
        }

        public void ShowToast(string message, float duration = 1.7f)
        {
            toastMessage = message;
            toastEndsAt = Time.unscaledTime + duration;
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            if (SfxService.Instance != null) SfxService.Instance.SetVolume(SfxVolume);
            SaveSettings();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            if (SfxService.Instance != null) SfxService.Instance.SetMusicVolume(MusicVolume);
            SaveSettings();
        }

        public void ToggleReducedFlashing()
        {
            ReduceFlashing = !ReduceFlashing;
            SaveSettings();
        }

        public void ToggleScreenShake()
        {
            ScreenShakeEnabled = !ScreenShakeEnabled;
            SaveSettings();
        }

        public void ToggleHighContrast()
        {
            HighContrast = !HighContrast;
            SaveSettings();
        }

        public void ToggleLargeText()
        {
            LargeText = !LargeText;
            titleStyle = null;
            SaveSettings();
        }

        public void Victory()
        {
            if (!CanEnterGate || IsFinished)
            {
                return;
            }

            state = RunState.Won;
            won = true;
            Time.timeScale = 0f;
            if (SfxService.Instance != null) SfxService.Instance.Play(SoundCue.Victory);
            newBestScore = PlayerProfile.SubmitClear(SelectedDifficulty, CurrentScore, ElapsedRunSeconds, EnemiesDefeated, PotsBroken, shards, DamageTaken);
            runSubmitted = true;
            resumeSnapshot = null;
            PlayerProfile.ClearRunSnapshot();
            UnlockAchievement("gatewalker");
            if (DamageTaken == 0) UnlockAchievement("unbroken");
            if (GameRules.IsSwiftClear(ElapsedRunSeconds)) UnlockAchievement("swift_blade");
        }

        public void Defeat()
        {
            if (IsFinished)
            {
                return;
            }
            state = RunState.Defeated;
            won = false;
            Time.timeScale = 0f;
            PlayerProfile.SubmitAttempt(SelectedDifficulty, shards, EnemiesDefeated, PotsBroken, DamageTaken, false);
            runSubmitted = true;
            resumeSnapshot = null;
            PlayerProfile.ClearRunSnapshot();
        }

        public void MarkGuardianDefeated()
        {
            IsGuardianDefeated = true;
            UnlockAchievement("warden_slayer");
            ShowToast("守護者を倒した。古代の門が開いた。", 3.5f);
        }

        public void RegisterEnemyDefeated(int scoreValue)
        {
            EnemiesDefeated++;
            combatScore += scoreValue;
            if (EnemiesDefeated >= 8) UnlockAchievement("forest_hunter");
            SaveRunSnapshot();
        }

        public void RegisterCombatHit(int hitCount)
        {
            combo = Mathf.Clamp(combo + Mathf.Max(1, hitCount), 1, 99);
            bestCombo = Mathf.Max(bestCombo, combo);
            comboEndsAt = Time.time + ComboWindowSeconds;
        }

        public void RegisterPotBroken()
        {
            PotsBroken++;
            if (PotsBroken >= 4) UnlockAchievement("potter");
            SaveRunSnapshot();
        }

        public void RegisterDamageTaken(int amount)
        {
            DamageTaken += amount;
            combo = 0;
            SaveRunSnapshot();
        }

        public void NotifyPlayerHealed(int amount)
        {
            if (amount > 0 && Player != null && Player.Health <= 3)
            {
                ShowToast("ライフが回復した。危険なときはダッシュで逃げよう。", 2.3f);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            var previousMatrix = GUI.matrix;
            uiScale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            uiScale = Mathf.Max(0.01f, uiScale);
            uiOffset = new Vector2((Screen.width - ReferenceWidth * uiScale) * 0.5f, (Screen.height - ReferenceHeight * uiScale) * 0.5f);
            GUI.matrix = Matrix4x4.TRS(uiOffset, Quaternion.identity, Vector3.one * uiScale);
            try
            {
                if (state == RunState.Title)
                {
                    DrawTitleScreen();
                    return;
                }

                DrawHud();
                if (IsPlaying) DrawGuidance();
                if (IsPaused) DrawPauseScreen();
                if (IsFinished) DrawResultScreen();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawHud()
        {
            var panel = PanelColor(0.9f);
            GUI.color = panel;
            GUI.Box(new Rect(18f, 18f, 302f, 150f), GUIContent.none);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(34f, 27f, 268f, 28f), "VERDANT BLADE", titleStyle);
            GUI.Label(new Rect(34f, 59f, 268f, 23f), "ライフ  " + HeartText(Player == null ? 0 : Player.Health, Player == null ? 6 : Player.MaxHealth), bodyStyle);
            GUI.Label(new Rect(34f, 83f, 268f, 23f), "太陽の欠片  " + shards + " / " + GameRules.ShardGoal, bodyStyle);
            GUI.Label(new Rect(34f, 108f, 268f, 23f), "スコア  " + CurrentScore + "     時間  " + FormatTime(ElapsedRunSeconds), bodyStyle);
            GUI.Label(new Rect(34f, 134f, 268f, 23f), ObjectiveText(), subtleStyle);

            GUI.color = PanelColor(0.83f);
            GUI.Box(new Rect(ViewWidth - 258f, 18f, 240f, 142f), GUIContent.none);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(ViewWidth - 242f, 28f, 218f, 21f), "移動  WASD / 矢印 / スティック", subtleStyle);
            GUI.Label(new Rect(ViewWidth - 242f, 51f, 218f, 21f), "攻撃  " + GameInput.BindingLabel(GameAction.Attack) + " / クリック / " + GameInput.GamepadHintLabel(GameAction.Attack), subtleStyle);
            GUI.Label(new Rect(ViewWidth - 242f, 74f, 218f, 21f), DashText(), subtleStyle);
            GUI.Label(new Rect(ViewWidth - 242f, 97f, 218f, 21f), "ポーズ  " + GameInput.BindingLabel(GameAction.Pause) + " / P / " + GameInput.GamepadHintLabel(GameAction.Pause), subtleStyle);
            GUI.Label(new Rect(ViewWidth - 242f, 124f, 218f, 21f), combo > 1 ? "フローコンボ  x" + combo : "フローコンボ  連続で攻撃", bodyStyle);

            if (HasAllShards && !IsGuardianDefeated)
            {
                GUI.color = PanelColor(0.9f);
                GUI.Box(new Rect(ViewWidth * 0.5f - 170f, 18f, 340f, 30f), GUIContent.none);
                GUI.color = new Color(1f, 0.66f, 0.4f);
                GUI.Label(new Rect(ViewWidth * 0.5f - 160f, 21f, 320f, 24f), "門の守護者  —  撃破せよ", bodyCenterStyle);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDiagnostics)
            {
                GUI.color = PanelColor(0.86f);
                GUI.Box(new Rect(18f, ViewHeight - 88f, 294f, 62f), GUIContent.none);
                GUI.color = PrimaryTextColor;
                GUI.Label(new Rect(30f, ViewHeight - 82f, 270f, 22f), "開発用  状態=" + state + "  欠片=" + shards + "  コンボ=" + combo + "/" + bestCombo, subtleStyle);
                GUI.Label(new Rect(30f, ViewHeight - 59f, 270f, 22f), "F1 HUD  •  F2 欠片  •  F3 回復", subtleStyle);
            }
#endif
        }

        private void DrawTitleScreen()
        {
            GUI.color = new Color(0.015f, 0.04f, 0.03f, 0.72f);
            GUI.Box(new Rect(0f, 0f, ViewWidth, ViewHeight), GUIContent.none);
            var width = Mathf.Min(600f, ViewWidth - 34f);
            var x = (ViewWidth - width) * 0.5f;
            var panelHeight = titlePage == TitlePage.Settings ? 650f : titlePage == TitlePage.Profile ? (profileAchievementsOpen ? 590f : 550f) : 530f;
            var y = Mathf.Max(20f, (ViewHeight - panelHeight) * 0.5f);
            GUI.color = PanelColor(0.98f);
            GUI.Box(new Rect(x, y, width, panelHeight), GUIContent.none);

            GUI.color = AccentColor;
            GUI.Label(new Rect(x, y + 30f, width, 46f), "VERDANT BLADE", titleCenterStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x, y + 78f, width, 24f), "見下ろし型アクションアドベンチャー", bodyCenterStyle);

            switch (titlePage)
            {
                case TitlePage.Main: DrawTitleMain(x, y, width); break;
                case TitlePage.HowTo: DrawHowTo(x, y, width); break;
                case TitlePage.Settings: DrawSettings(x, y, width, true); break;
                case TitlePage.Profile: DrawProfile(x, y, width); break;
            }
        }

        private void DrawTitleMain(float x, float y, float width)
        {
            GUI.Label(new Rect(x + 34f, y + 112f, width - 68f, 42f), "太陽の欠片を8個集め、門の守護者を倒して古代の門へ戻ろう。", bodyCenterStyle);
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 160f, width - 256f, 32f), 0, "難易度  " + GameRules.DifficultyName(SelectedDifficulty) + "  •  " + GameRules.DifficultyDescription(SelectedDifficulty))) CycleDifficulty();
            var offset = 0f;
            if (resumeSnapshot != null)
            {
                if (DrawTitleMenuButton(new Rect(x + 128f, y + 200f, width - 256f, 34f), 1, "冒険をつづきから  [Enter]")) ResumeRun();
                if (DrawTitleMenuButton(new Rect(x + 128f, y + 240f, width - 256f, 34f), 2, newRunConfirmation ? "確認：保存した冒険を破棄して始める" : "新しい冒険をはじめる")) StartFreshRun();
                offset = 40f;
            }
            else if (DrawTitleMenuButton(new Rect(x + 128f, y + 200f, width - 256f, 34f), 1, "冒険をはじめる  [Enter]")) StartFreshRun();
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 246f + offset, width - 256f, 32f), resumeSnapshot == null ? 2 : 3, "あそびかた")) titlePage = TitlePage.HowTo;
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 286f + offset, width - 256f, 32f), resumeSnapshot == null ? 3 : 4, "設定・アクセシビリティ")) { titlePage = TitlePage.Settings; settingsMenuIndex = 0; }
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 326f + offset, width - 256f, 32f), resumeSnapshot == null ? 4 : 5, "冒険の記録")) { titlePage = TitlePage.Profile; profileDifficulty = SelectedDifficulty; profileMenuIndex = 0; }
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 366f + offset, width - 256f, 28f), resumeSnapshot == null ? 5 : 6, "ゲームを終了")) QuitGame();

            GUI.color = AccentColor;
            GUI.Label(new Rect(x, y + 408f + offset, width, 22f), GameRules.DifficultyName(SelectedDifficulty) + "  最高 " + PlayerProfile.BestScoreFor(SelectedDifficulty) + "     クリア " + PlayerProfile.ClearCountFor(SelectedDifficulty) + "     最多 " + PlayerProfile.FurthestShardCount + " / " + GameRules.ShardGoal, bodyCenterStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x, y + 442f + offset, width, 22f), "矢印キー / D-padで選択  •  Enter / Aで決定", subtleCenterStyle);
            GUI.Label(new Rect(x, y + 472f + offset, width, 18f), "v" + ProductVersion + "  •  オフライン保存  •  Esc / Bで戻る", subtleCenterStyle);
        }

        private void DrawHowTo(float x, float y, float width)
        {
            var lines = new[]
            {
                "1. 設定した移動キー、矢印キー、または左スティックで移動します。",
                "2. " + GameInput.BindingLabel(GameAction.Attack) + "、Z、左クリック、または" + GameInput.GamepadHintLabel(GameAction.Attack) + "で攻撃します。",
                "3. " + GameInput.BindingLabel(GameAction.Dash) + "、X、右クリック、または" + GameInput.GamepadHintLabel(GameAction.Dash) + "でダッシュします。",
                "4. 壺を壊し、ライフブルームを拾うとライフが回復します。",
                "5. 金色のコンパスは、いつでも次の目的地を指します。"
            };
            for (var index = 0; index < lines.Length; index++)
            {
                GUI.Label(new Rect(x + 44f, y + 125f + index * 43f, width - 88f, 38f), lines[index], bodyStyle);
            }
            GUI.color = AccentColor;
            GUI.Label(new Rect(x + 42f, y + 346f, width - 84f, 32f), "ヒント：被弾せず連続で攻撃すると、フローコンボでスコアが増えます。", bodyCenterStyle);
            if (DrawButton(new Rect(x + 180f, y + 406f, width - 360f, 34f), "戻る")) titlePage = TitlePage.Main;
        }

        private void DrawSettings(float x, float y, float width, bool fromTitle)
        {
            if (bindingsOpen)
            {
                DrawBindings(x, y, width, fromTitle);
                return;
            }
            if (gamepadBindingsOpen)
            {
                DrawGamepadBindings(x, y, width, fromTitle);
                return;
            }

            GUI.Label(new Rect(x + 42f, y + 108f, width - 84f, 25f), "設定はこの端末に自動保存されます。", bodyCenterStyle);
            var buttonX = x + 42f;
            var buttonWidth = width - 84f;
            var smallButtonWidth = 92f;
            if (DrawMenuButton(new Rect(buttonX, y + 144f, smallButtonWidth, 30f), 0, settingsMenuIndex, "効果音 −")) { settingsMenuIndex = 0; SetSfxVolume(SfxVolume - 0.1f); }
            GUI.Label(new Rect(buttonX + smallButtonWidth + 4f, y + 144f, buttonWidth - smallButtonWidth * 2f - 8f, 30f), "効果音  " + Mathf.RoundToInt(SfxVolume * 100f) + "%", bodyCenterStyle);
            if (DrawMenuButton(new Rect(buttonX + buttonWidth - smallButtonWidth, y + 144f, smallButtonWidth, 30f), 1, settingsMenuIndex, "効果音 ＋")) { settingsMenuIndex = 1; SetSfxVolume(SfxVolume + 0.1f); }
            if (DrawMenuButton(new Rect(buttonX, y + 180f, smallButtonWidth, 30f), 2, settingsMenuIndex, "音楽 −")) { settingsMenuIndex = 2; SetMusicVolume(MusicVolume - 0.1f); }
            GUI.Label(new Rect(buttonX + smallButtonWidth + 4f, y + 180f, buttonWidth - smallButtonWidth * 2f - 8f, 30f), "音楽  " + Mathf.RoundToInt(MusicVolume * 100f) + "%", bodyCenterStyle);
            if (DrawMenuButton(new Rect(buttonX + buttonWidth - smallButtonWidth, y + 180f, smallButtonWidth, 30f), 3, settingsMenuIndex, "音楽 ＋")) { settingsMenuIndex = 3; SetMusicVolume(MusicVolume + 0.1f); }
            if (DrawMenuButton(new Rect(buttonX, y + 216f, buttonWidth, 28f), 4, settingsMenuIndex, "表示モード     " + DisplayModeLabel(SelectedDisplayMode))) { settingsMenuIndex = 4; CycleDisplayMode(); }
            if (DrawMenuButton(new Rect(buttonX, y + 248f, buttonWidth, 28f), 5, settingsMenuIndex, "解像度     " + ResolutionLabel())) { settingsMenuIndex = 5; CycleResolution(); }
            if (DrawMenuButton(new Rect(buttonX, y + 280f, buttonWidth, 28f), 6, settingsMenuIndex, "垂直同期     " + OnOff(VSyncEnabled))) { settingsMenuIndex = 6; ToggleVSync(); }
            if (DrawMenuButton(new Rect(buttonX, y + 312f, buttonWidth, 28f), 7, settingsMenuIndex, "点滅を抑える     " + OnOff(ReduceFlashing) + "     [F]")) { settingsMenuIndex = 7; ToggleReducedFlashing(); }
            if (DrawMenuButton(new Rect(buttonX, y + 344f, buttonWidth, 28f), 8, settingsMenuIndex, "画面の揺れ     " + OnOff(ScreenShakeEnabled) + "     [C]")) { settingsMenuIndex = 8; ToggleScreenShake(); }
            if (DrawMenuButton(new Rect(buttonX, y + 376f, buttonWidth, 28f), 9, settingsMenuIndex, "高コントラストHUD     " + OnOff(HighContrast) + "     [H]")) { settingsMenuIndex = 9; ToggleHighContrast(); }
            if (DrawMenuButton(new Rect(buttonX, y + 408f, buttonWidth, 28f), 10, settingsMenuIndex, "文字を大きくする     " + OnOff(LargeText) + "     [T]")) { settingsMenuIndex = 10; ToggleLargeText(); }
            if (DrawMenuButton(new Rect(buttonX, y + 440f, buttonWidth, 28f), 11, settingsMenuIndex, "キーボード操作を変更")) { settingsMenuIndex = 11; bindingsOpen = true; keyboardBindingsMenuIndex = 0; }
            if (DrawMenuButton(new Rect(buttonX, y + 472f, buttonWidth, 28f), 12, settingsMenuIndex, "ゲームパッド操作を変更")) { settingsMenuIndex = 12; gamepadBindingsOpen = true; gamepadBindingsMenuIndex = 0; }
            var resetLabel = resetSettingsConfirmation ? "確認：すべての設定と操作を初期状態に戻す" : "設定と操作を初期状態に戻す";
            if (DrawMenuButton(new Rect(buttonX, y + 504f, buttonWidth, 28f), 13, settingsMenuIndex, resetLabel))
            {
                settingsMenuIndex = 13;
                if (resetSettingsConfirmation) { resetSettingsConfirmation = false; ResetAllSettings(); }
                else { resetSettingsConfirmation = true; settingsNotice = "もう一度押すと設定と操作を初期状態に戻します。"; }
            }
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 42f, y + 538f, width - 84f, 20f), "難易度はタイトルで選択。変更はすぐに保存されます。", subtleCenterStyle);
            if (!string.IsNullOrEmpty(settingsNotice)) GUI.Label(new Rect(x + 42f, y + 560f, width - 84f, 20f), settingsNotice, subtleCenterStyle);
            if (DrawMenuButton(new Rect(x + 180f, y + 588f, width - 360f, 28f), 14, settingsMenuIndex, fromTitle ? "戻る" : "ポーズへ戻る"))
            {
                settingsMenuIndex = 14;
                CloseSettings(fromTitle);
            }
        }

        private void DrawProfile(float x, float y, float width)
        {
            if (profileAchievementsOpen)
            {
                DrawAchievementDetails(x, y, width);
                return;
            }
            GUI.Label(new Rect(x + 42f, y + 114f, width - 84f, 24f), "冒険の記録", centerStyle);
            if (DrawMenuButton(new Rect(x + 116f, y + 148f, width - 232f, 30f), 0, profileMenuIndex, "記録の難易度     " + GameRules.DifficultyName(profileDifficulty)))
            {
                profileMenuIndex = 0;
                profileDifficulty = GameRules.NextDifficulty(profileDifficulty);
            }
            GUI.Label(new Rect(x + 72f, y + 190f, width - 144f, 22f), "挑戦  " + PlayerProfile.AttemptCountFor(profileDifficulty) + "      クリア  " + PlayerProfile.ClearCountFor(profileDifficulty) + "      最高  " + PlayerProfile.BestScoreFor(profileDifficulty), bodyStyle);
            GUI.Label(new Rect(x + 72f, y + 216f, width - 144f, 22f), "欠片  " + PlayerProfile.TotalShardsCollected + "      敵  " + PlayerProfile.TotalEnemiesDefeated + "      壺  " + PlayerProfile.TotalPotsBroken, bodyStyle);
            var bestClearTime = PlayerProfile.BestClearTimeFor(profileDifficulty);
            var fastest = bestClearTime < 0f ? "--:--" : FormatTime(bestClearTime);
            GUI.Label(new Rect(x + 72f, y + 242f, width - 144f, 22f), "最速クリア  " + fastest + "      被ダメージ  " + PlayerProfile.TotalDamageTaken, bodyStyle);

            GUI.color = AccentColor;
            GUI.Label(new Rect(x + 42f, y + 278f, width - 84f, 22f), "実績  " + UnlockedAchievementCount() + " / " + AchievementIds.Length + "     中断 " + PlayerProfile.AbandonedCount, bodyCenterStyle);
            GUI.color = PrimaryTextColor;
            if (DrawMenuButton(new Rect(x + 116f, y + 308f, width - 232f, 30f), 1, profileMenuIndex, "実績一覧と解除条件を見る"))
            {
                profileMenuIndex = 1;
                profileAchievementsOpen = true;
            }
            if (!string.IsNullOrEmpty(profileNotice))
            {
                GUI.color = new Color(1f, 0.74f, 0.48f);
                GUI.Label(new Rect(x + 42f, y + 348f, width - 84f, 22f), profileNotice, subtleCenterStyle);
            }
            if (DrawMenuButton(new Rect(x + 72f, y + 382f, width - 144f, 30f), 2, profileMenuIndex, resetConfirmation ? "確認：冒険の記録を消去する" : "冒険の記録をリセット"))
            {
                profileMenuIndex = 2;
                if (resetConfirmation)
                {
                    ResetProgressAndReload();
                }
                else
                {
                    resetConfirmation = true;
                    profileNotice = "もう一度押すと消去します。この操作は元に戻せません。";
                }
            }
            if (DrawMenuButton(new Rect(x + 180f, y + 432f, width - 360f, 32f), 3, profileMenuIndex, "戻る"))
            {
                profileMenuIndex = 3;
                ReturnToTitleMain();
            }
        }

        private void DrawAchievementDetails(float x, float y, float width)
        {
            GUI.Label(new Rect(x + 42f, y + 112f, width - 84f, 28f), "実績一覧", titleCenterStyle);
            for (var index = 0; index < AchievementIds.Length; index++)
            {
                var column = index / 4;
                var row = index % 4;
                var cardWidth = (width - 104f) * 0.5f;
                var cardX = x + 42f + column * (cardWidth + 20f);
                var cardY = y + 164f + row * 72f;
                var unlocked = PlayerProfile.IsAchievementUnlocked(AchievementIds[index]);
                GUI.color = unlocked ? new Color(0.17f, 0.34f, 0.23f, 0.96f) : PanelColor(0.84f);
                GUI.Box(new Rect(cardX, cardY, cardWidth, 60f), GUIContent.none);
                GUI.color = unlocked ? AccentColor : PrimaryTextColor;
                GUI.Label(new Rect(cardX + 12f, cardY + 5f, cardWidth - 24f, 21f), (unlocked ? "解除済  " : "未解除  ") + AchievementName(AchievementIds[index]), bodyStyle);
                GUI.color = PrimaryTextColor;
                GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardWidth - 24f, 25f), AchievementDescription(AchievementIds[index]), subtleStyle);
            }
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 42f, y + 468f, width - 84f, 22f), "B / Esc / Enter / A で戻る", subtleCenterStyle);
            if (DrawButton(new Rect(x + 180f, y + 500f, width - 360f, 30f), "戻る")) profileAchievementsOpen = false;
        }

        private void DrawGuidance()
        {
            var message = Time.unscaledTime < toastEndsAt ? toastMessage : ContextHint();
            if (string.IsNullOrEmpty(message)) return;
            GUI.color = PanelColor(0.82f);
            GUI.Box(new Rect(ViewWidth * 0.5f - 245f, ViewHeight - 68f, 490f, 40f), GUIContent.none);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(ViewWidth * 0.5f - 235f, ViewHeight - 63f, 470f, 29f), message, bodyCenterStyle);
        }

        private void DrawPauseScreen()
        {
            GUI.color = new Color(0.01f, 0.03f, 0.025f, 0.77f);
            GUI.Box(new Rect(0f, 0f, ViewWidth, ViewHeight), GUIContent.none);
            var width = Mathf.Min(500f, ViewWidth - 36f);
            var x = (ViewWidth - width) * 0.5f;
            var panelHeight = pauseSettingsOpen ? 650f : 430f;
            var y = Mathf.Max(32f, (ViewHeight - panelHeight) * 0.5f);
            GUI.color = PanelColor(0.98f);
            GUI.Box(new Rect(x, y, width, panelHeight), GUIContent.none);
            GUI.color = AccentColor;
            GUI.Label(new Rect(x, y + 24f, width, 32f), pauseSettingsOpen ? "設定" : "ポーズ中", titleCenterStyle);
            GUI.color = PrimaryTextColor;
            if (pauseSettingsOpen)
            {
                DrawSettings(x, y - 38f, width, false);
                return;
            }

            GUI.Label(new Rect(x, y + 64f, width, 24f), pausedByFocusLoss ? "アプリが非アクティブになったため、安全にポーズしました。" : "冒険は安全にポーズされています。", bodyCenterStyle);
            if (DrawMenuButton(new Rect(x + 92f, y + 112f, width - 184f, 34f), 0, pauseMenuIndex, "再開  [ESC / P]")) { pauseMenuIndex = 0; TogglePause(); }
            if (DrawMenuButton(new Rect(x + 92f, y + 154f, width - 184f, 30f), 1, pauseMenuIndex, "設定・アクセシビリティ")) { pauseMenuIndex = 1; pauseSettingsOpen = true; settingsMenuIndex = 0; }
            if (DrawMenuButton(new Rect(x + 92f, y + 194f, width - 184f, 30f), 2, pauseMenuIndex, "最初からやり直す")) { pauseMenuIndex = 2; RestartRun(); }
            if (DrawMenuButton(new Rect(x + 92f, y + 234f, width - 184f, 30f), 3, pauseMenuIndex, "中断してタイトルへ")) { pauseMenuIndex = 3; ReturnToTitle(); }
            if (DrawMenuButton(new Rect(x + 92f, y + 274f, width - 184f, 28f), 4, pauseMenuIndex, "ゲームを終了")) { pauseMenuIndex = 4; QuitGame(); }
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 38f, y + 326f, width - 76f, 22f), "目的：" + ObjectiveText().Replace("目的：", string.Empty), subtleCenterStyle);
            GUI.Label(new Rect(x + 38f, y + 353f, width - 76f, 22f), "今回のスコア " + CurrentScore + "  •  敵 " + EnemiesDefeated + "  •  被ダメージ " + DamageTaken, subtleCenterStyle);
        }

        private void DrawResultScreen()
        {
            GUI.color = new Color(0.01f, 0.03f, 0.025f, 0.82f);
            GUI.Box(new Rect(0f, 0f, ViewWidth, ViewHeight), GUIContent.none);
            var width = Mathf.Min(540f, ViewWidth - 36f);
            var x = (ViewWidth - width) * 0.5f;
            var y = Mathf.Max(28f, (ViewHeight - 440f) * 0.5f);
            GUI.color = PanelColor(0.98f);
            GUI.Box(new Rect(x, y, width, 440f), GUIContent.none);
            GUI.color = won ? AccentColor : new Color(1f, 0.48f, 0.46f);
            GUI.Label(new Rect(x, y + 18f, width, 34f), won ? "古代の門が目覚めた" : "森は静寂に包まれた", centerStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x, y + 54f, width, 24f), won ? "森へ続く道が開かれた。" : "力を蓄えて、もう一度挑もう。", bodyCenterStyle);
            GUI.Label(new Rect(x + 48f, y + 94f, width - 96f, 22f), "スコア  " + CurrentScore + (newBestScore ? "    自己ベスト！" : string.Empty), bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 120f, width - 96f, 22f), "時間  " + FormatTime(ElapsedRunSeconds) + "    " + GameRules.DifficultyName(SelectedDifficulty) + "    フロー " + ComboBonus, bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 146f, width - 96f, 22f), "敵  " + EnemiesDefeated + "    壺  " + PotsBroken + "    被ダメージ  " + DamageTaken, bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 172f, width - 96f, 22f), "最高スコア  " + PlayerProfile.BestScoreFor(SelectedDifficulty) + "    クリア  " + PlayerProfile.ClearCountFor(SelectedDifficulty), bodyStyle);
            var bestClearTime = PlayerProfile.BestClearTimeFor(SelectedDifficulty);
            var fastest = bestClearTime < 0f ? "--:--" : FormatTime(bestClearTime);
            GUI.Label(new Rect(x + 48f, y + 198f, width - 96f, 22f), "最速クリア  " + fastest, bodyStyle);
            DrawNewAchievements(x, y + 236f, width);
            if (DrawMenuButton(new Rect(x + 80f, y + 332f, width - 160f, 32f), 0, resultMenuIndex, "もう一度あそぶ  [R / ENTER]")) { resultMenuIndex = 0; RestartRun(); }
            if (DrawMenuButton(new Rect(x + 80f, y + 370f, width - 160f, 28f), 1, resultMenuIndex, "タイトルへ戻る")) { resultMenuIndex = 1; ReturnToTitle(); }
            if (DrawMenuButton(new Rect(x + 80f, y + 404f, width - 160f, 24f), 2, resultMenuIndex, "ゲームを終了")) { resultMenuIndex = 2; QuitGame(); }
        }

        private void DrawNewAchievements(float x, float y, float width)
        {
            if (newAchievements.Count == 0)
            {
                GUI.color = PrimaryTextColor;
                GUI.Label(new Rect(x + 48f, y, width - 96f, 22f), "追加の目標を達成すると実績を解除できます。", subtleCenterStyle);
                return;
            }
            GUI.color = AccentColor;
            GUI.Label(new Rect(x, y, width, 22f), "新しい実績" + (newAchievements.Count > 1 ? "（複数）" : string.Empty), bodyCenterStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 48f, y + 26f, width - 96f, 42f), string.Join("  •  ", newAchievements), subtleCenterStyle);
        }

        private void DrawBindings(float x, float y, float width, bool fromTitle)
        {
            var buttonX = x + 42f;
            var buttonWidth = width - 84f;
            var columnWidth = (buttonWidth - 8f) * 0.5f;
            var waitingText = pendingBinding == GameAction.None
                ? "操作を選び、割り当てるキーを押してください。"
                : "「" + ActionLabel(pendingBinding) + "」に割り当てるキーを押してください（Escも可）。";
            GUI.color = pendingBinding == GameAction.None ? PrimaryTextColor : AccentColor;
            GUI.Label(new Rect(buttonX, y + 112f, buttonWidth, 28f), waitingText, bodyCenterStyle);
            GUI.color = PrimaryTextColor;

            DrawBindingButton(new Rect(buttonX, y + 150f, columnWidth, 30f), GameAction.MoveUp, 0);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 150f, columnWidth, 30f), GameAction.MoveDown, 1);
            DrawBindingButton(new Rect(buttonX, y + 186f, columnWidth, 30f), GameAction.MoveLeft, 2);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 186f, columnWidth, 30f), GameAction.MoveRight, 3);
            DrawBindingButton(new Rect(buttonX, y + 222f, columnWidth, 30f), GameAction.Attack, 4);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 222f, columnWidth, 30f), GameAction.Dash, 5);
            DrawBindingButton(new Rect(buttonX, y + 258f, buttonWidth, 30f), GameAction.Pause, 6);

            if (pendingBinding != GameAction.None && DrawButton(new Rect(buttonX, y + 300f, buttonWidth, 28f), "割り当てを中止"))
            {
                pendingBinding = GameAction.None;
                settingsNotice = "キーの割り当てを中止しました。";
            }
            if (DrawMenuButton(new Rect(buttonX, y + 340f, buttonWidth, 28f), 7, keyboardBindingsMenuIndex, "初期設定に戻す"))
            {
                keyboardBindingsMenuIndex = 7;
                GameInput.ResetBindings();
                pendingBinding = GameAction.None;
                settingsNotice = "キー設定を初期状態に戻しました。";
            }
            if (!string.IsNullOrEmpty(settingsNotice)) GUI.Label(new Rect(buttonX, y + 374f, buttonWidth, 20f), settingsNotice, subtleCenterStyle);
            if (DrawMenuButton(new Rect(x + 180f, y + 414f, width - 360f, 28f), 8, keyboardBindingsMenuIndex, fromTitle ? "設定に戻る" : "ポーズ設定に戻る"))
            {
                keyboardBindingsMenuIndex = 8;
                pendingBinding = GameAction.None;
                bindingsOpen = false;
            }
        }

        private void DrawBindingButton(Rect rect, GameAction action, int index)
        {
            var prefix = pendingBinding == action ? "入力待機中  " : string.Empty;
            if (DrawMenuButton(rect, index, keyboardBindingsMenuIndex, prefix + ActionLabel(action) + "  " + GameInput.BindingLabel(action)))
            {
                keyboardBindingsMenuIndex = index;
                pendingBinding = action;
                settingsNotice = string.Empty;
            }
        }

        private void DrawGamepadBindings(float x, float y, float width, bool fromTitle)
        {
            var buttonX = x + 42f;
            var buttonWidth = width - 84f;
            var waitingText = pendingGamepadBinding == GameAction.None
                ? "変更する操作を選び、ゲームパッドのボタンを押してください。"
                : "「" + ActionLabel(pendingGamepadBinding) + "」に割り当てるボタンを押してください。";
            GUI.color = pendingGamepadBinding == GameAction.None ? PrimaryTextColor : AccentColor;
            GUI.Label(new Rect(buttonX, y + 112f, buttonWidth, 36f), waitingText, bodyCenterStyle);
            GUI.color = PrimaryTextColor;

            DrawGamepadBindingButton(new Rect(buttonX, y + 164f, buttonWidth, 32f), GameAction.Attack, 0);
            DrawGamepadBindingButton(new Rect(buttonX, y + 204f, buttonWidth, 32f), GameAction.Dash, 1);
            DrawGamepadBindingButton(new Rect(buttonX, y + 244f, buttonWidth, 32f), GameAction.Pause, 2);
            if (pendingGamepadBinding != GameAction.None && DrawButton(new Rect(buttonX, y + 286f, buttonWidth, 28f), "割り当てを中止"))
            {
                pendingGamepadBinding = GameAction.None;
                settingsNotice = "ゲームパッドの割り当てを中止しました。";
            }
            if (DrawMenuButton(new Rect(buttonX, y + 332f, buttonWidth, 28f), 3, gamepadBindingsMenuIndex, "初期設定に戻す"))
            {
                gamepadBindingsMenuIndex = 3;
                GameInput.ResetGamepadBindings();
                pendingGamepadBinding = GameAction.None;
                settingsNotice = "ゲームパッド設定を初期状態に戻しました。";
            }
            if (!string.IsNullOrEmpty(settingsNotice)) GUI.Label(new Rect(buttonX, y + 370f, buttonWidth, 20f), settingsNotice, subtleCenterStyle);
            if (DrawMenuButton(new Rect(x + 180f, y + 414f, width - 360f, 28f), 4, gamepadBindingsMenuIndex, fromTitle ? "設定に戻る" : "ポーズ設定に戻る"))
            {
                gamepadBindingsMenuIndex = 4;
                pendingGamepadBinding = GameAction.None;
                gamepadBindingsOpen = false;
            }
        }

        private void DrawGamepadBindingButton(Rect rect, GameAction action, int index)
        {
            var prefix = pendingGamepadBinding == action ? "入力待機中  " : string.Empty;
            if (DrawMenuButton(rect, index, gamepadBindingsMenuIndex, prefix + ActionLabel(action) + "  " + GameInput.GamepadBindingLabel(action)))
            {
                gamepadBindingsMenuIndex = index;
                pendingGamepadBinding = action;
                settingsNotice = string.Empty;
            }
        }

        private bool DrawTitleMenuButton(Rect rect, int index, string label)
        {
            return DrawMenuButton(rect, index, titleMenuIndex, label, 0.36f);
        }

        private bool DrawMenuButton(Rect rect, int index, int selectedIndex, string label, float selectionAlpha = 0.46f)
        {
            if (selectedIndex == index)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, selectionAlpha);
                GUI.Box(new Rect(rect.x - 4f, rect.y - 3f, rect.width + 8f, rect.height + 6f), GUIContent.none);
                GUI.color = previousColor;
            }
            return DrawButton(rect, (selectedIndex == index ? ">  " : string.Empty) + label);
        }

        private bool DrawButton(Rect rect, string text)
        {
            // GUI.color tints both the button background and its label. Keep it white here
            // and tint only the background, otherwise dark button colors make text unreadable.
            var previousColor = GUI.color;
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.color = Color.white;
            var pointer = Event.current == null ? Vector2.zero : (Event.current.mousePosition - uiOffset) / uiScale;
            var hovered = Event.current != null && rect.Contains(pointer);
            GUI.backgroundColor = HighContrast
                ? (hovered ? new Color(0.38f, 0.46f, 0.31f, 1f) : new Color(0.26f, 0.31f, 0.23f, 1f))
                : (hovered ? new Color(0.27f, 0.43f, 0.3f, 1f) : new Color(0.18f, 0.3f, 0.21f, 1f));
            GUI.contentColor = Color.white;
            var clicked = GUI.Button(rect, text, buttonStyle);
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            if (clicked && SfxService.Instance != null) SfxService.Instance.Play(SoundCue.MenuConfirm);
            return clicked;
        }

    }
}
