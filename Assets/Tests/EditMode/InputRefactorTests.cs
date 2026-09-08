#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace VerdantBlade.Tests
{
    public sealed class InputRefactorTests
    {
        [TestCase(GameAction.Attack, KeyCode.Space, false)]
        [TestCase(GameAction.Dash, KeyCode.Space, true)]
        [TestCase(GameAction.Pause, KeyCode.Escape, false)]
        public void BindingConflictsExcludeTheCurrentAction(GameAction action, KeyCode key, bool expected)
        {
            var bindings = new Dictionary<GameAction, KeyCode>
            {
                [GameAction.Attack] = KeyCode.Space,
                [GameAction.Dash] = KeyCode.LeftShift
            };
            Assert.AreEqual(expected, Invoke(typeof(GameInput), "HasConflictingBinding", bindings, action, key));
        }

        [Test]
        public void CachedKeyCodesKeepTheOriginalEnumerationOrder()
        {
            CollectionAssert.AreEqual(Enum.GetValues(typeof(KeyCode)), (KeyCode[])Field(typeof(GameInput), "KeyCodes").GetValue(null));
        }

        [Test]
        public void ResetBindingsOnlyDeletesTheSpecifiedActions()
        {
            var prefix = "VerdantBlade.RefactorTest." + Guid.NewGuid().ToString("N") + ".";
            var initialized = Field(typeof(PlayerProfile), "initialized");
            var previousInitialized = initialized.GetValue(null);
            var actions = new[] { GameAction.Attack, GameAction.Dash };
            try
            {
                initialized.SetValue(null, true);
                foreach (var action in actions) PlayerPrefs.SetInt(prefix + action, 1);
                PlayerPrefs.SetInt(prefix + GameAction.Pause, 7);
                Invoke(typeof(PlayerProfile), "ResetBindings", prefix, actions);
                foreach (var action in actions) Assert.IsFalse(PlayerPrefs.HasKey(prefix + action));
                Assert.AreEqual(7, PlayerPrefs.GetInt(prefix + GameAction.Pause));
            }
            finally
            {
                foreach (var action in actions) PlayerPrefs.DeleteKey(prefix + action);
                PlayerPrefs.DeleteKey(prefix + GameAction.Pause);
                initialized.SetValue(null, previousInitialized);
                PlayerPrefs.Save();
            }
        }

        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        private static object Invoke(Type type, string name, params object[] arguments) =>
            type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
    }
}
#endif
