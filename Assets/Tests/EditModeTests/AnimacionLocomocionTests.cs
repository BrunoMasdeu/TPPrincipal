using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// [ANIMACIONES] Quieto, caminar y correr en un solo blend tree, sin cortes.
public class AnimacionLocomocionTests
{
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

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
        Assert.That(arbol.children.Select(c => c.threshold), Is.EqualTo(new[] { 0f, 1f, 2f }));
        Assert.That(arbol.children.All(c => c.motion != null), Is.True,
            "Falta un clip en el blend tree.");
        Assert.That(locomocion.transitions, Is.Empty,
            "Caminar y correr ya no son estados aparte: no hacen falta transiciones.");
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
