using UnityEngine;

namespace VerdantBlade
{
    /// <summary>Keyboard and gamepad routing for title, pause, result, and settings screens.</summary>
    public sealed partial class GameManager
    {
        private void HandleTitleInput()
        {
            if (titlePage == TitlePage.Main)
            {
                HandleTitleMenuInput();
                return;
            }
            if (titlePage == TitlePage.HowTo)
            {
                if (GameInput.MenuBackPressed || GameInput.MenuConfirmPressed) ReturnToTitleMain();
                return;
            }
            if (titlePage == TitlePage.Profile)
            {
                HandleProfileInput();
                return;
            }
            HandleSettingsInput(true);
        }

        private void HandleTitleMenuInput()
        {
            var itemCount = resumeSnapshot == null ? 6 : 7;
            if (!MoveMenuSelection(ref titleMenuIndex, itemCount)) return;

            if (resumeSnapshot == null)
            {
                switch (titleMenuIndex)
                {
                    case 0: CycleDifficulty(); break;
                    case 1: StartFreshRun(); break;
                    case 2: titlePage = TitlePage.HowTo; break;
                    case 3: titlePage = TitlePage.Settings; settingsMenuIndex = 0; break;
                    case 4: titlePage = TitlePage.Profile; profileMenuIndex = 0; profileDifficulty = SelectedDifficulty; break;
                    case 5: QuitGame(); break;
                }
                return;
            }

            switch (titleMenuIndex)
            {
                case 0: CycleDifficulty(); break;
                case 1: ResumeRun(); break;
                case 2: StartFreshRun(); break;
                case 3: titlePage = TitlePage.HowTo; break;
                case 4: titlePage = TitlePage.Settings; settingsMenuIndex = 0; break;
                case 5: titlePage = TitlePage.Profile; profileMenuIndex = 0; profileDifficulty = SelectedDifficulty; break;
                case 6: QuitGame(); break;
            }
        }

        private void HandleSettingsInput(bool fromTitle)
        {
            if (bindingsOpen)
            {
                HandleKeyboardBindingsInput();
                return;
            }
            if (gamepadBindingsOpen)
            {
                HandleGamepadBindingsInput();
                return;
            }
            HandleSettingsShortcuts();
            if (GameInput.MenuBackPressed)
            {
                CloseSettings(fromTitle);
                return;
            }
            if (!MoveMenuSelection(ref settingsMenuIndex, 15)) return;
            switch (settingsMenuIndex)
            {
                case 0: SetSfxVolume(SfxVolume - 0.1f); break;
                case 1: SetSfxVolume(SfxVolume + 0.1f); break;
                case 2: SetMusicVolume(MusicVolume - 0.1f); break;
                case 3: SetMusicVolume(MusicVolume + 0.1f); break;
                case 4: CycleDisplayMode(); break;
                case 5: CycleResolution(); break;
                case 6: ToggleVSync(); break;
                case 7: ToggleReducedFlashing(); break;
                case 8: ToggleScreenShake(); break;
                case 9: ToggleHighContrast(); break;
                case 10: ToggleLargeText(); break;
                case 11: bindingsOpen = true; keyboardBindingsMenuIndex = 0; break;
                case 12: gamepadBindingsOpen = true; gamepadBindingsMenuIndex = 0; break;
                case 13: ToggleSettingsResetConfirmation(); break;
                case 14: CloseSettings(fromTitle); break;
            }
        }

        private void ToggleSettingsResetConfirmation()
        {
            if (resetSettingsConfirmation)
            {
                resetSettingsConfirmation = false;
                ResetAllSettings();
                return;
            }

            resetSettingsConfirmation = true;
            settingsNotice = "もう一度決定すると設定と操作を初期状態に戻します。";
        }

        private void HandleKeyboardBindingsInput()
        {
            if (GameInput.MenuBackPressed)
            {
                if (pendingBinding != GameAction.None)
                {
                    pendingBinding = GameAction.None;
                    settingsNotice = "キーの割り当てを中止しました。";
                }
                else
                {
                    bindingsOpen = false;
                }
                return;
            }
            if (!MoveMenuSelection(ref keyboardBindingsMenuIndex, 9)) return;
            if (keyboardBindingsMenuIndex <= 6)
            {
                pendingBinding = (GameAction)(keyboardBindingsMenuIndex + 1);
                settingsNotice = string.Empty;
            }
            else if (keyboardBindingsMenuIndex == 7)
            {
                GameInput.ResetBindings();
                pendingBinding = GameAction.None;
                settingsNotice = "キー設定を初期状態に戻しました。";
            }
            else
            {
                bindingsOpen = false;
            }
        }

