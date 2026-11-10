using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PlayerCrouchAnimationTests
{
    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", 0f)]
    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", 2f)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", 0f)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", 2f)]
    public void CrouchLowersBodyKeepsAnimatedFeetAndRestoresStanding(string path, float speed)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var crouch = player.GetComponent<Crouch>();
            var pose = player.GetComponent<PlayerCrouchAnimation>();
            Assert.That(pose, Is.Not.Null);
            Call(crouch, "Awake");
            Call(pose, "Awake");
            var animator = player.GetComponent<Animator>();
            animator.Rebind();
            animator.SetBool("IsGrounded", true);
            animator.SetFloat("Speed", speed);
            float standingHeight = crouch.capsule.height;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            for (int frame = 0; frame < 40; frame++)
            {
                animator.Update(0.025f);
                Vector3 left = leftFoot.position;
                Vector3 right = rightFoot.position;
                float headHeight = head.position.y;
                crouch.capsule.height = crouch.crouchHeight;
                Call(pose, "LateUpdate");
                Assert.That(head.position.y, Is.LessThan(headHeight - 0.4f));
                Assert.That(Vector3.Distance(left, leftFoot.position), Is.LessThan(0.03f));
                Assert.That(Vector3.Distance(right, rightFoot.position), Is.LessThan(0.03f));
                Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
            }
            crouch.capsule.height = standingHeight;
            animator.Update(0.025f);
            Vector3 standingHead = head.position;
            Call(pose, "LateUpdate");
            Assert.That(Vector3.Distance(standingHead, head.position), Is.LessThan(0.001f));
        }
        finally { Object.DestroyImmediate(player); }
    }

    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
