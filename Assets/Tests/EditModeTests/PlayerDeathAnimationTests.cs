using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class PlayerDeathAnimationTests
{
    [Test]
    public void DeathIsFullBodyNonLoopingAndLastsEightTenthsOfASecond()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/PlayerAnimatorController.controller");
        var layer = controller.layers.Last();
        Assert.That(layer.name, Is.EqualTo("Death"));
        Assert.That(layer.avatarMask, Is.Null);
        Assert.That(layer.defaultWeight, Is.Zero);
        var state = layer.stateMachine.defaultState;
        var clip = state.motion as AnimationClip;
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.humanMotion, Is.True);
        Assert.That(clip.isLooping, Is.False);
        Assert.That(clip.length / state.speed, Is.EqualTo(0.8f).Within(0.01f));
        Assert.That(state.transitions, Is.Empty);
    }

    [TestCase(1, "Pistol Combat Fire")]
    [TestCase(2, "Rifle Combat Reload")]
    public void DeathOverridesCombatHoldsThroughRespawningAndResetsForNextLife(
        int weaponPose, string weaponState)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/PlayerPrefab.prefab"));
        try
        {
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var presentation = player.GetComponent<PlayerAnimationController>();
            Call(presentation, "Awake");
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("deathBlendTime").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var animator = player.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("IsGrounded", true);
            int death = animator.GetLayerIndex("Death");
            int wall = animator.GetLayerIndex("Wall Run");
            int upper = animator.GetLayerIndex("Upper Body");
            animator.SetInteger("WeaponPose", weaponPose);
            animator.Play(weaponState, upper, 0.2f);
            animator.SetLayerWeight(wall, 1f);
            animator.Play("Wall Run Right", wall, 0.25f);
            animator.Update(0f);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            float standingHeadHeight = head.position.y;

            Call(presentation, "OnLifeStateChanged", PlayerLifeState.Dead);
            Call(presentation, "Update");
            for (int i = 0; i < 50; i++) animator.Update(0.02f);
            Assert.That(animator.GetLayerWeight(death), Is.EqualTo(1f));
            Assert.That(animator.GetLayerWeight(wall), Is.Zero);
            Assert.That(animator.GetBool("Reloading"), Is.False);
            Assert.That(head.position.y, Is.LessThan(standingHeadHeight - 0.5f));
            Vector3 deadHead = head.position;
            Quaternion deadRotation = head.rotation;
            animator.Update(4f);
            Assert.That(Vector3.Distance(head.position, deadHead), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(head.rotation, deadRotation), Is.LessThan(1f));
            Call(presentation, "OnLifeStateChanged", PlayerLifeState.Respawning);
            animator.Update(0.02f);
            Assert.That(Vector3.Distance(head.position, deadHead), Is.LessThan(0.01f));
            Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.identity), Is.LessThan(0.01f));

            Call(presentation, "OnLifeStateChanged", PlayerLifeState.Alive);
            animator.Update(0.02f);
            Assert.That(animator.GetLayerWeight(death), Is.Zero);
            Assert.That(head.position.y, Is.GreaterThan(deadHead.y + 0.5f));
            Call(presentation, "OnLifeStateChanged", PlayerLifeState.Dead);
            Call(presentation, "Update");
            animator.Update(0.02f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(death).normalizedTime, Is.LessThan(0.1f),
                "A second death must replay the fall from its beginning.");
        }
        finally { Object.DestroyImmediate(player); }
    }

    private static void Call(PlayerAnimationController target, string method, params object[] args)
    {
        typeof(PlayerAnimationController).GetMethod(method,
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
}
