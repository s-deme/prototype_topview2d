using UnityEngine;

namespace VerdantBlade
{
    /// <summary>Shared UI styles, labels, visual tokens, and achievement presentation.</summary>
    public sealed partial class GameManager
    {
        private GUIStyle subtleCenterStyle;

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
        }

        private GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                wordWrap = true
            };
            style.normal.textColor = PrimaryTextColor;
            return style;
        }

        private GUIStyle MakeButtonStyle(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private Font ResolveUiFont()
        {
            if (uiFont != null) return uiFont;
            var embeddedFont = Resources.Load<Font>("Fonts/NotoSansJP-VF");
            if (embeddedFont != null) return embeddedFont;
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
            var elapsed = ElapsedRunSeconds;
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
                ? "ダッシュ  " + GameInput.BindingLabel(GameAction.Dash) + " / X / " + GameInput.GamepadHintLabel(GameAction.Dash) + "  使用可"
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

        private static string AchievementDescription(string id)
        {
            switch (id)
            {
                case "first_steps": return "最初の冒険を開始する";
                case "shard_seeker": return "太陽の欠片を8個集める";
                case "warden_slayer": return "門の守護者を倒す";
                case "gatewalker": return "古代の門をくぐり、クリアする";
                case "unbroken": return "被ダメージ0でクリアする";
                case "swift_blade": return "2分30秒以内にクリアする";
                case "forest_hunter": return "敵を8体倒す";
                case "potter": return "壺割り名人";
                default: return "冒険を進めて解除する";
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

        private static string OnOff(bool value) => value ? "オン" : "オフ";
        private static string DisplayModeLabel(DisplayMode displayMode)
        {
            switch (displayMode)
            {
                case DisplayMode.Fullscreen: return "フルスクリーン";
                case DisplayMode.Windowed: return "ウィンドウ";
                default: return "ボーダーレス";
            }
        }

        private string ResolutionLabel()
        {
            return ResolutionLabels[Mathf.Clamp(ResolutionIndex, 0, ResolutionLabels.Length - 1)];
        }
    }
}
