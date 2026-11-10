using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FootstepCadenceTests
{
    [TestCase(30)]
    [TestCase(100)]
    [TestCase(144)]
    public void PhysicsSamplesAndEmptyFramesNeverBypassMinimumInterval(int fps)
    {
        var go = new GameObject("Footstep cadence test");
        try
        {
            var footsteps = go.AddComponent<FootstepAudio>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var tick = typeof(FootstepAudio).GetMethod("UpdateStepCadence", flags);
            var lastTime = typeof(FootstepAudio).GetField("lastStepTime", flags);
            float previousPosition = 0f;
            float previousStep = -999f;
            var times = new List<float>();
            for (int frame = 0; frame < fps * 10; frame++)
            {
                float now = frame / (float)fps;
                float position = Mathf.Floor(now / 0.02f) * 0.02f * 6f;
                float distance = position - previousPosition;
                previousPosition = position;
                tick.Invoke(footsteps, new object[] { distance > 0f, false, distance, now, default(RaycastHit) });
                float emitted = (float)lastTime.GetValue(footsteps);
                if (emitted == previousStep) continue;
                Assert.That(emitted - previousStep, Is.GreaterThanOrEqualTo(0.28f - 0.0001f));
                times.Add(emitted);
                previousStep = emitted;
            }
            Assert.That(times.Count, Is.InRange(16, 18), "Cadence should follow distance at 6 m/s.");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
