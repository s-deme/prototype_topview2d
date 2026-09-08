using System;
using System.Collections.Generic;
using UnityEngine;

namespace VerdantBlade
{
    public enum DisplayMode
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    /// <summary>
    /// Persisted checkpoint for a suspended local run. World entities are tracked by stable IDs,
    /// so resuming never recreates an already collected shard or defeated foe.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int difficulty;
        public int worldVariant;
        public int shards;
        public int combatScore;
        public int enemiesDefeated;
        public int potsBroken;
        public int damageTaken;
        public int bestCombo;
        public float elapsedSeconds;
        public float playerX;
        public float playerY;
        public int playerHealth;
        public string destroyedEntityIds;

        public bool IsValid()
        {
            return version == CurrentVersion
                && difficulty >= (int)Difficulty.Explorer
                && difficulty <= (int)Difficulty.Veteran
                && worldVariant >= 0
                && worldVariant <= 2
                && shards >= 0
                && shards <= GameRules.ShardGoal
                && elapsedSeconds >= 0f
                && !float.IsNaN(elapsedSeconds)
                && !float.IsInfinity(elapsedSeconds)
                && !float.IsNaN(playerX)
                && !float.IsInfinity(playerX)
                && !float.IsNaN(playerY)
                && !float.IsInfinity(playerY)
                && playerHealth >= 0;
        }
    }

    /// <summary>
    /// Small, schema-versioned local profile. It is deliberately offline-only and tolerates
    /// malformed preference values by falling back to safe defaults.
    /// </summary>
    public static class PlayerProfile
    {
        private const string Prefix = "VerdantBlade.";
        private const int CurrentSchemaVersion = 3;
        private const string ProfileVersionKey = Prefix + "ProfileVersion";
        private const string BestScoreKey = Prefix + "BestScore";
        private const string BestTimeKey = Prefix + "BestTime";
        private const string ClearCountKey = Prefix + "ClearCount";
        private const string FurthestShardKey = Prefix + "FurthestShard";
        private const string AttemptCountKey = Prefix + "AttemptCount";
        private const string AbandonedCountKey = Prefix + "AbandonedCount";
        private const string TotalEnemiesKey = Prefix + "TotalEnemies";
        private const string TotalPotsKey = Prefix + "TotalPots";
        private const string TotalShardsKey = Prefix + "TotalShards";
        private const string TotalDamageKey = Prefix + "TotalDamage";
        private const string SfxVolumeKey = Prefix + "SfxVolume";
        private const string MusicVolumeKey = Prefix + "MusicVolume";
        private const string ReduceFlashingKey = Prefix + "ReduceFlashing";
        private const string ScreenShakeKey = Prefix + "ScreenShake";
        private const string HighContrastKey = Prefix + "HighContrast";
        private const string LargeTextKey = Prefix + "LargeText";
        private const string DifficultyKey = Prefix + "Difficulty";
        private const string DisplayModeKey = Prefix + "DisplayMode";
        private const string ResolutionKey = Prefix + "Resolution";
        private const string VSyncKey = Prefix + "VSync";
        private const string BindingPrefix = Prefix + "Binding.";
        private const string GamepadBindingPrefix = Prefix + "GamepadBinding.";
        private const string AchievementPrefix = Prefix + "Achievement.";
        private const string RunSnapshotKey = Prefix + "RunSnapshot";

        private static bool initialized;

        public static int BestScore => BestScoreFor(LoadDifficulty());
        public static int ClearCount => ClearCountFor(LoadDifficulty());
        public static int FurthestShardCount => NonNegative(FurthestShardKey);
        public static int AttemptCount => NonNegative(AttemptCountKey);
        public static int AbandonedCount => NonNegative(AbandonedCountKey);
        public static int TotalEnemiesDefeated => NonNegative(TotalEnemiesKey);
        public static int TotalPotsBroken => NonNegative(TotalPotsKey);
        public static int TotalShardsCollected => NonNegative(TotalShardsKey);
        public static int TotalDamageTaken => NonNegative(TotalDamageKey);
        public static float BestClearTime => BestClearTimeFor(LoadDifficulty());

        public static int BestScoreFor(Difficulty difficulty)
        {
            EnsureInitialized();
            return NonNegative(PerDifficultyKey(BestScoreKey, difficulty));
        }

        public static int ClearCountFor(Difficulty difficulty)
        {
            EnsureInitialized();
            return NonNegative(PerDifficultyKey(ClearCountKey, difficulty));
        }

        public static int AttemptCountFor(Difficulty difficulty)
        {
            EnsureInitialized();
            return NonNegative(PerDifficultyKey(AttemptCountKey, difficulty));
        }

        public static float BestClearTimeFor(Difficulty difficulty)
        {
            EnsureInitialized();
            return SafeTime(PlayerPrefs.GetFloat(PerDifficultyKey(BestTimeKey, difficulty), -1f));
        }

        public static bool SubmitClear(Difficulty difficulty, int score, float elapsedSeconds, int enemies, int pots, int shards, int damage)
        {
            EnsureInitialized();
            var scoreKey = PerDifficultyKey(BestScoreKey, difficulty);
            var isBestScore = score > NonNegative(scoreKey);
            if (isBestScore)
            {
                PlayerPrefs.SetInt(scoreKey, Mathf.Max(0, score));
            }

            var timeKey = PerDifficultyKey(BestTimeKey, difficulty);
            var safeElapsed = Mathf.Max(0f, elapsedSeconds);
            var previousBestTime = SafeTime(PlayerPrefs.GetFloat(timeKey, -1f));
            if (previousBestTime < 0f || safeElapsed < previousBestTime)
            {
                PlayerPrefs.SetFloat(timeKey, safeElapsed);
            }

            PlayerPrefs.SetInt(PerDifficultyKey(ClearCountKey, difficulty), ClearCountFor(difficulty) + 1);
            AddRunTotals(difficulty, enemies, pots, shards, damage, false);
            PlayerPrefs.SetInt(FurthestShardKey, Mathf.Max(FurthestShardCount, Mathf.Clamp(shards, 0, GameRules.ShardGoal)));
            PlayerPrefs.Save();
            return isBestScore;
        }

        public static void SubmitAttempt(Difficulty difficulty, int shardCount, int enemies, int pots, int damage, bool abandoned)
        {
            EnsureInitialized();
            AddRunTotals(difficulty, enemies, pots, shardCount, damage, abandoned);
            PlayerPrefs.SetInt(FurthestShardKey, Mathf.Max(FurthestShardCount, Mathf.Clamp(shardCount, 0, GameRules.ShardGoal)));
            PlayerPrefs.Save();
        }

        public static bool UnlockAchievement(string id)
        {
            EnsureInitialized();
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
            EnsureInitialized();
            return PlayerPrefs.GetInt(AchievementPrefix + id, 0) == 1;
        }

        public static void SaveBinding(GameAction action, KeyCode key)
        {
            EnsureInitialized();
            PlayerPrefs.SetInt(BindingPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static KeyCode LoadBinding(GameAction action, KeyCode fallback)
        {
            EnsureInitialized();
            return LoadKey(BindingPrefix + action, fallback, false);
        }

        public static void SaveGamepadBinding(GameAction action, KeyCode key)
        {
            EnsureInitialized();
            PlayerPrefs.SetInt(GamepadBindingPrefix + action, (int)key);
            PlayerPrefs.Save();
        }

        public static KeyCode LoadGamepadBinding(GameAction action, KeyCode fallback)
        {
            EnsureInitialized();
            return LoadKey(GamepadBindingPrefix + action, fallback, true);
        }

        public static void ResetBindings()
        {
            ResetBindings(BindingPrefix, GameInput.Actions);
        }

        public static void ResetGamepadBindings()
        {
            ResetBindings(GamepadBindingPrefix, GameInput.GamepadActions);
        }

        private static void ResetBindings(string prefix, IEnumerable<GameAction> actions)
        {
            EnsureInitialized();
            foreach (var action in actions)
            {
                PlayerPrefs.DeleteKey(prefix + action);
            }
            PlayerPrefs.Save();
        }

        public static void SaveSettings(float sfxVolume, float musicVolume, bool reduceFlashing, bool screenShakeEnabled, bool highContrast, bool largeText)
        {
            EnsureInitialized();
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(sfxVolume));
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(musicVolume));
            PlayerPrefs.SetInt(ReduceFlashingKey, reduceFlashing ? 1 : 0);
            PlayerPrefs.SetInt(ScreenShakeKey, screenShakeEnabled ? 1 : 0);
            PlayerPrefs.SetInt(HighContrastKey, highContrast ? 1 : 0);
            PlayerPrefs.SetInt(LargeTextKey, largeText ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static float LoadSfxVolume() { EnsureInitialized(); return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 0.62f)); }
        public static float LoadMusicVolume() { EnsureInitialized(); return Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.38f)); }
        public static bool LoadReduceFlashing() { EnsureInitialized(); return PlayerPrefs.GetInt(ReduceFlashingKey, 0) == 1; }
        public static bool LoadScreenShake() { EnsureInitialized(); return PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1; }
        public static bool LoadHighContrast() { EnsureInitialized(); return PlayerPrefs.GetInt(HighContrastKey, 0) == 1; }
        public static bool LoadLargeText() { EnsureInitialized(); return PlayerPrefs.GetInt(LargeTextKey, 0) == 1; }
        public static Difficulty LoadDifficulty() { EnsureInitialized(); return (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Adventurer), (int)Difficulty.Explorer, (int)Difficulty.Veteran); }
        public static DisplayMode LoadDisplayMode() { EnsureInitialized(); return (DisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey, (int)DisplayMode.Borderless), (int)DisplayMode.Fullscreen, (int)DisplayMode.Windowed); }
        public static int LoadResolutionIndex() { EnsureInitialized(); return Mathf.Clamp(PlayerPrefs.GetInt(ResolutionKey, 0), 0, 2); }
        public static bool LoadVSync() { EnsureInitialized(); return PlayerPrefs.GetInt(VSyncKey, 1) == 1; }

        public static void SaveDifficulty(Difficulty difficulty)
        {
            EnsureInitialized();
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
        }

        public static void SaveDisplaySettings(DisplayMode displayMode, int resolutionIndex, bool vSync)
        {
            EnsureInitialized();
            PlayerPrefs.SetInt(DisplayModeKey, (int)displayMode);
            PlayerPrefs.SetInt(ResolutionKey, Mathf.Clamp(resolutionIndex, 0, 2));
            PlayerPrefs.SetInt(VSyncKey, vSync ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetSettings()
        {
            EnsureInitialized();
            PlayerPrefs.DeleteKey(SfxVolumeKey);
            PlayerPrefs.DeleteKey(MusicVolumeKey);
            PlayerPrefs.DeleteKey(ReduceFlashingKey);
            PlayerPrefs.DeleteKey(ScreenShakeKey);
            PlayerPrefs.DeleteKey(HighContrastKey);
            PlayerPrefs.DeleteKey(LargeTextKey);
            PlayerPrefs.DeleteKey(DisplayModeKey);
            PlayerPrefs.DeleteKey(ResolutionKey);
            PlayerPrefs.DeleteKey(VSyncKey);
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            EnsureInitialized();
            PlayerPrefs.DeleteKey(BestScoreKey);
            PlayerPrefs.DeleteKey(BestTimeKey);
            PlayerPrefs.DeleteKey(ClearCountKey);
            PlayerPrefs.DeleteKey(FurthestShardKey);
            PlayerPrefs.DeleteKey(AttemptCountKey);
            PlayerPrefs.DeleteKey(AbandonedCountKey);
            PlayerPrefs.DeleteKey(TotalEnemiesKey);
            PlayerPrefs.DeleteKey(TotalPotsKey);
            PlayerPrefs.DeleteKey(TotalShardsKey);
            PlayerPrefs.DeleteKey(TotalDamageKey);
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                PlayerPrefs.DeleteKey(PerDifficultyKey(BestScoreKey, difficulty));
                PlayerPrefs.DeleteKey(PerDifficultyKey(BestTimeKey, difficulty));
                PlayerPrefs.DeleteKey(PerDifficultyKey(ClearCountKey, difficulty));
                PlayerPrefs.DeleteKey(PerDifficultyKey(AttemptCountKey, difficulty));
            }
            foreach (var id in GameManager.AchievementIds)
            {
                PlayerPrefs.DeleteKey(AchievementPrefix + id);
            }
            ClearRunSnapshot();
        }

        public static void SaveRunSnapshot(RunSnapshot snapshot)
        {
            EnsureInitialized();
            if (snapshot == null || !snapshot.IsValid())
            {
                return;
            }
            PlayerPrefs.SetString(RunSnapshotKey, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        public static RunSnapshot LoadRunSnapshot()
        {
            EnsureInitialized();
            var json = PlayerPrefs.GetString(RunSnapshotKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                var snapshot = JsonUtility.FromJson<RunSnapshot>(json);
                if (snapshot != null && snapshot.IsValid())
                {
                    return snapshot;
                }
            }
            catch (ArgumentException)
            {
                // Invalid data is safely discarded below.
            }
            ClearRunSnapshot();
            return null;
        }

        public static void ClearRunSnapshot()
        {
            PlayerPrefs.DeleteKey(RunSnapshotKey);
            PlayerPrefs.Save();
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            var version = PlayerPrefs.GetInt(ProfileVersionKey, 0);
            if (version < 2)
            {
                // Older builds kept one shared record. Preserve it as the original standard-mode record.
                CopyLegacyRecordTo(Difficulty.Adventurer);
                version = 2;
            }
            if (version < CurrentSchemaVersion)
            {
                version = CurrentSchemaVersion;
            }
            PlayerPrefs.SetInt(ProfileVersionKey, version);
            PlayerPrefs.Save();
        }

        private static void CopyLegacyRecordTo(Difficulty difficulty)
        {
            PlayerPrefs.SetInt(PerDifficultyKey(BestScoreKey, difficulty), NonNegative(BestScoreKey));
            PlayerPrefs.SetFloat(PerDifficultyKey(BestTimeKey, difficulty), SafeTime(PlayerPrefs.GetFloat(BestTimeKey, -1f)));
            PlayerPrefs.SetInt(PerDifficultyKey(ClearCountKey, difficulty), NonNegative(ClearCountKey));
            PlayerPrefs.SetInt(PerDifficultyKey(AttemptCountKey, difficulty), NonNegative(AttemptCountKey));
        }

        private static void AddRunTotals(Difficulty difficulty, int enemies, int pots, int shards, int damage, bool abandoned)
        {
            PlayerPrefs.SetInt(AttemptCountKey, AttemptCount + 1);
            PlayerPrefs.SetInt(PerDifficultyKey(AttemptCountKey, difficulty), AttemptCountFor(difficulty) + 1);
            if (abandoned)
            {
                PlayerPrefs.SetInt(AbandonedCountKey, AbandonedCount + 1);
            }
            PlayerPrefs.SetInt(TotalEnemiesKey, TotalEnemiesDefeated + Mathf.Max(0, enemies));
            PlayerPrefs.SetInt(TotalPotsKey, TotalPotsBroken + Mathf.Max(0, pots));
            PlayerPrefs.SetInt(TotalShardsKey, TotalShardsCollected + Mathf.Max(0, shards));
            PlayerPrefs.SetInt(TotalDamageKey, TotalDamageTaken + Mathf.Max(0, damage));
        }

        private static int NonNegative(string key)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
        }

        private static float SafeTime(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : -1f;
        }

        private static string PerDifficultyKey(string baseKey, Difficulty difficulty)
        {
            return baseKey + "." + difficulty;
        }

        private static KeyCode LoadKey(string key, KeyCode fallback, bool gamepadOnly)
        {
            var stored = PlayerPrefs.GetInt(key, -1);
            if (stored < 0 || !Enum.IsDefined(typeof(KeyCode), stored))
            {
                return fallback;
            }
            var result = (KeyCode)stored;
            return !gamepadOnly || (result >= KeyCode.JoystickButton0 && result <= KeyCode.JoystickButton19) ? result : fallback;
        }
    }
}
