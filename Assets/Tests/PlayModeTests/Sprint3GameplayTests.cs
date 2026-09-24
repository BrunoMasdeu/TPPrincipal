using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class Sprint3GameplayTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<InputDevice> createdDevices = new List<InputDevice>();

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (InputDevice device in createdDevices)
        {
            if (device != null && device.added)
                InputSystem.RemoveDevice(device);
        }

        createdDevices.Clear();

        foreach (GameObject gameObject in createdObjects)
        {
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        createdObjects.Clear();
        Time.timeScale = 1f;
    }

    [UnityTest]
    [Category("TST_S3_014")]
    public IEnumerator TST_S3_014_RigidbodyConservaOrientacionYEstadoFisicoValido()
    {
        CreateGround();
        Move move = CreatePlayerWithMove(Vector3.up, out Rigidbody body);

        yield return WaitForPhysicsSteps(3);

        Quaternion initialRotation = body.rotation;
        yield return DriveMove(move, Vector2.up, false, 4);
        InvokePrivate(move, "Jump");
        yield return WaitForPhysicsSteps(20);

        Assert.IsTrue(body.freezeRotation, "El Rigidbody no mantiene la rotación congelada.");
        Assert.That(Quaternion.Angle(initialRotation, body.rotation), Is.LessThan(0.5f));
        Assert.That(
            Vector3.Distance(body.position, Vector3.up),
            Is.GreaterThan(0.05f),
            "El controlador no respondió al input de movimiento/salto."
        );
        AssertVectorIsFinite(body.position, "posición");
        AssertVectorIsFinite(body.linearVelocity, "velocidad");
        Assert.IsFalse(move.wallrunning, "El fixture activó Wall Running sin una pared válida.");
    }

    [UnityTest]
    [Category("TST_S3_015")]
    public IEnumerator TST_S3_015_VelocidadAceleracionYFrenadoRespetanLaConfiguracion()
    {
        CreateGround();
        Move move = CreatePlayerWithMove(Vector3.up, out Rigidbody body);

        yield return WaitForPhysicsSteps(3);

        yield return DriveMove(move, Vector2.up, false, 35);
        float walkingSpeed = HorizontalSpeed(body);

        Assert.That(walkingSpeed, Is.GreaterThan(move.speed - 0.5f));
        Assert.That(walkingSpeed, Is.LessThanOrEqualTo(move.speed + 0.15f));

        yield return DriveMove(move, Vector2.zero, false, 20);
        Assert.That(HorizontalSpeed(body), Is.LessThan(0.15f));

        yield return DriveMove(move, Vector2.up, true, 40);
        float runningSpeed = HorizontalSpeed(body);

        Assert.That(runningSpeed, Is.GreaterThan(move.speed + 0.5f));
        Assert.That(runningSpeed, Is.LessThanOrEqualTo(move.runSpeed + 0.15f));
    }

    [UnityTest]
    [Category("TST_S3_016")]
    public IEnumerator TST_S3_016_DiagonalesYOpuestosNoGeneranVentajaNiOscilacion()
    {
        CreateGround();
        Move move = CreatePlayerWithMove(Vector3.up, out Rigidbody body);

        yield return WaitForPhysicsSteps(3);

        yield return DriveMove(move, Vector2.up, false, 35);
        float straightSpeed = HorizontalSpeed(body);

        Assert.That(straightSpeed, Is.GreaterThan(move.speed - 0.5f));

        body.linearVelocity = Vector3.zero;
        yield return DriveMove(move, new Vector2(1f, 1f).normalized, false, 35);
        float diagonalSpeed = HorizontalSpeed(body);

        Assert.That(diagonalSpeed, Is.GreaterThan(move.speed - 0.5f));
        Assert.That(diagonalSpeed, Is.LessThanOrEqualTo(straightSpeed + 0.15f));
        Assert.That(diagonalSpeed, Is.LessThanOrEqualTo(move.speed + 0.15f));

        for (int i = 0; i < 20; i++)
        {
            Vector2 input = i % 2 == 0 ? Vector2.left : Vector2.right;
            yield return DriveMove(move, input, false, 2);

            AssertVectorIsFinite(body.linearVelocity, "velocidad al alternar direcciones");
            Assert.That(HorizontalSpeed(body), Is.LessThanOrEqualTo(move.speed + 0.15f));
        }

        yield return DriveMove(move, Vector2.zero, false, 10);
        Assert.That(HorizontalSpeed(body), Is.LessThan(0.15f));
    }

    [UnityTest]
    [Category("TST_S3_017")]
    public IEnumerator TST_S3_017_JugadorNoAtraviesaUnaParedEstatica()
    {
        CreateGround();
        Move move = CreatePlayerWithMove(Vector3.up, out Rigidbody body);
        Collider playerCollider = move.GetComponent<Collider>();
        GameObject wall = CreateCube("Sprint3Wall", new Vector3(2.5f, 1.5f, 0f), new Vector3(1f, 4f, 6f));
        Collider wallCollider = wall.GetComponent<Collider>();

        yield return WaitForPhysicsSteps(3);

        yield return DriveMove(move, Vector2.right, false, 100);

        bool penetrates = Physics.ComputePenetration(
            playerCollider,
            playerCollider.transform.position,
            playerCollider.transform.rotation,
            wallCollider,
            wallCollider.transform.position,
            wallCollider.transform.rotation,
            out _,
            out float penetrationDistance
        );

        Assert.IsFalse(
            penetrates && penetrationDistance > 0.02f,
            "El jugador penetró de forma apreciable la pared."
        );
        Assert.That(body.position.x, Is.GreaterThan(0.5f), "El jugador no avanzó hacia la pared.");
        Assert.That(body.position.x, Is.LessThan(2.05f));
        Assert.That(wall.transform.position, Is.EqualTo(new Vector3(2.5f, 1.5f, 0f)));
    }

    [UnityTest]
    [Category("TST_S3_019")]
    public IEnumerator TST_S3_019_GanchoValidoActivaVisualesYAcercaAlJugador()
    {
        CreateMouse();
        Grappling grappling = CreateGrapplingFixture(out Rigidbody body, out LineRenderer line, out Transform hook);
        GameObject target = CreateCube("ValidGrappleTarget", new Vector3(0f, 0f, 8f), new Vector3(2f, 2f, 1f));
        target.layer = 8;
        grappling.whatIsGrappleable = 1 << target.layer;

        yield return null;
        Physics.SyncTransforms();

        InvokePrivate(grappling, "StartGrapple");

        Assert.IsTrue(grappling.IsGrappling(), "El gancho no se activó sobre un objetivo válido.");
        Assert.IsTrue(line.enabled, "La línea del gancho no se hizo visible.");
        Assert.IsTrue(hook.gameObject.activeSelf, "La cabeza del gancho no se hizo visible.");

        float initialDistance = Vector3.Distance(body.position, grappling.GetGrapplePoint());
        yield return WaitForPhysicsSteps(8);
        float finalDistance = Vector3.Distance(body.position, grappling.GetGrapplePoint());

        Assert.That(finalDistance, Is.LessThan(initialDistance));
        AssertVectorIsFinite(body.linearVelocity, "velocidad del gancho");
    }

    [UnityTest]
    [Category("TST_S3_020")]
    public IEnumerator TST_S3_020_GanchoInvalidoNoAlteraEstadoNiMovimiento()
    {
        CreateMouse();
        Grappling grappling = CreateGrapplingFixture(out Rigidbody body, out LineRenderer line, out Transform hook);
        grappling.whatIsGrappleable = 1 << 8;
        body.linearVelocity = new Vector3(1f, 0f, 2f);

        yield return null;
        Vector3 initialVelocity = body.linearVelocity;

        for (int i = 0; i < 3; i++)
            InvokePrivate(grappling, "StartGrapple");

        Assert.IsFalse(grappling.IsGrappling());
        Assert.IsFalse(line.enabled);
        Assert.IsFalse(hook.gameObject.activeSelf);
        Assert.That(body.linearVelocity, Is.EqualTo(initialVelocity));
    }

    [UnityTest]
    [Category("TST_S3_024")]
    public IEnumerator TST_S3_024_ObstaculoProvocaUnSoloRespawnSinVelocidadResidual()
    {
        CreateGround(new Vector3(5f, -0.5f, 0f), new Vector3(5f, 1f, 5f));

        GameObject spawn = CreateGameObject("Sprint3Respawn", new Vector3(5f, 1f, 0f));
        GameObject deathZoneObject = CreateGameObject("Sprint3DeathZone", new Vector3(0f, -1f, 0f));
        BoxCollider deathTrigger = deathZoneObject.AddComponent<BoxCollider>();
        deathTrigger.isTrigger = true;
        deathTrigger.size = new Vector3(4f, 1f, 4f);
        ZonaMuerte deathZone = deathZoneObject.AddComponent<ZonaMuerte>();
        deathZone.puntoDeRespawn = spawn.transform;

        GameObject player = CreateGameObject("Sprint3RespawnPlayer", new Vector3(0f, 2f, 0f));
        player.tag = "Player";
        player.AddComponent<CapsuleCollider>();
        Rigidbody body = player.AddComponent<Rigidbody>();
        body.freezeRotation = true;
        body.linearVelocity = Vector3.down * 5f;

        int respawnLogs = 0;
        Application.LogCallback callback = (message, _, _) =>
        {
            if (message.Contains("Situación inválida"))
                respawnLogs++;
        };
        Application.logMessageReceived += callback;

        bool respawned = false;

        try
        {
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();

                if (Vector3.Distance(body.position, spawn.transform.position) < 0.2f)
                {
                    respawned = true;
                    break;
                }
            }

            Assert.IsTrue(respawned, "ZonaMuerte no recuperó al jugador.");
            Assert.That(new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude, Is.LessThan(0.05f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(-0.3f));

            yield return WaitForPhysicsSteps(10);
            Assert.That(respawnLogs, Is.EqualTo(1), "El mismo evento produjo más de un respawn.");
            Assert.That(body.position.x, Is.EqualTo(spawn.transform.position.x).Within(0.1f));
        }
        finally
        {
            Application.logMessageReceived -= callback;
        }
    }

    private Move CreatePlayerWithMove(Vector3 position, out Rigidbody body)
    {
        GameObject player = CreateGameObject("Sprint3Player", position);
        player.tag = "Player";

        CapsuleCollider collider = player.AddComponent<CapsuleCollider>();
        collider.height = 2f;
        collider.radius = 0.5f;

        body = player.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.None;

        Transform orientation = CreateChild(player.transform, "Orientation", Vector3.zero);
        Transform groundCheck = CreateChild(player.transform, "GroundCheck", Vector3.down * 0.95f);

        Move move = player.AddComponent<Move>();
        move.orientation = orientation;
        move.groundCheck = groundCheck;
        move.groundDistance = 0.2f;
        move.groundMask = 1 << 0;
        move.speed = 6f;
        move.runSpeed = 10f;
        move.acceleration = 20f;
        move.jumpForce = 7f;
        move.enabled = false;

        Physics.SyncTransforms();
        return move;
    }

    private Grappling CreateGrapplingFixture(
        out Rigidbody body,
        out LineRenderer line,
        out Transform hook)
    {
        GameObject player = CreateGameObject("Sprint3GrapplingPlayer", Vector3.zero);
        body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.freezeRotation = true;

        GameObject cameraObject = CreateGameObject("Sprint3GrapplingCamera", Vector3.zero);
        cameraObject.transform.SetParent(player.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.localRotation = Quaternion.identity;

        Transform gunTip = CreateChild(player.transform, "GunTip", Vector3.forward * 0.5f);
        hook = CreateChild(player.transform, "HookHead", Vector3.zero);
        hook.gameObject.SetActive(false);

        line = player.AddComponent<LineRenderer>();
        line.enabled = false;

        Grappling grappling = player.AddComponent<Grappling>();
        grappling.cam = camera;
        grappling.gunTip = gunTip;
        grappling.hookHead = hook;
        grappling.lr = line;
        grappling.maxGrappleDistance = 20f;
        grappling.grappleForce = 10f;
        grappling.stopDistance = 0.5f;
        grappling.grapplingCd = 0f;

        return grappling;
    }

    private GameObject CreateGround()
    {
        return CreateGround(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
    }

    private GameObject CreateGround(Vector3 position, Vector3 scale)
    {
        return CreateCube("Sprint3Ground", position, scale);
    }

    private GameObject CreateCube(string name, Vector3 position, Vector3 scale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        createdObjects.Add(cube);
        Physics.SyncTransforms();
        return cube;
    }

    private GameObject CreateGameObject(string name, Vector3 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private void CreateMouse()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>("Sprint3TestMouse");
        createdDevices.Add(mouse);
        mouse.MakeCurrent();
    }

    private static IEnumerator WaitForPhysicsSteps(int count)
    {
        for (int i = 0; i < count; i++)
            yield return new WaitForFixedUpdate();
    }

    private static IEnumerator DriveMove(
        Move move,
        Vector2 input,
        bool running,
        int physicsSteps)
    {
        SetPrivateField(move, "input", Vector2.ClampMagnitude(input, 1f));
        SetPrivateField(move, "running", running);

        for (int i = 0; i < physicsSteps; i++)
        {
            InvokePrivate(move, "MovePlayer");
            yield return new WaitForFixedUpdate();
        }
    }

    private static float HorizontalSpeed(Rigidbody body)
    {
        return new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
    }

    private static void AssertVectorIsFinite(Vector3 value, string label)
    {
        Assert.IsFalse(float.IsNaN(value.x) || float.IsInfinity(value.x), $"{label}.x no es finito.");
        Assert.IsFalse(float.IsNaN(value.y) || float.IsInfinity(value.y), $"{label}.y no es finito.");
        Assert.IsFalse(float.IsNaN(value.z) || float.IsInfinity(value.z), $"{label}.z no es finito.");
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.IsNotNull(method, $"No se encontró el método {methodName}.");
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.IsNotNull(field, $"No se encontró el campo {fieldName}.");
        field.SetValue(target, value);
    }
}
