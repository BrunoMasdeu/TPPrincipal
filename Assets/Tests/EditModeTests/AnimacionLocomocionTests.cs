using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// [ANIMACIONES] Quieto, caminar y correr (adelante y atrás) en un solo blend
// tree, sin cortes, con los clips del pack de Kevin Iglesias. El agachado es
// lo único que queda de Mixamo.
public class AnimacionLocomocionTests
{
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string AvatarPath = "Assets/Animations/Pistol1/X Bot.fbx";
    private const string Kit = "Assets/Animations/Mixamo/Lite Rifle Pack/";
    private const string Kit8 = "Assets/Animations/Mixamo/Rifle 8-Way Locomotion Pack/";
    private const string Pack = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";

    // ---- El valor que recibe el blend tree ----

    [TestCase(0f, false, 0f)]
    [TestCase(0.05f, true, 0f)]
    [TestCase(3f, false, 0.5f)]
    [TestCase(6f, false, 1f)]
    [TestCase(9f, false, 1f)]
    [TestCase(7f, true, 2f)]
    public void LocomocionSegunVelocidadYCorrer(float velocidad, bool corriendo, float esperado)
    {
        Assert.That(
            PlayerAnimationController.Locomocion(velocidad, corriendo, 6f),
            Is.EqualTo(esperado).Within(0.0001f));
    }

    [TestCase(6f, false, -1f)]
    [TestCase(7f, true, -2f)]
    [TestCase(0.05f, false, 0f)]
    public void LocomocionHaciaAtrasEsNegativa(float velocidad, bool corriendo, float esperado)
    {
        Assert.That(
            PlayerAnimationController.Locomocion(velocidad, corriendo, 6f, true),
            Is.EqualTo(esperado).Within(0.0001f));
    }

    // ---- Cómo quedó el Animator ----

    [Test]
    public void LaCapaBaseUsaUnBlendTreeQuietoCaminarCorrer()
    {
        AnimatorStateMachine baseLayer = Controller().layers[0].stateMachine;
        AnimatorState locomocion = baseLayer.defaultState;

        Assert.That(locomocion.name, Is.EqualTo("Locomotion Base"),
            "PlayerAnimationController vuelve a este estado por nombre al reaparecer.");

        BlendTree arbol = locomocion.motion as BlendTree;
        Assert.That(arbol, Is.Not.Null, "Locomotion Base tiene que ser un blend tree.");
        Assert.That(arbol.blendParameter, Is.EqualTo("Locomocion"));
        Assert.That(arbol.children.Select(c => c.threshold),
            Is.EqualTo(new[] { -2f, -1f, 0f, 1f, 2f }));
        Assert.That(arbol.children.All(c => c.motion != null), Is.True,
            "Falta un clip en el blend tree.");
        Assert.That(
            arbol.children.Select(c => AssetDatabase.GetAssetPath(c.motion)),
            Is.EqualTo(new[]
            {
                Pack + "Movement/Run/HumanM@Run01_Backward.fbx",
                Pack + "Movement/Walk/HumanM@Walk01_Backward.fbx",
                Pack + "Idles/HumanM@Idle01.fbx",
                Pack + "Movement/Walk/HumanM@Walk01_Forward.fbx",
                Pack + "Movement/Run/HumanM@Run01_Forward.fbx"
            }));
        Assert.That(
            locomocion.transitions.Select(t => t.destinationState.name),
            Is.EqualTo(new[] { "Crouch Idle" }),
            "Caminar y correr no son estados aparte: solo se sale para agacharse.");
    }

    [Test]
    public void ElSaltoUsaElClipDelPack()
    {
        AnimatorState salto = Controller().layers[0].stateMachine.states
            .Select(s => s.state).Single(s => s.name == "Pistol Jump");

        Assert.That(AssetDatabase.GetAssetPath(salto.motion),
            Is.EqualTo(Pack + "Movement/Jump/HumanM@Jump01.fbx"));
    }

