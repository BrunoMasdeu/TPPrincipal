using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FootstepJumpTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void AcceptedJumpsPlayAfterLandingCooldownAndSurviveFirstPhysicsSample()
    {
        var player = new GameObject("Jump audio test");
        var clip = AudioClip.Create("Test jump", 4410, 1, 44100, false);
        int sounds = 0;
        Application.LogCallback countSounds = (message, trace, type) =>
        {
            if (message.StartsWith("[FootstepDebug] SALTO |")) sounds++;
        };
        Application.logMessageReceived += countSounds;
        try
        {
            var move = player.AddComponent<Move>();
            var audio = player.AddComponent<FootstepAudio>();
            // EditMode does not run MonoBehaviour's Awake/OnEnable lifecycle.
            Invoke(audio, "Awake");
            Set(audio, "jumpClips", new[] { clip });
            Invoke(audio, "OnEnable");
            Set(audio, "lastAirSoundTime", Time.time);

            // The real event emitted by Move after accepting a jump.
            var eventField = typeof(Move).GetField("Jumped", PrivateInstance);
            ((Action)eventField.GetValue(move)).Invoke();
            Assert.That(sounds, Is.EqualTo(1), "A recent landing must not silence an accepted jump.");
            Invoke(audio, "HandleJumpAndLand", true, true, true, 0f, 0.02f);
            Assert.That(Get<bool>(audio, "airborne"), Is.True,
                "Before physics applies the impulse, zero velocity must not count as landing.");

            Invoke(audio, "HandleJumpAndLand", false, false, false, 2f, 0.1f);
            Invoke(audio, "HandleJumpAndLand", true, true, true, -1f, 0.3f);
            Assert.That(Get<bool>(audio, "airborne"), Is.False);
            ((Action)eventField.GetValue(move)).Invoke();
            Assert.That(sounds, Is.EqualTo(2), "The next accepted jump must also play.");

            Invoke(audio, "OnDisable");
            Assert.That(eventField.GetValue(move), Is.Null);
            Invoke(audio, "OnEnable");
            ((Action)eventField.GetValue(move)).Invoke();
            Assert.That(sounds, Is.EqualTo(3), "Re-enabling must subscribe exactly once.");
        }
        finally
        {
            Application.logMessageReceived -= countSounds;
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void LocalUpwardMotionWithoutJumpEventDoesNotProduceJumpAudio()
    {
        var player = new GameObject("Stairs audio test");
        try
        {
            player.AddComponent<Move>();
            var audio = player.AddComponent<FootstepAudio>();
            Invoke(audio, "Awake");
            Set(audio, "wasEffectiveGrounded", true);
            Invoke(audio, "HandleJumpAndLand", true, true, true, 9f, 0.02f);
            Assert.That(Get<bool>(audio, "jumpPending"), Is.False);
            Assert.That(Get<bool>(audio, "jumped"), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    private static void Invoke(FootstepAudio target, string method, params object[] args) =>
        typeof(FootstepAudio).GetMethod(method, PrivateInstance).Invoke(target, args);
    private static void Set(FootstepAudio target, string field, object value) =>
        typeof(FootstepAudio).GetField(field, PrivateInstance).SetValue(target, value);
    private static T Get<T>(FootstepAudio target, string field) =>
        (T)typeof(FootstepAudio).GetField(field, PrivateInstance).GetValue(target);
}
