#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VerdantBlade.Tests
{
    public sealed class VerdantBladePlayModeTests
    {
        [UnityTest]
        public IEnumerator RunClockPersistsAcrossFramesWithoutCountingPausedFrames()
        {
            var clock = new RunClock();
            clock.Tick(1f, true);
            yield return null;
            clock.Tick(5f, false);
            yield return null;
            clock.Tick(2f, true);

            Assert.AreEqual(3f, clock.ElapsedSeconds, 0.001f);
        }
    }
}
#endif