    [Test]
    public void AgachadoEntraYSaleConElParametroAgachado()
    {
        AnimatorController controller = Controller();
        Assert.That(
            controller.parameters.Any(p =>
                p.name == "Agachado" && p.type == AnimatorControllerParameterType.Bool),
            Is.True);

        AnimatorState agachado = controller.layers[0].stateMachine.states
            .Select(s => s.state).Single(s => s.name == "Crouch Idle");

        // Adentro: caminar agachado atrás, quieto y adelante.
        BlendTree arbol = agachado.motion as BlendTree;
        Assert.That(arbol, Is.Not.Null, "Crouch Idle tiene que ser un blend tree.");
        Assert.That(arbol.blendParameter, Is.EqualTo("Locomocion"));
        Assert.That(arbol.children.Select(c => c.threshold), Is.EqualTo(new[] { -1f, 0f, 1f }));
        Assert.That(
            arbol.children.Select(c => AssetDatabase.GetAssetPath(c.motion)),
            Is.EqualTo(new[]
            {
                Kit8 + "walk crouching backward.fbx",
                Kit + "idle crouching.fbx",
                Kit8 + "walk crouching forward.fbx"
            }));
        Assert.That(
            agachado.transitions.Select(t => t.destinationState.name),
            Is.EqualTo(new[] { "Locomotion Base" }));
    }

    [TestCase(Kit + "idle crouching.fbx")]
    [TestCase(Kit8 + "walk crouching forward.fbx")]
    [TestCase(Kit8 + "walk crouching backward.fbx")]
    public void LosClipsDeAgachadoSonHumanoidEnLoopYSinBakeEnXZ(string ruta)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ruta);
        Assert.That(importer, Is.Not.Null, ruta);

        Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
        Assert.That(AssetDatabase.GetAssetPath(importer.sourceAvatar), Is.EqualTo(AvatarPath),
            "El clip de Mixamo usa el avatar del X Bot.");

        ModelImporterClipAnimation clip = importer.clipAnimations.Single();
        Assert.That(clip.loopTime, Is.True);
        Assert.That(clip.lockRootPositionXZ, Is.False,
            "Root Transform Position (XZ) va sin Bake Into Pose.");
    }

    [Test]
    public void NoSeTocanLasOtrasCapas()
    {
        Assert.That(Controller().layers.Select(l => l.name),
            Is.EqualTo(new[] { "Base Layer", "Wall Run", "Upper Body", "Death" }));
    }

    [Test]
    public void DeMixamoSoloQuedaElAgachado()
    {
        string[] fbx = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations/Mixamo" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        Assert.That(fbx, Is.EquivalentTo(new[]
        {
            Kit + "idle crouching.fbx",
            Kit8 + "walk crouching forward.fbx",
            Kit8 + "walk crouching backward.fbx"
        }));
    }

    [Test]
    public void YaNoQuedanLosEstadosSueltosDeCaminarYCorrer()
    {
        string[] estados = Controller().layers[0].stateMachine.states
            .Select(s => s.state.name).ToArray();

        Assert.That(estados, Does.Not.Contain("Base_Walking"));
        Assert.That(estados, Does.Not.Contain("Run"));
    }

    [Test]
    public void LaCaidaNoSeReiniciaCadaCuadro()
    {
        AnimatorStateTransition alCaer = Controller().layers[0].stateMachine
            .anyStateTransitions.Single(t => t.destinationState.name == "HumanM@Fall01");

        Assert.That(alCaer.canTransitionToSelf, Is.False);
    }

    [Test]
    public void ElControllerTieneElParametroLocomocion()
    {
        Assert.That(
            Controller().parameters.Any(p =>
                p.name == "Locomocion" && p.type == AnimatorControllerParameterType.Float),
            Is.True);
    }

    [Test]
    public void LaVelocidadDeLaAnimacionSeSuaviza()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        SerializedObject animacion =
            new(prefab.GetComponent<PlayerAnimationController>());

        Assert.That(animacion.FindProperty("speedDampTime").floatValue, Is.GreaterThan(0f));
    }

    private static AnimatorController Controller()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null, ControllerPath);
        return controller;
    }
}
