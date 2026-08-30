using UnityEngine;

namespace VerdantBlade
{
    public enum Difficulty
    {
        Explorer,
        Adventurer,
        Veteran
    }

    /// <summary>Pure, testable rules shared by runtime presentation and automated tests.</summary>
    public static class GameRules
    {
        public const int ShardGoal = 8;
        public const float SwiftClearSeconds = 150f;

        public static int ClampShards(int current, int amount)
        {
            return Mathf.Clamp(current + amount, 0, ShardGoal);
        }

        public static int CalculateScore(int shards, int combatScore, int potsBroken, int remainingHealth, int combo, bool won, float elapsedSeconds)
        {
            var comboBonus = combo < 2 ? 0 : combo * 10;
            var victoryTimeBonus = won ? Mathf.Max(0, 450 - Mathf.FloorToInt(elapsedSeconds * 5f)) : 0;
            return Mathf.Clamp(shards, 0, ShardGoal) * 100 + Mathf.Max(0, combatScore) + Mathf.Max(0, potsBroken) * 15 + Mathf.Max(0, remainingHealth) * 25 + comboBonus + victoryTimeBonus;
        }

        public static bool IsSwiftClear(float elapsedSeconds)
        {
            return elapsedSeconds <= SwiftClearSeconds;
        }

        public static Difficulty NextDifficulty(Difficulty current)
        {
            return current == Difficulty.Veteran ? Difficulty.Explorer : (Difficulty)((int)current + 1);
        }

        public static string DifficultyName(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Explorer: return "探索者";
                case Difficulty.Veteran: return "熟練者";
                default: return "冒険者";
            }
        }

        public static string DifficultyDescription(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Explorer: return "ライフ7・敵が弱め";
                case Difficulty.Veteran: return "ライフ5・敵が強め";
                default: return "標準";
            }
        }

        public static int PlayerMaxHealth(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Explorer: return 7;
                case Difficulty.Veteran: return 5;
                default: return 6;
            }
        }

        public static int EnemyHealth(int baseHealth, Difficulty difficulty)
        {
            if (difficulty == Difficulty.Explorer)
            {
                return Mathf.Max(1, baseHealth - 1);
            }
            if (difficulty == Difficulty.Veteran)
            {
                return baseHealth + 1;
            }
            return baseHealth;
        }

        public static float EnemySpeed(float baseSpeed, Difficulty difficulty)
        {
            if (difficulty == Difficulty.Explorer) return baseSpeed * 0.9f;
            if (difficulty == Difficulty.Veteran) return baseSpeed * 1.12f;
            return baseSpeed;
        }
    }
}
