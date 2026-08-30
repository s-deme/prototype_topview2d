#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace VerdantBlade.Tests
{
    public sealed class VerdantBladeRulesTests
    {
        [Test]
        public void ClampShardsNeverExceedsTheObjective()
        {
            Assert.AreEqual(GameRules.ShardGoal, GameRules.ClampShards(GameRules.ShardGoal - 1, 3));
            Assert.AreEqual(0, GameRules.ClampShards(0, -1));
        }

        [Test]
        public void ScoreOnlyAwardsTimeBonusForACompletedRun()
        {
            var activeScore = GameRules.CalculateScore(8, 100, 2, 4, 3, false, 20f);
            var clearScore = GameRules.CalculateScore(8, 100, 2, 4, 3, true, 20f);
            Assert.AreEqual(350, clearScore - activeScore);
        }

        [Test]
        public void SwiftClearIncludesTheBoundarySecond()
        {
            Assert.IsTrue(GameRules.IsSwiftClear(GameRules.SwiftClearSeconds));
            Assert.IsFalse(GameRules.IsSwiftClear(GameRules.SwiftClearSeconds + 0.01f));
        }

        [Test]
        public void DifficultyPresetsChangePlayerAndEnemyTuningInOppositeDirections()
        {
            Assert.Greater(GameRules.PlayerMaxHealth(Difficulty.Explorer), GameRules.PlayerMaxHealth(Difficulty.Veteran));
            Assert.Greater(GameRules.EnemyHealth(9, Difficulty.Veteran), GameRules.EnemyHealth(9, Difficulty.Explorer));
            Assert.Greater(GameRules.EnemySpeed(2f, Difficulty.Veteran), GameRules.EnemySpeed(2f, Difficulty.Explorer));
        }

        [Test]
        public void RunClockOnlyCountsActiveGameplay()
        {
            var clock = new RunClock();
            clock.Tick(12.5f, true);
            clock.Tick(50f, false);
            clock.Tick(4.25f, true);

            Assert.AreEqual(16.75f, clock.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void RunSnapshotRejectsOutOfRangeProgress()
        {
            var valid = new RunSnapshot { difficulty = (int)Difficulty.Adventurer, worldVariant = 2, shards = GameRules.ShardGoal, elapsedSeconds = 20f };
            var invalid = new RunSnapshot { difficulty = (int)Difficulty.Adventurer, worldVariant = 4, shards = GameRules.ShardGoal + 1, elapsedSeconds = -1f, playerX = float.PositiveInfinity };

            Assert.IsTrue(valid.IsValid());
            Assert.IsFalse(invalid.IsValid());
        }
    }
}
#endif
