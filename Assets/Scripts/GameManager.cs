using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public sealed class GameManager : MonoBehaviour
    {
        private enum TitlePage { Main, HowTo, Settings, Profile }

        public static GameManager Instance { get; private set; }
        public static readonly string[] AchievementIds = { "first_steps", "shard_seeker", "warden_slayer", "gatewalker", "unbroken", "swift_blade", "forest_hunter", "potter" };

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
        public int ShardCount => shards;
        public int EnemiesDefeated { get; private set; }
        public int PotsBroken { get; private set; }
        public int DamageTaken { get; private set; }
        public int Combo => combo;
        public int BestCombo => bestCombo;
        public float ElapsedRunSeconds => state == RunState.Title ? 0f : (IsFinished ? finishedAt - runStartedAt : Time.unscaledTime - runStartedAt);
        public int CurrentScore => GameRules.CalculateScore(shards, combatScore, PotsBroken, Player == null ? 0 : Player.Health, bestCombo, state == RunState.Won, ElapsedRunSeconds);

        private const float ComboWindowSeconds = 3.5f;
        private RunState state = RunState.Title;
        private TitlePage titlePage;
        private int shards;
        private bool won;
        private float runStartedAt;
        private float finishedAt;
        private float toastEndsAt;
        private string toastMessage;
        private int combatScore;
        private int combo;
        private int bestCombo;
        private float comboEndsAt;
        private bool newBestScore;
        private bool pausedByFocusLoss;
        private bool pauseSettingsOpen;
        private bool bindingsOpen;
        private GameAction pendingBinding;
        private int titleMenuIndex = 1;
        private bool resetConfirmation;
        private string profileNotice;
        private string settingsNotice;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool showDiagnostics;
#endif
        private readonly List<string> newAchievements = new List<string>();
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

        private int ComboBonus => bestCombo < 2 ? 0 : bestCombo * 10;

        private void Awake()
        {
            Instance = this;
            SfxVolume = PlayerProfile.LoadSfxVolume();
            ReduceFlashing = PlayerProfile.LoadReduceFlashing();
            ScreenShakeEnabled = PlayerProfile.LoadScreenShake();
            HighContrast = PlayerProfile.LoadHighContrast();
            LargeText = PlayerProfile.LoadLargeText();
            SelectedDifficulty = PlayerProfile.LoadDifficulty();
            Time.timeScale = 0f;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandleDevelopmentShortcuts();
#endif
            if (CapturePendingBinding()) return;
            if (state == RunState.Title)
            {
                if (titlePage == TitlePage.Settings && !bindingsOpen) HandleSettingsShortcuts();
                if (titlePage == TitlePage.Main)
                {
                    HandleTitleMenuInput();
                }
                else if (titlePage != TitlePage.Main && GameInput.MenuBackPressed && !bindingsOpen)
                {
                    titlePage = TitlePage.Main;
                }
                return;
            }

            if (IsFinished)
            {
                if (GameInput.RestartPressed)
                {
                    RestartRun();
                }
                return;
            }

            if (GameInput.PausePressed)
            {
                TogglePause();
            }

            if (IsPlaying && combo > 0 && Time.time > comboEndsAt)
            {
                combo = 0;
            }

            if (IsPaused && pauseSettingsOpen)
            {
                HandleSettingsShortcuts();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
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
        }

        public void StartRun()
        {
            if (state != RunState.Title)
            {
                return;
            }

            state = RunState.Playing;
            Time.timeScale = 1f;
            runStartedAt = Time.unscaledTime;
            UnlockAchievement("first_steps");
            ShowToast("森に散らばる太陽の欠片を集めよう。", 4f);
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
            finishedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            if (SfxService.Instance != null) SfxService.Instance.Play(SoundCue.Victory);
            newBestScore = PlayerProfile.SubmitClear(CurrentScore, ElapsedRunSeconds, EnemiesDefeated, PotsBroken, shards, DamageTaken);
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
            finishedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            PlayerProfile.SubmitAttempt(shards, EnemiesDefeated, PotsBroken, DamageTaken);
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
        }

        public void RegisterDamageTaken(int amount)
        {
            DamageTaken += amount;
            combo = 0;
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
            GUI.Box(new Rect(Screen.width - 258f, 18f, 240f, 142f), GUIContent.none);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(Screen.width - 242f, 28f, 218f, 21f), "移動  WASD / 矢印 / スティック", subtleStyle);
            GUI.Label(new Rect(Screen.width - 242f, 51f, 218f, 21f), "攻撃  " + GameInput.BindingLabel(GameAction.Attack) + " / クリック / A", subtleStyle);
            GUI.Label(new Rect(Screen.width - 242f, 74f, 218f, 21f), DashText(), subtleStyle);
            GUI.Label(new Rect(Screen.width - 242f, 97f, 218f, 21f), "ポーズ  " + GameInput.BindingLabel(GameAction.Pause) + " / P / START", subtleStyle);
            GUI.Label(new Rect(Screen.width - 242f, 124f, 218f, 21f), combo > 1 ? "フローコンボ  x" + combo : "フローコンボ  連続で攻撃", bodyStyle);

            if (HasAllShards && !IsGuardianDefeated)
            {
                GUI.color = PanelColor(0.9f);
                GUI.Box(new Rect(Screen.width * 0.5f - 170f, 18f, 340f, 30f), GUIContent.none);
                GUI.color = new Color(1f, 0.66f, 0.4f);
                GUI.Label(new Rect(Screen.width * 0.5f - 160f, 21f, 320f, 24f), "門の守護者  —  撃破せよ", bodyCenterStyle);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDiagnostics)
            {
                GUI.color = PanelColor(0.86f);
                GUI.Box(new Rect(18f, Screen.height - 88f, 294f, 62f), GUIContent.none);
                GUI.color = PrimaryTextColor;
                GUI.Label(new Rect(30f, Screen.height - 82f, 270f, 22f), "開発用  状態=" + state + "  欠片=" + shards + "  コンボ=" + combo + "/" + bestCombo, subtleStyle);
                GUI.Label(new Rect(30f, Screen.height - 59f, 270f, 22f), "F1 HUD  •  F2 欠片  •  F3 回復", subtleStyle);
            }
#endif
        }

        private void DrawTitleScreen()
        {
            GUI.color = new Color(0.015f, 0.04f, 0.03f, 0.72f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            var width = Mathf.Min(600f, Screen.width - 34f);
            var x = (Screen.width - width) * 0.5f;
            var y = Mathf.Max(26f, Screen.height * 0.12f);
            GUI.color = PanelColor(0.98f);
            GUI.Box(new Rect(x, y, width, 472f), GUIContent.none);

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
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 200f, width - 256f, 38f), 1, "冒険をはじめる  [Enter]")) StartRun();
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 246f, width - 256f, 32f), 2, "あそびかた")) titlePage = TitlePage.HowTo;
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 286f, width - 256f, 32f), 3, "設定・アクセシビリティ")) titlePage = TitlePage.Settings;
            if (DrawTitleMenuButton(new Rect(x + 128f, y + 326f, width - 256f, 32f), 4, "冒険の記録")) titlePage = TitlePage.Profile;

            GUI.color = AccentColor;
            GUI.Label(new Rect(x, y + 374f, width, 22f), "最高スコア  " + PlayerProfile.BestScore + "     クリア  " + PlayerProfile.ClearCount + "     最多  " + PlayerProfile.FurthestShardCount + " / " + GameRules.ShardGoal, bodyCenterStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x, y + 406f, width, 22f), "矢印キー / D-padで選択  •  Enter / Aで決定", subtleCenterStyle);
        }

        private void DrawHowTo(float x, float y, float width)
        {
            var lines = new[]
            {
                "1. 設定した移動キー、矢印キー、または左スティックで移動します。",
                "2. " + GameInput.BindingLabel(GameAction.Attack) + "、Z、左クリック、またはAで攻撃します。",
                "3. " + GameInput.BindingLabel(GameAction.Dash) + "、X、右クリック、またはBでダッシュします。",
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

            GUI.Label(new Rect(x + 42f, y + 112f, width - 84f, 25f), "設定はこの端末に自動保存されます。", bodyCenterStyle);
            var buttonX = x + 42f;
            var buttonWidth = width - 84f;
            if (DrawButton(new Rect(buttonX, y + 150f, 84f, 32f), "効果音 −")) SetSfxVolume(SfxVolume - 0.1f);
            GUI.Label(new Rect(buttonX + 88f, y + 150f, buttonWidth - 176f, 32f), "効果音  " + Mathf.RoundToInt(SfxVolume * 100f) + "%", bodyCenterStyle);
            if (DrawButton(new Rect(buttonX + buttonWidth - 84f, y + 150f, 84f, 32f), "効果音 ＋")) SetSfxVolume(SfxVolume + 0.1f);
            if (DrawButton(new Rect(buttonX, y + 188f, buttonWidth, 30f), "点滅を抑える     " + OnOff(ReduceFlashing) + "     [F]")) ToggleReducedFlashing();
            if (DrawButton(new Rect(buttonX, y + 224f, buttonWidth, 30f), "画面の揺れ     " + OnOff(ScreenShakeEnabled) + "     [C]")) ToggleScreenShake();
            if (DrawButton(new Rect(buttonX, y + 260f, buttonWidth, 30f), "高コントラストHUD     " + OnOff(HighContrast) + "     [H]")) ToggleHighContrast();
            if (DrawButton(new Rect(buttonX, y + 296f, buttonWidth, 30f), "文字を大きくする     " + OnOff(LargeText) + "     [T]")) ToggleLargeText();
            if (DrawButton(new Rect(buttonX, y + 332f, buttonWidth, 30f), "キーボード操作")) bindingsOpen = true;
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 42f, y + 370f, width - 84f, 30f), "点滅・揺れはすぐに反映されます。難易度はタイトル画面で選択します。", subtleCenterStyle);
            if (!string.IsNullOrEmpty(settingsNotice)) GUI.Label(new Rect(x + 42f, y + 396f, width - 84f, 18f), settingsNotice, subtleCenterStyle);
            if (DrawButton(new Rect(x + 180f, y + 430f, width - 360f, 28f), fromTitle ? "戻る" : "ポーズへ戻る"))
            {
                bindingsOpen = false;
                if (fromTitle) titlePage = TitlePage.Main;
                else pauseSettingsOpen = false;
            }
        }

        private void DrawProfile(float x, float y, float width)
        {
            GUI.Label(new Rect(x + 42f, y + 114f, width - 84f, 24f), "冒険の記録", centerStyle);
            GUI.Label(new Rect(x + 72f, y + 153f, width - 144f, 22f), "挑戦  " + PlayerProfile.AttemptCount + "      クリア  " + PlayerProfile.ClearCount + "      最高  " + PlayerProfile.BestScore, bodyStyle);
            GUI.Label(new Rect(x + 72f, y + 179f, width - 144f, 22f), "欠片  " + PlayerProfile.TotalShardsCollected + "      敵  " + PlayerProfile.TotalEnemiesDefeated + "      壺  " + PlayerProfile.TotalPotsBroken, bodyStyle);
            var fastest = PlayerProfile.BestClearTime < 0f ? "--:--" : FormatTime(PlayerProfile.BestClearTime);
            GUI.Label(new Rect(x + 72f, y + 205f, width - 144f, 22f), "最速クリア  " + fastest + "      被ダメージ  " + PlayerProfile.TotalDamageTaken, bodyStyle);

            GUI.color = AccentColor;
            GUI.Label(new Rect(x + 42f, y + 248f, width - 84f, 22f), "実績  " + UnlockedAchievementCount() + " / " + AchievementIds.Length, bodyCenterStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 72f, y + 276f, width - 144f, 22f), AchievementProgressText(), subtleCenterStyle);
            if (!string.IsNullOrEmpty(profileNotice))
            {
                GUI.color = new Color(1f, 0.74f, 0.48f);
                GUI.Label(new Rect(x + 42f, y + 306f, width - 84f, 22f), profileNotice, subtleCenterStyle);
            }
            if (DrawButton(new Rect(x + 72f, y + 344f, width - 144f, 30f), resetConfirmation ? "確認：冒険の記録を消去する" : "冒険の記録をリセット"))
            {
                if (resetConfirmation)
                {
                    PlayerProfile.ResetProgress();
                    resetConfirmation = false;
                    profileNotice = "冒険の記録を消去しました。設定は保持されています。";
                }
                else
                {
                    resetConfirmation = true;
                    profileNotice = "もう一度押すと消去します。この操作は元に戻せません。";
                }
            }
            if (DrawButton(new Rect(x + 180f, y + 410f, width - 360f, 32f), "戻る"))
            {
                resetConfirmation = false;
                titlePage = TitlePage.Main;
            }
        }

        private void DrawGuidance()
        {
            var message = Time.unscaledTime < toastEndsAt ? toastMessage : ContextHint();
            if (string.IsNullOrEmpty(message)) return;
            GUI.color = PanelColor(0.82f);
            GUI.Box(new Rect(Screen.width * 0.5f - 245f, Screen.height - 68f, 490f, 40f), GUIContent.none);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(Screen.width * 0.5f - 235f, Screen.height - 63f, 470f, 29f), message, bodyCenterStyle);
        }

        private void DrawPauseScreen()
        {
            GUI.color = new Color(0.01f, 0.03f, 0.025f, 0.77f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            var width = Mathf.Min(500f, Screen.width - 36f);
            var x = (Screen.width - width) * 0.5f;
            var panelHeight = pauseSettingsOpen ? 430f : 390f;
            var y = Mathf.Max(32f, (Screen.height - panelHeight) * 0.5f);
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
            if (DrawButton(new Rect(x + 92f, y + 112f, width - 184f, 34f), "再開  [ESC / P]")) TogglePause();
            if (DrawButton(new Rect(x + 92f, y + 154f, width - 184f, 30f), "設定・アクセシビリティ")) pauseSettingsOpen = true;
            if (DrawButton(new Rect(x + 92f, y + 194f, width - 184f, 30f), "最初からやり直す")) RestartRun();
            if (DrawButton(new Rect(x + 92f, y + 234f, width - 184f, 30f), "タイトルへ戻る")) ReturnToTitle();
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x + 38f, y + 286f, width - 76f, 22f), "目的：" + ObjectiveText().Replace("目的：", string.Empty), subtleCenterStyle);
            GUI.Label(new Rect(x + 38f, y + 313f, width - 76f, 22f), "今回のスコア " + CurrentScore + "  •  敵 " + EnemiesDefeated + "  •  被ダメージ " + DamageTaken, subtleCenterStyle);
        }

        private void DrawResultScreen()
        {
            GUI.color = new Color(0.01f, 0.03f, 0.025f, 0.82f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            var width = Mathf.Min(540f, Screen.width - 36f);
            var x = (Screen.width - width) * 0.5f;
            var y = Mathf.Max(28f, (Screen.height - 402f) * 0.5f);
            GUI.color = PanelColor(0.98f);
            GUI.Box(new Rect(x, y, width, 402f), GUIContent.none);
            GUI.color = won ? AccentColor : new Color(1f, 0.48f, 0.46f);
            GUI.Label(new Rect(x, y + 18f, width, 34f), won ? "古代の門が目覚めた" : "森は静寂に包まれた", centerStyle);
            GUI.color = PrimaryTextColor;
            GUI.Label(new Rect(x, y + 54f, width, 24f), won ? "森へ続く道が開かれた。" : "力を蓄えて、もう一度挑もう。", bodyCenterStyle);
            GUI.Label(new Rect(x + 48f, y + 94f, width - 96f, 22f), "スコア  " + CurrentScore + (newBestScore ? "    自己ベスト！" : string.Empty), bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 120f, width - 96f, 22f), "時間  " + FormatTime(ElapsedRunSeconds) + "    " + GameRules.DifficultyName(SelectedDifficulty) + "    フロー " + ComboBonus, bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 146f, width - 96f, 22f), "敵  " + EnemiesDefeated + "    壺  " + PotsBroken + "    被ダメージ  " + DamageTaken, bodyStyle);
            GUI.Label(new Rect(x + 48f, y + 172f, width - 96f, 22f), "最高スコア  " + PlayerProfile.BestScore + "    クリア  " + PlayerProfile.ClearCount, bodyStyle);
            var fastest = PlayerProfile.BestClearTime < 0f ? "--:--" : FormatTime(PlayerProfile.BestClearTime);
            GUI.Label(new Rect(x + 48f, y + 198f, width - 96f, 22f), "最速クリア  " + fastest, bodyStyle);
            DrawNewAchievements(x, y + 236f, width);
            if (DrawButton(new Rect(x + 80f, y + 332f, width - 160f, 32f), "もう一度あそぶ  [R / ENTER]")) RestartRun();
            if (DrawButton(new Rect(x + 80f, y + 370f, width - 160f, 24f), "タイトルへ戻る")) ReturnToTitle();
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
            GUI.Label(new Rect(x + 48f, y + 26f, width - 96f, 42f), string.Join("  •  ", newAchievements.ToArray()), subtleCenterStyle);
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

            DrawBindingButton(new Rect(buttonX, y + 150f, columnWidth, 30f), GameAction.MoveUp);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 150f, columnWidth, 30f), GameAction.MoveDown);
            DrawBindingButton(new Rect(buttonX, y + 186f, columnWidth, 30f), GameAction.MoveLeft);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 186f, columnWidth, 30f), GameAction.MoveRight);
            DrawBindingButton(new Rect(buttonX, y + 222f, columnWidth, 30f), GameAction.Attack);
            DrawBindingButton(new Rect(buttonX + columnWidth + 8f, y + 222f, columnWidth, 30f), GameAction.Dash);
            DrawBindingButton(new Rect(buttonX, y + 258f, buttonWidth, 30f), GameAction.Pause);

            if (pendingBinding != GameAction.None && DrawButton(new Rect(buttonX, y + 300f, buttonWidth, 28f), "割り当てを中止"))
            {
                pendingBinding = GameAction.None;
                settingsNotice = "キーの割り当てを中止しました。";
            }
            if (DrawButton(new Rect(buttonX, y + 340f, buttonWidth, 28f), "初期設定に戻す"))
            {
                GameInput.ResetBindings();
                pendingBinding = GameAction.None;
                settingsNotice = "キー設定を初期状態に戻しました。";
            }
            if (!string.IsNullOrEmpty(settingsNotice)) GUI.Label(new Rect(buttonX, y + 374f, buttonWidth, 20f), settingsNotice, subtleCenterStyle);
            if (DrawButton(new Rect(x + 180f, y + 414f, width - 360f, 28f), fromTitle ? "設定に戻る" : "ポーズ設定に戻る"))
            {
                pendingBinding = GameAction.None;
                bindingsOpen = false;
            }
        }

        private void DrawBindingButton(Rect rect, GameAction action)
        {
            var prefix = pendingBinding == action ? "入力待機中  " : string.Empty;
            if (DrawButton(rect, prefix + ActionLabel(action) + "  " + GameInput.BindingLabel(action)))
            {
                pendingBinding = action;
                settingsNotice = string.Empty;
            }
        }

        private bool DrawTitleMenuButton(Rect rect, int index, string label)
        {
            if (titleMenuIndex == index)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.36f);
                GUI.Box(new Rect(rect.x - 4f, rect.y - 3f, rect.width + 8f, rect.height + 6f), GUIContent.none);
                GUI.color = previousColor;
            }
            return DrawButton(rect, (titleMenuIndex == index ? ">  " : string.Empty) + label);
        }

        private bool DrawButton(Rect rect, string text)
        {
            // GUI.color tints both the button background and its label. Keep it white here
            // and tint only the background, otherwise dark button colors make text unreadable.
            var previousColor = GUI.color;
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.color = Color.white;
            var hovered = Event.current != null && rect.Contains(Event.current.mousePosition);
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

        private void HandleTitleMenuInput()
        {
            if (GameInput.MenuUpPressed) titleMenuIndex = (titleMenuIndex + 4) % 5;
            if (GameInput.MenuDownPressed) titleMenuIndex = (titleMenuIndex + 1) % 5;
            if (!GameInput.MenuConfirmPressed) return;

            switch (titleMenuIndex)
            {
                case 0: CycleDifficulty(); break;
                case 1: StartRun(); break;
                case 2: titlePage = TitlePage.HowTo; break;
                case 3: titlePage = TitlePage.Settings; break;
                case 4: titlePage = TitlePage.Profile; break;
            }
        }

        private bool CapturePendingBinding()
        {
            if (pendingBinding == GameAction.None || !GameInput.TryCaptureKeyboardKey(out var captured)) return false;
            if (GameInput.IsBoundElsewhere(pendingBinding, captured))
            {
                settingsNotice = GameInput.KeyLabel(captured) + " は別の操作に割り当て済みです。別のキーを選んでください。";
                return true;
            }

            GameInput.SetBinding(pendingBinding, captured);
            settingsNotice = ActionLabel(pendingBinding) + " を " + GameInput.BindingLabel(pendingBinding) + " に設定しました。";
            pendingBinding = GameAction.None;
            if (SfxService.Instance != null) SfxService.Instance.Play(SoundCue.MenuConfirm);
            return true;
        }

        private void CycleDifficulty()
        {
            SelectedDifficulty = GameRules.NextDifficulty(SelectedDifficulty);
            PlayerProfile.SaveDifficulty(SelectedDifficulty);
            // The whole encounter is generated while the title is visible, so reload before a run
            // to apply this tuning consistently to the player, every enemy, and the boss.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && stylesUseLargeText == LargeText && stylesUseHighContrast == HighContrast) return;
            stylesUseLargeText = LargeText;
            stylesUseHighContrast = HighContrast;
            uiFont = ResolveUiFont();
            var bodySize = LargeText ? 19 : 16;
            titleStyle = MakeStyle(LargeText ? 23 : 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            bodyStyle = MakeStyle(bodySize, FontStyle.Bold, TextAnchor.MiddleLeft);
            subtleStyle = MakeStyle(LargeText ? 16 : 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            centerStyle = MakeStyle(LargeText ? 24 : 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            bodyCenterStyle = MakeStyle(bodySize, FontStyle.Bold, TextAnchor.MiddleCenter);
            subtleCenterStyle = MakeStyle(LargeText ? 16 : 14, FontStyle.Normal, TextAnchor.MiddleCenter);
            titleCenterStyle = MakeStyle(LargeText ? 38 : 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            buttonStyle = MakeButtonStyle(LargeText ? 16 : 14);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
        }

        private GUIStyle subtleCenterStyle;

        private GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.font = uiFont;
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = alignment;
            style.wordWrap = true;
            style.normal.textColor = PrimaryTextColor;
            return style;
        }

        private GUIStyle MakeButtonStyle(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button);
            style.font = uiFont;
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.wordWrap = true;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private Font ResolveUiFont()
        {
            if (uiFont != null) return uiFont;
            var preferredFonts = new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic" };
            foreach (var fontName in preferredFonts)
            {
                var font = Font.CreateDynamicFontFromOSFont(fontName, 16);
                if (font != null) return font;
            }
            return GUI.skin.label.font;
        }

        private Color PrimaryTextColor => Color.white;
        private Color AccentColor => HighContrast ? new Color(1f, 0.96f, 0.08f) : new Color(1f, 0.89f, 0.37f);
        private Color PanelColor(float alpha) => HighContrast
            ? new Color(0.005f, 0.01f, 0.008f, Mathf.Max(alpha, 0.96f))
            : new Color(0.012f, 0.035f, 0.022f, Mathf.Max(alpha, 0.93f));

        private string ContextHint()
        {
            var elapsed = Time.unscaledTime - runStartedAt;
            if (elapsed < 8f) return "移動：WASD、矢印キー、またはコントローラーのスティック。";
            if (elapsed < 17f) return "マウスで方向を定め、攻撃とダッシュで危険を切り抜けよう。";
            if (Player != null && Player.Health <= 2) return "壺を壊してライフブルームを探そう。拾うとライフが回復します。";
            if (HasAllShards && !IsGuardianDefeated) return "門の守護者が現れた。倒して古代の門を開こう。";
            if (CanEnterGate) return "古代の門が開いた。金色のコンパスをたどって門へ向かおう。";
            return "金色のコンパスは、最も近い太陽の欠片を指しています。";
        }

        private string DashText()
        {
            return Player == null || Player.DashCooldownRemaining <= 0.01f
                ? "ダッシュ  " + GameInput.BindingLabel(GameAction.Dash) + " / X / B  使用可"
                : "ダッシュ  " + Player.DashCooldownRemaining.ToString("0.0") + "秒";
        }

        private static string HeartText(int health, int maximumHealth)
        {
            var result = string.Empty;
            for (var index = 0; index < maximumHealth; index++) result += index < health ? "● " : "○ ";
            return result;
        }

        private string ObjectiveText()
        {
            if (!HasAllShards) return "目的：太陽の欠片を探す";
            if (!IsGuardianDefeated) return "目的：門の守護者を倒す";
            return "目的：古代の門へ戻る";
        }

        private void UnlockAchievement(string id)
        {
            if (!PlayerProfile.UnlockAchievement(id)) return;
            var name = AchievementName(id);
            newAchievements.Add(name);
            ShowToast("実績を解除：" + name, 3f);
        }

        private static string AchievementName(string id)
        {
            switch (id)
            {
                case "first_steps": return "最初の一歩";
                case "shard_seeker": return "欠片の探求者";
                case "warden_slayer": return "守護者討伐";
                case "gatewalker": return "門をくぐる者";
                case "unbroken": return "無傷の勝利";
                case "swift_blade": return "疾風の刃";
                case "forest_hunter": return "森の狩人";
                case "potter": return "壺割り名人";
                default: return id;
            }
        }

        private static string ActionLabel(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveUp: return "上へ移動";
                case GameAction.MoveDown: return "下へ移動";
                case GameAction.MoveLeft: return "左へ移動";
                case GameAction.MoveRight: return "右へ移動";
                case GameAction.Attack: return "攻撃";
                case GameAction.Dash: return "ダッシュ";
                case GameAction.Pause: return "ポーズ";
                default: return "操作";
            }
        }

        private static int UnlockedAchievementCount()
        {
            var count = 0;
            foreach (var id in AchievementIds) if (PlayerProfile.IsAchievementUnlocked(id)) count++;
            return count;
        }

        private static string AchievementProgressText()
        {
            var locked = new List<string>();
            foreach (var id in AchievementIds)
            {
                if (!PlayerProfile.IsAchievementUnlocked(id)) locked.Add(AchievementName(id));
            }
            if (locked.Count == 0) return "森に伝わる実績をすべて達成しました。";
            var shown = locked.GetRange(0, Mathf.Min(3, locked.Count));
            return "次の目標：" + string.Join("、", shown.ToArray()) + (locked.Count > shown.Count ? "  ほか" + (locked.Count - shown.Count) + "件" : string.Empty);
        }

        private static string OnOff(bool value) => value ? "オン" : "オフ";
        private void TogglePause()
        {
            if (IsFinished || state == RunState.Title) return;
            state = state == RunState.Paused ? RunState.Playing : RunState.Paused;
            Time.timeScale = state == RunState.Playing ? 1f : 0f;
            pausedByFocusLoss = false;
            if (state == RunState.Playing) pauseSettingsOpen = false;
        }

        private void RestartRun()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReturnToTitle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SaveSettings()
        {
            PlayerProfile.SaveSettings(SfxVolume, ReduceFlashing, ScreenShakeEnabled, HighContrast, LargeText);
        }

        private void HandleSettingsShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SetSfxVolume(SfxVolume - 0.1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetSfxVolume(SfxVolume + 0.1f);
            if (Input.GetKeyDown(KeyCode.F)) ToggleReducedFlashing();
            if (Input.GetKeyDown(KeyCode.C)) ToggleScreenShake();
            if (Input.GetKeyDown(KeyCode.H)) ToggleHighContrast();
            if (Input.GetKeyDown(KeyCode.T)) ToggleLargeText();
        }

        private void PauseForInterruption()
        {
            if (!IsPlaying) return;
            state = RunState.Paused;
            pausedByFocusLoss = true;
            Time.timeScale = 0f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandleDevelopmentShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.F1)) showDiagnostics = !showDiagnostics;
            if (!IsPlaying) return;
            if (Input.GetKeyDown(KeyCode.F2)) AddShard(1);
            if (Input.GetKeyDown(KeyCode.F3) && Player != null) Player.Heal(Player.MaxHealth);
        }
#endif

        private static string FormatTime(float seconds)
        {
            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (wholeSeconds / 60).ToString("00") + ":" + (wholeSeconds % 60).ToString("00");
        }
    }
}
