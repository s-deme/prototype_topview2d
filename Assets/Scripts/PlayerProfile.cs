using UnityEngine;

namespace VerdantBlade
{
    /// <summary>
    /// Small, versioned local profile. Gameplay is never blocked by a missing profile or network.
    /// </summary>
    public static class PlayerProfile
    {
        private const string Prefix = "VerdantBlade.";
        private const string BestScoreKey = Prefix + "BestScore";
        private const string BestTimeKey = Prefix + "BestTime";
        private const string ClearCountKey = Prefix + "ClearCount";
        private const string FurthestShardKey = Prefix + "FurthestShard";
        private const string AttemptCountKey = Prefix + "AttemptCount";
        private const string TotalEnemiesKey = Prefix + "TotalEnemies";
        private const string TotalPotsKey = Prefix + "TotalPots";
        private const string TotalShardsKey = Prefix + "TotalShards";
        private const string TotalDamageKey = Prefix + "TotalDamage";
        private const string SfxVolumeKey = Prefix + "SfxVolume";
        private const string ReduceFlashingKey = Prefix + "ReduceFlashing";
        private const string ScreenShakeKey = Prefix + "ScreenShake";
        private const string HighContrastKey = Prefix + "HighContrast";
        private const string LargeTextKey = Prefix + "LargeText";
        private const string DifficultyKey = Prefix + "Difficulty";
        private const string BindingPrefix = Prefix + "Binding.";
        private const string AchievementPrefix = Prefix + "Achievement.";

        public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
        public static int ClearCount => PlayerPrefs.GetInt(ClearCountKey, 0);
        public static int FurthestShardCount => PlayerPrefs.GetInt(FurthestShardKey, 0);
        public static int AttemptCount => PlayerPrefs.GetInt(AttemptCountKey, 0);
        public static int TotalEnemiesDefeated => PlayerPrefs.GetInt(TotalEnemiesKey, 0);
        public static int TotalPotsBroken => PlayerPrefs.GetInt(TotalPotsKey, 0);
        public static int TotalShardsCollected => PlayerPrefs.GetInt(TotalShardsKey, 0);
        public static int TotalDamageTaken => PlayerPrefs.GetInt(TotalDamageKey, 0);
        public static float BestClearTime => PlayerPrefs.GetFloat(BestTimeKey, -1f);

        public static bool SubmitClear(int score, float elapsedSeconds, int enemies, int pots, int shards, int damage)
        {
            var isBestScore = score > BestScore;
            if (isBestScore)
            {
                PlayerPrefs.SetInt(BestScoreKey, score);
            }
            if (BestClearTime < 0f || elapsedSeconds < BestClearTime)
            {
                PlayerPrefs.SetFloat(BestTimeKey, elapsedSeconds);
            }

            AddRunTotals(enemies, pots, shards, damage);
            PlayerPrefs.SetInt(ClearCountKey, ClearCount + 1);
            PlayerPrefs.SetInt(FurthestShardKey, Mathf.Max(FurthestShardCount, shards));
            PlayerPrefs.Save();
            return isBestScore;
        }

        public static void SubmitAttempt(int shardCount, int enemies, int pots, int damage)
        {
            AddRunTotals(enemies, pots, shardCount, damage);
            PlayerPrefs.SetInt(FurthestShardKey, Mathf.Max(FurthestShardCount, shardCount));
            PlayerPrefs.Save();
        }

        public static bool UnlockAchievement(string id)
        {
            var key = AchievementPrefix + id;
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                return false;
            }
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool IsAchievementUnlocked(string id)
        {
            return PlayerPrefs.GetInt(AchievementPrefix + id, 0) == 1;
        }

        public static void SaveBinding(GameAction action, KeyCode key)
        {
            PlayerPrefs.SetInt(BindingPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static KeyCode LoadBinding(GameAction action, KeyCode fallback)
        {
            var stored = PlayerPrefs.GetInt(BindingPrefix + action, -1);
            return stored >= 0 && System.Enum.IsDefined(typeof(KeyCode), stored) ? (KeyCode)stored : fallback;
        }

        public static void ResetBindings()
        {
            foreach (var action in GameInput.Actions)
            {
                PlayerPrefs.DeleteKey(BindingPrefix + action);
            }
            PlayerPrefs.Save();
        }

        public static void SaveSettings(float sfxVolume, bool reduceFlashing, bool screenShakeEnabled, bool highContrast, bool largeText)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(sfxVolume));
            PlayerPrefs.SetInt(ReduceFlashingKey, reduceFlashing ? 1 : 0);
            PlayerPrefs.SetInt(ScreenShakeKey, screenShakeEnabled ? 1 : 0);
            PlayerPrefs.SetInt(HighContrastKey, highContrast ? 1 : 0);
            PlayerPrefs.SetInt(LargeTextKey, largeText ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static float LoadSfxVolume() => PlayerPrefs.GetFloat(SfxVolumeKey, 0.62f);
        public static bool LoadReduceFlashing() => PlayerPrefs.GetInt(ReduceFlashingKey, 0) == 1;
        public static bool LoadScreenShake() => PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;
        public static bool LoadHighContrast() => PlayerPrefs.GetInt(HighContrastKey, 0) == 1;
        public static bool LoadLargeText() => PlayerPrefs.GetInt(LargeTextKey, 0) == 1;
        public static Difficulty LoadDifficulty() => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Adventurer), (int)Difficulty.Explorer, (int)Difficulty.Veteran);

        public static void SaveDifficulty(Difficulty difficulty)
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(BestScoreKey);
            PlayerPrefs.DeleteKey(BestTimeKey);
            PlayerPrefs.DeleteKey(ClearCountKey);
            PlayerPrefs.DeleteKey(FurthestShardKey);
            PlayerPrefs.DeleteKey(AttemptCountKey);
            PlayerPrefs.DeleteKey(TotalEnemiesKey);
            PlayerPrefs.DeleteKey(TotalPotsKey);
            PlayerPrefs.DeleteKey(TotalShardsKey);
            PlayerPrefs.DeleteKey(TotalDamageKey);
            foreach (var id in GameManager.AchievementIds)
            {
                PlayerPrefs.DeleteKey(AchievementPrefix + id);
            }
            PlayerPrefs.Save();
        }

        private static void AddRunTotals(int enemies, int pots, int shards, int damage)
        {
            PlayerPrefs.SetInt(AttemptCountKey, AttemptCount + 1);
            PlayerPrefs.SetInt(TotalEnemiesKey, TotalEnemiesDefeated + Mathf.Max(0, enemies));
            PlayerPrefs.SetInt(TotalPotsKey, TotalPotsBroken + Mathf.Max(0, pots));
            PlayerPrefs.SetInt(TotalShardsKey, TotalShardsCollected + Mathf.Max(0, shards));
            PlayerPrefs.SetInt(TotalDamageKey, TotalDamageTaken + Mathf.Max(0, damage));
        }
    }
}