        private void HandleGamepadBindingsInput()
        {
            if (GameInput.MenuBackPressed)
            {
                if (pendingGamepadBinding != GameAction.None)
                {
                    pendingGamepadBinding = GameAction.None;
                    settingsNotice = "ゲームパッドの割り当てを中止しました。";
                }
                else
                {
                    gamepadBindingsOpen = false;
                }
                return;
            }
            if (!MoveMenuSelection(ref gamepadBindingsMenuIndex, 5)) return;
            if (gamepadBindingsMenuIndex <= 2)
            {
                pendingGamepadBinding = gamepadBindingsMenuIndex == 0 ? GameAction.Attack : gamepadBindingsMenuIndex == 1 ? GameAction.Dash : GameAction.Pause;
                settingsNotice = string.Empty;
            }
            else if (gamepadBindingsMenuIndex == 3)
            {
                GameInput.ResetGamepadBindings();
                pendingGamepadBinding = GameAction.None;
                settingsNotice = "ゲームパッド設定を初期状態に戻しました。";
            }
            else
            {
                gamepadBindingsOpen = false;
            }
        }

        private void HandleProfileInput()
        {
            if (profileAchievementsOpen)
            {
                if (GameInput.MenuBackPressed || GameInput.MenuConfirmPressed) profileAchievementsOpen = false;
                return;
            }
            if (GameInput.MenuBackPressed)
            {
                ReturnToTitleMain();
                return;
            }
            if (!MoveMenuSelection(ref profileMenuIndex, 4)) return;
            switch (profileMenuIndex)
            {
                case 0: profileDifficulty = GameRules.NextDifficulty(profileDifficulty); break;
                case 1: profileAchievementsOpen = true; break;
                case 2:
                    if (resetConfirmation)
                    {
                        ResetProgressAndReload();
                    }
                    else
                    {
                        resetConfirmation = true;
                        profileNotice = "もう一度決定すると記録を消去します。";
                    }
                    break;
                case 3: ReturnToTitleMain(); break;
            }
        }

        private void HandlePauseInput()
        {
            if (pauseSettingsOpen)
            {
                HandleSettingsInput(false);
                return;
            }
            if (GameInput.MenuBackPressed)
            {
                TogglePause();
                return;
            }
            if (!MoveMenuSelection(ref pauseMenuIndex, 5)) return;
            switch (pauseMenuIndex)
            {
                case 0: TogglePause(); break;
                case 1: pauseSettingsOpen = true; settingsMenuIndex = 0; break;
                case 2: RestartRun(); break;
                case 3: ReturnToTitle(); break;
                case 4: QuitGame(); break;
            }
        }

        private void HandleResultInput()
        {
            if (GameInput.MenuBackPressed)
            {
                ReturnToTitle();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartRun();
                return;
            }
            if (!MoveMenuSelection(ref resultMenuIndex, 3)) return;
            switch (resultMenuIndex)
            {
                case 0: RestartRun(); break;
                case 1: ReturnToTitle(); break;
                case 2: QuitGame(); break;
            }
        }

        private static bool MoveMenuSelection(ref int selectedIndex, int itemCount)
        {
            if (GameInput.MenuUpPressed) selectedIndex = (selectedIndex + itemCount - 1) % itemCount;
            if (GameInput.MenuDownPressed) selectedIndex = (selectedIndex + 1) % itemCount;
            return GameInput.MenuConfirmPressed;
        }

        private void CloseSettings(bool fromTitle)
        {
            bindingsOpen = false;
            gamepadBindingsOpen = false;
            pendingBinding = GameAction.None;
            pendingGamepadBinding = GameAction.None;
            resetSettingsConfirmation = false;
            if (fromTitle) ReturnToTitleMain();
            else pauseSettingsOpen = false;
        }

        private void ReturnToTitleMain()
        {
            titlePage = TitlePage.Main;
            bindingsOpen = false;
            gamepadBindingsOpen = false;
            profileAchievementsOpen = false;
            resetConfirmation = false;
            newRunConfirmation = false;
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

        private bool CapturePendingGamepadBinding()
        {
            if (pendingGamepadBinding == GameAction.None || !GameInput.TryCaptureGamepadButton(out var captured)) return false;
            if (GameInput.IsGamepadBoundElsewhere(pendingGamepadBinding, captured))
            {
                settingsNotice = GameInput.KeyLabel(captured) + " は別のゲームパッド操作に割り当て済みです。";
                return true;
            }

            GameInput.SetGamepadBinding(pendingGamepadBinding, captured);
            settingsNotice = ActionLabel(pendingGamepadBinding) + " を " + GameInput.GamepadBindingLabel(pendingGamepadBinding) + " に設定しました。";
            pendingGamepadBinding = GameAction.None;
            if (SfxService.Instance != null) SfxService.Instance.Play(SoundCue.MenuConfirm);
            return true;
        }
    }
}
