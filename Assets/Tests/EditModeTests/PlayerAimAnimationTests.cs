using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerAimAnimationTests
{
    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", 1, false, 0f)]
    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", 2, true, 90f)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", 1, true, -90f)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", 2, false, 180f)]
    public void AimFollowsCameraPreservesGripAndFeetAndYieldsToDeath(
        string path, int weapon, bool crouched, float yaw)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
            Assert.That(player.GetComponent<PlayerAnimationController>(), Is.Not.Null);
            var aim = player.GetComponent<PlayerAimAnimation>();
            var crouch = player.GetComponent<Crouch>();
            var crouchPose = player.GetComponent<PlayerCrouchAnimation>();
            Call(crouch, "Awake");
            Call(crouchPose, "Awake");
            Call(aim, "Awake");
            var animator = player.GetComponent<Animator>();
            animator.Rebind();
            animator.SetBool("IsGrounded", true);
            animator.SetInteger("WeaponPose", weapon);
            int upper = animator.GetLayerIndex("Upper Body");
            animator.Play(weapon == 1 ? "Pistol Combat Pose" : "Rifle Combat Pose", upper, 0f);
            var camera = player.GetComponentInChildren<CameraMovement>(true).transform;
            camera.GetComponent<Camera>().enabled = false;
            float maxPitch = new SerializedObject(aim).FindProperty("maxPitch").floatValue;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (crouched) crouch.capsule.height = crouch.crouchHeight;

            foreach (float pitch in new[] { -90f, 0f, 90f })
            {
                camera.localRotation = Quaternion.Euler(pitch, 0, 0);
                for (int frame = 0; frame < 10; frame++)
                {
                    Call(aim, "Update");
                    animator.Update(0.02f);
                    Call(crouchPose, "LateUpdate");
                    Vector3 grip = left.position - right.position;
                    Vector3 footPosition = foot.position;
                    Quaternion handRotation = right.rotation;
                    Quaternion headRotation = head.rotation;
                    Quaternion cameraRotation = camera.rotation;
                    float expected = Mathf.Clamp(pitch, -maxPitch, maxPitch);
                    Quaternion delta = Quaternion.AngleAxis(expected - crouchPose.AppliedTorsoLean, player.transform.right);
                    Call(aim, "LateUpdate");
                    Assert.That(aim.PresentedPitch, Is.EqualTo(expected).Within(0.01f));
                    Assert.That(Quaternion.Angle(right.rotation, delta * handRotation), Is.LessThan(0.1f));
                    Assert.That(Quaternion.Angle(head.rotation, delta * headRotation), Is.LessThan(0.1f));
                    Assert.That(Vector3.Distance(left.position - right.position, delta * grip), Is.LessThan(0.005f));
                    Assert.That(Vector3.Distance(footPosition, foot.position), Is.LessThan(0.001f));
                    Assert.That(Quaternion.Angle(camera.rotation, cameraRotation), Is.LessThan(0.01f));
                }
            }

            var health = player.GetComponent<NetworkPlayerHealth>();
            var life = (NetworkVariable<PlayerLifeState>)typeof(NetworkPlayerHealth)
                .GetField("lifeState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(health);
            life.Value = PlayerLifeState.Dead;
            Quaternion deadHead = head.rotation;
            Call(aim, "LateUpdate");
            Assert.That(Quaternion.Angle(head.rotation, deadHead), Is.LessThan(0.01f));
            Assert.That(aim.PresentedPitch, Is.Zero);
            life.Value = PlayerLifeState.Alive;
            Call(aim, "Update");
            animator.Update(0.02f);
            Call(crouchPose, "LateUpdate");
            Call(aim, "LateUpdate");
            Assert.That(aim.PresentedPitch, Is.EqualTo(maxPitch).Within(0.01f));
        }
        finally { Object.DestroyImmediate(player); }
    }

    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", false, "Pistol Combat Pose", 1)]
    [TestCase("Assets/Prefabs/PlayerPrefab.prefab", true, "Rifle Combat Pose", 2)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", false, "Rifle Combat Reload", 2)]
    [TestCase("Assets/Prefabs/Survivalist (3).prefab", true, "Pistol Combat Reload", 1)]
    public void LocalArmsKeepTheirPlacementBelowCameraAtAnyPitch(
        string path, bool crouched, string weaponState, int weapon)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            var aim = player.GetComponent<PlayerAimAnimation>();
            var crouch = player.GetComponent<Crouch>();
            var crouchPose = player.GetComponent<PlayerCrouchAnimation>();
            Call(crouch, "Awake");
            Call(crouchPose, "Awake");
            Call(aim, "Awake");
            var camera = player.GetComponentInChildren<CameraMovement>(true).GetComponent<Camera>();
            camera.enabled = true;
            var animator = player.GetComponent<Animator>();
            animator.Rebind();
            animator.SetBool("IsGrounded", true);
            animator.SetInteger("WeaponPose", weapon);
            animator.SetBool("Reloading", weaponState.EndsWith("Reload"));
            int upper = animator.GetLayerIndex("Upper Body");
            if (crouched)
            {
                crouch.capsule.height = crouch.crouchHeight;
                camera.transform.localPosition -= Vector3.up * crouch.cameraOffset;
            }
            var armBones = new[]
            {
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
            };
            var baseline = new Vector3[armBones.Length];
            Quaternion baselineHandRotation = Quaternion.identity;
            var skins = player.GetComponentsInChildren<SkinnedMeshRenderer>();
            var originalMeshes = System.Array.ConvertAll(skins, skin => skin.sharedMesh);
            foreach (float pitch in new[] { 0f, -45f, -90f, 45f, 90f })
            {
                Call(aim, "Update");
                animator.Play("Locomotion Base", 0, 0.2f);
                animator.Play(weaponState, upper, 0.35f);
                animator.Update(0f);
                Call(crouchPose, "LateUpdate");
                camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                Call(aim, "LateUpdate");
                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Vector3 worldHandPosition = rightHand.position;
                Call(aim, "BeginCameraRendering", default(ScriptableRenderContext), camera);
                Assert.That(System.Array.Exists(skins, skin => skin.forceRenderingOff), Is.True);
                Assert.That(System.Array.Exists(player.GetComponentsInChildren<SkinnedMeshRenderer>(), skin =>
                    !skin.forceRenderingOff && skin.sharedMesh.name.EndsWith("(First Person Arms)")), Is.True);
                for (int i = 0; i < armBones.Length; i++)
                {
                    Vector3 position = Quaternion.Inverse(camera.transform.rotation) *
                        (animator.GetBoneTransform(armBones[i]).position - camera.transform.position);
                    Assert.That(position.y, Is.LessThan(-0.1f), $"{armBones[i]} at {pitch} degrees");
                    if (pitch == 0f) baseline[i] = position;
                    Assert.That(Vector3.Distance(position, baseline[i]), Is.LessThan(0.005f),
                        $"{armBones[i]} drifted relative to the camera at {pitch} degrees");
                }
                Quaternion handRotation = Quaternion.Inverse(camera.transform.rotation) *
                    animator.GetBoneTransform(HumanBodyBones.RightHand).rotation;
                if (pitch == 0f) baselineHandRotation = handRotation;
                Assert.That(Quaternion.Angle(handRotation, baselineHandRotation), Is.LessThan(0.1f));
                Call(aim, "EndCameraRendering", default(ScriptableRenderContext), camera);
                Assert.That(Vector3.Distance(rightHand.position, worldHandPosition), Is.LessThan(0.001f));
                CollectionAssert.AreEqual(originalMeshes, System.Array.ConvertAll(skins, skin => skin.sharedMesh));
            }
        }
        finally { Object.DestroyImmediate(player); }
    }

    private static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
