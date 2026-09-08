using UnityEngine;
using UnityEngine.SceneManagement;

namespace VerdantBlade
{
    /// <summary>Run transitions, local persistence, and platform display settings.</summary>
    public sealed partial class GameManager
    {
        private static readonly Vector2Int[] AvailableResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080)
        };

        private static readonly string[] ResolutionLabels =
        {
            "1280 × 720",
            "1600 × 900",
            "1920 × 1080"
        };

        private void CycleDifficulty()
        {
            if (resumeSnapshot != null)
            {
                ShowToast("続きから再開する冒険の難易度は変更できません。新しい冒険で選択できます。", 3.2f);
                return;
            }
            SelectedDifficulty = GameRules.NextDifficulty(SelectedDifficulty);
            PlayerProfile.SaveDifficulty(SelectedDifficulty);
            // The whole encounter is generated while the title is visible, so reload before a run
            // to apply this tuning consistently to the player, every enemy, and the boss.
            ReloadCurrentScene();
        }

        private void TogglePause()
        {
            if (IsFinished || state == RunState.Title) return;
            state = state == RunState.Paused ? RunState.Playing : RunState.Paused;
            Time.timeScale = state == RunState.Playing ? 1f : 0f;
            if (SfxService.Instance != null) SfxService.Instance.SetMusicPaused(state == RunState.Paused);
            pausedByFocusLoss = false;
            if (state == RunState.Playing)
            {
                pauseSettingsOpen = false;
            }
            else
            {
                SaveRunSnapshot();
            }
        }

        private void RestartRun()
        {
            SubmitCurrentRunAsAbandoned();
            PlayerProfile.ClearRunSnapshot();
            resumeSnapshot = null;
            destroyedEntityIds.Clear();
            startRunOnSceneLoad = true;
            ReloadCurrentScene();
        }

        private void ReturnToTitle()
        {
            SaveRunSnapshot();
            ReloadCurrentScene();
        }

        private static void ReloadCurrentScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SaveSettings()
        {
            PlayerProfile.SaveSettings(SfxVolume, MusicVolume, ReduceFlashing, ScreenShakeEnabled, HighContrast, LargeText);
        }

        private void LoadAudioAndAccessibilitySettings()
        {
            SfxVolume = PlayerProfile.LoadSfxVolume();
            MusicVolume = PlayerProfile.LoadMusicVolume();
            ReduceFlashing = PlayerProfile.LoadReduceFlashing();
            ScreenShakeEnabled = PlayerProfile.LoadScreenShake();
            HighContrast = PlayerProfile.LoadHighContrast();
            LargeText = PlayerProfile.LoadLargeText();
        }

        private void LoadDisplayPreferences()
        {
            SelectedDisplayMode = PlayerProfile.LoadDisplayMode();
            ResolutionIndex = PlayerProfile.LoadResolutionIndex();
            VSyncEnabled = PlayerProfile.LoadVSync();
        }

        private void HandleSettingsShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SetSfxVolume(SfxVolume - 0.1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetSfxVolume(SfxVolume + 0.1f);
            if (Input.GetKeyDown(KeyCode.Minus)) SetMusicVolume(MusicVolume - 0.1f);
            if (Input.GetKeyDown(KeyCode.Equals)) SetMusicVolume(MusicVolume + 0.1f);
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
            if (SfxService.Instance != null) SfxService.Instance.SetMusicPaused(true);
            SaveRunSnapshot();
        }

        private void ApplyDisplaySettings()
        {
            var resolution = AvailableResolutions[Mathf.Clamp(ResolutionIndex, 0, AvailableResolutions.Length - 1)];
            var mode = SelectedDisplayMode == DisplayMode.Fullscreen
                ? FullScreenMode.ExclusiveFullScreen
                : SelectedDisplayMode == DisplayMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = VSyncEnabled ? -1 : 120;
            Screen.SetResolution(resolution.x, resolution.y, mode);
        }

        private void CycleDisplayMode()
        {
            SelectedDisplayMode = SelectedDisplayMode == DisplayMode.Windowed ? DisplayMode.Fullscreen : (DisplayMode)((int)SelectedDisplayMode + 1);
            SaveAndApplyDisplaySettings();
        }

        private void CycleResolution()
        {
            ResolutionIndex = (ResolutionIndex + 1) % AvailableResolutions.Length;
            SaveAndApplyDisplaySettings();
        }

        private void ToggleVSync()
        {
            VSyncEnabled = !VSyncEnabled;
            SaveAndApplyDisplaySettings();
        }

        private void SaveAndApplyDisplaySettings()
        {
            PlayerProfile.SaveDisplaySettings(SelectedDisplayMode, ResolutionIndex, VSyncEnabled);
            ApplyDisplaySettings();
        }

        private void ResetAllSettings()
        {
            PlayerProfile.ResetSettings();
            GameInput.ResetBindings();
            GameInput.ResetGamepadBindings();
            LoadAudioAndAccessibilitySettings();
            LoadDisplayPreferences();
            titleStyle = null;
            if (SfxService.Instance != null)
            {
                SfxService.Instance.SetVolume(SfxVolume);
                SfxService.Instance.SetMusicVolume(MusicVolume);
            }
            ApplyDisplaySettings();
            settingsNotice = "設定と操作を初期状態に戻しました。";
        }

        private void ResetProgressAndReload()
        {
            PlayerProfile.ResetProgress();
            resumeSnapshot = null;
            destroyedEntityIds.Clear();
            resetConfirmation = false;
            ReloadCurrentScene();
        }

        private void QuitGame()
        {
            SaveRunSnapshot();
            Application.Quit();
        }

        private void SaveRunSnapshot()
        {
            if ((state != RunState.Playing && state != RunState.Paused) || runSubmitted || Player == null)
            {
                return;
            }
            resumeSnapshot = new RunSnapshot
            {
                difficulty = (int)SelectedDifficulty,
                worldVariant = WorldVariant,
                shards = shards,
                combatScore = combatScore,
                enemiesDefeated = EnemiesDefeated,
                potsBroken = PotsBroken,
                damageTaken = DamageTaken,
                bestCombo = bestCombo,
                elapsedSeconds = ElapsedRunSeconds,
                playerX = Player.transform.position.x,
                playerY = Player.transform.position.y,
                playerHealth = Player.Health,
                destroyedEntityIds = string.Join("|", destroyedEntityIds)
            };
            PlayerProfile.SaveRunSnapshot(resumeSnapshot);
        }

        private void RestoreDestroyedEntityIds(string packedIds)
        {
            if (!string.IsNullOrEmpty(packedIds))
            {
                foreach (var id in packedIds.Split('|'))
                {
                    if (!string.IsNullOrEmpty(id)) destroyedEntityIds.Add(id);
                }
            }
            IsGuardianDefeated = destroyedEntityIds.Contains("warden");
        }

        private void SubmitCurrentRunAsAbandoned()
        {
            if (runSubmitted || (state != RunState.Playing && state != RunState.Paused)) return;
            PlayerProfile.SubmitAttempt(SelectedDifficulty, shards, EnemiesDefeated, PotsBroken, DamageTaken, true);
            runSubmitted = true;
        }

        private void SubmitSnapshotAsAbandoned()
        {
            if (resumeSnapshot == null) return;
            PlayerProfile.SubmitAttempt((Difficulty)resumeSnapshot.difficulty, resumeSnapshot.shards, resumeSnapshot.enemiesDefeated, resumeSnapshot.potsBroken, resumeSnapshot.damageTaken, true);
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
