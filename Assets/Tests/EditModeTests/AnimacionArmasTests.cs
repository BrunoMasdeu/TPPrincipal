using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// [ANIMACIONES] Pose y ataque de cada arma en la capa Upper Body.
public class AnimacionArmasTests
{
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";
    private const string Pack1H =
        "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/";

    // ---- Qué pose le toca a cada arma ----

    [TestCase(WeaponId.None, 0)]
    [TestCase(WeaponId.Pistol, 1)]
    [TestCase(WeaponId.Revolver, 1)]
    [TestCase(WeaponId.Grapple, 1)]
    [TestCase(WeaponId.AK, 2)]
    [TestCase(WeaponId.M4, 2)]
    [TestCase(WeaponId.Sniper, 2)]
    [TestCase(WeaponId.Knife, 3)]
    public void CadaArmaTieneSuPose(WeaponId arma, int pose)
    {
        Assert.That(PlayerAnimationController.PoseDelArma(arma), Is.EqualTo(pose));
    }

    // ---- Cómo quedó la capa Upper Body ----

    [Test]
    public void SiguenLosEstadosDePistolaYRifle()
    {
        string[] estados = UpperBody().states.Select(s => s.state.name).ToArray();

        foreach (string estado in new[]
                 {
                     "Neutral Upper Body",
                     "Pistol Combat Pose", "Pistol Combat Fire", "Pistol Combat Reload",
                     "Rifle Combat Pose", "Rifle Combat Fire", "Rifle Combat Reload"
                 })
        {
            Assert.That(estados, Does.Contain(estado));
        }
    }

    [Test]
    public void ElCuchilloTieneSuPose()
    {
        AnimatorState pose = Estado("Knife Combat Pose");

        Assert.That(AssetDatabase.GetAssetPath(pose.motion),
            Is.EqualTo(Pack1H + "HumanM@CombatIdle1H01.fbx"));
        Assert.That(
            pose.transitions.Select(t => t.destinationState.name),
            Is.EqualTo(new[] { "Neutral Upper Body" }));
        Assert.That(
            Estado("Neutral Upper Body").transitions.Count(t =>
                t.destinationState.name == "Knife Combat Pose"),
            Is.EqualTo(1),
            "Correr el menú dos veces no tiene que duplicar la transición.");
    }

    [Test]
    public void ElAtaqueDelCuchilloSaleConFireYVuelveSoloALaPose()
    {
        AnimatorState ataque = Estado("Knife Combat Attack");
        Assert.That(AssetDatabase.GetAssetPath(ataque.motion),
            Is.EqualTo(Pack1H + "HumanM@Attack1H01_R.fbx"));
        Assert.That(ataque.speed, Is.GreaterThan(1f),
            "El ataque se acelera para entrar en el cooldown del cuchillo.");

        AnimatorStateTransition atacar = UpperBody().anyStateTransitions
            .Single(t => t.destinationState == ataque);
        Assert.That(atacar.conditions.Any(c =>
            c.parameter == "Fire" && c.mode == AnimatorConditionMode.If), Is.True);
        Assert.That(atacar.conditions.Any(c =>
            c.parameter == "WeaponPose" && c.mode == AnimatorConditionMode.Equals &&
            Mathf.Approximately(c.threshold, 3f)), Is.True);

        AnimatorStateTransition volver = ataque.transitions.Single();
        Assert.That(volver.destinationState.name, Is.EqualTo("Knife Combat Pose"));
        Assert.That(volver.hasExitTime, Is.True);
    }

    [TestCase("Knife Combat Pose")]
    [TestCase("Knife Combat Attack")]
    public void LosClipsDelCuchilloSonHumanoid(string estado)
    {
        Assert.That(((AnimationClip)Estado(estado).motion).humanMotion, Is.True);
    }

    [Test]
    public void LaGranadaSeLanzaConElTriggerThrow()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(
            controller.parameters.Any(p =>
                p.name == "Throw" && p.type == AnimatorControllerParameterType.Trigger),
            Is.True);

        AnimatorState lanzar = Estado("Grenade Throw");
        Assert.That(AssetDatabase.GetAssetPath(lanzar.motion), Is.EqualTo(
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Grenade/" +
            "HumanM@ThrowGrenade01_L.fbx"));

        AnimatorStateTransition empezar = UpperBody().anyStateTransitions
            .Single(t => t.destinationState == lanzar);
        Assert.That(empezar.conditions.Single().parameter, Is.EqualTo("Throw"));

        AnimatorStateTransition volver = lanzar.transitions.Single();
        Assert.That(volver.destinationState.name, Is.EqualTo("Neutral Upper Body"));
        Assert.That(volver.hasExitTime, Is.True);
    }

    private static AnimatorState Estado(string nombre)
    {
        AnimatorState estado = UpperBody().states
            .Select(s => s.state).FirstOrDefault(s => s.name == nombre);
        Assert.That(estado, Is.Not.Null, "Falta el estado " + nombre);
        return estado;
    }

    private static AnimatorStateMachine UpperBody()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null, ControllerPath);
        return controller.layers.Single(l => l.name == "Upper Body").stateMachine;
    }
}
