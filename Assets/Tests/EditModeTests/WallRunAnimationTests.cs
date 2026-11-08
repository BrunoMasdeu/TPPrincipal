using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class WallRunAnimationTests
{
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";

    [Test]
    public void WallRunSitsBelowWeaponLayerAndCannotAnimateRoot()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int wall = System.Array.FindIndex(controller.layers, l => l.name == "Wall Run");
        int upper = System.Array.FindIndex(controller.layers, l => l.name == "Upper Body");
        Assert.That(wall, Is.GreaterThan(0));
        Assert.That(upper, Is.GreaterThan(wall));
        Assert.That(controller.layers[wall].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
        var states = controller.layers[upper].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string state in new[] { "Pistol Combat Pose", "Pistol Combat Fire", "Pistol Combat Reload",
                     "Rifle Combat Pose", "Rifle Combat Fire", "Rifle Combat Reload" })
            Assert.That(states, Does.Contain(state));
    }

    [TestCase("Left")]
    [TestCase("Right")]
    public void ClipsAreHumanoidLoopsWithContinuousLegs(string side)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath($"Assets/Animations/WallRun/WallRun{side}.fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        Assert.That(clip.humanMotion, Is.True);
        Assert.That(clip.isLooping, Is.True);
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Soldier/Mark_1_Olive_IdlePose.fbx"));
        try
        {
            var animator = model.GetComponent<Animator>();
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            clip.SampleAnimation(model, 0);
            Vector3 start = foot.localPosition;
            Quaternion rotation = foot.localRotation;
            clip.SampleAnimation(model, clip.length);
            Assert.That(Vector3.Distance(start, foot.localPosition), Is.LessThan(0.005f));
            Assert.That(Quaternion.Angle(rotation, foot.localRotation), Is.LessThan(1f));
            clip.SampleAnimation(model, clip.length * 0.5f);
            Assert.That(Quaternion.Angle(rotation, foot.localRotation), Is.GreaterThan(1f));
        }
        finally { Object.DestroyImmediate(model); }
    }

    [TestCase(1, "Pistol")]
    [TestCase(2, "Rifle")]
    public void FireAndReloadStillTransitionDuringWallRun(int pose, string weapon)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab"));
        try
        {
            var animator = player.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            int wall = animator.GetLayerIndex("Wall Run");
            int upper = animator.GetLayerIndex("Upper Body");
            animator.SetLayerWeight(wall, 1);
            animator.SetInteger("WallRunSide", 1);
            animator.SetFloat("WallRunRate", 1);
            animator.SetInteger("WeaponPose", pose);
            animator.Play("Wall Run Right", wall, 0.25f);
            animator.Play(weapon + " Combat Pose", upper, 0);
            animator.Update(0);
            animator.SetTrigger("Fire");
            for (int i = 0; i < 5; i++) animator.Update(0.02f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(upper).IsName(weapon + " Combat Fire"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(wall).IsName("Wall Run Right"), Is.True);
            animator.SetBool("Reloading", true);
            for (int i = 0; i < 8; i++) animator.Update(0.02f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(upper).IsName(weapon + " Combat Reload"), Is.True);
            Assert.That(animator.GetLayerWeight(wall), Is.EqualTo(1));
            Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(player); }
    }
}
