using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class Sprint4GameplayAutomatedTests : InputTestFixture
{
    private readonly List<GameObject> createdObjects = new();
    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();
        Time.timeScale = 1f;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
    }

    public override void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
                Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
        Time.timeScale = 1f;
        base.TearDown();
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_020")]
    public IEnumerator TST_S4_020_UnaSuperficieNoInteractuableNoActivaElGancho()
    {
        Grappling grappling = CreateGrapplingFixture(
            out Rigidbody body,
            out LineRenderer line,
            out Transform hook
        );
        CreateCube("InvalidTarget", new Vector3(0f, 0f, 6f), new Vector3(2f, 2f, 1f));
        grappling.whatIsGrappleable = 1 << 8;
        body.linearVelocity = new Vector3(1f, 0f, 2f);

        yield return null;
        Vector3 initialVelocity = body.linearVelocity;

        Press(mouse.rightButton);
        yield return null;

        Assert.That(grappling.IsGrappling(), Is.False);
        Assert.That(line.enabled, Is.False);
        Assert.That(hook.gameObject.activeSelf, Is.False);
        Assert.That(body.linearVelocity, Is.EqualTo(initialVelocity));

        Release(mouse.rightButton);
        yield return null;
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_021")]
    public IEnumerator TST_S4_021_VacioYObjetivoFueraDeAlcanceNoDejanEstadoResidual()
    {
        Grappling grappling = CreateGrapplingFixture(
            out Rigidbody body,
            out LineRenderer line,
            out Transform hook
        );
        GameObject distantTarget = CreateCube(
            "DistantTarget",
            new Vector3(0f, 0f, 30f),
            new Vector3(2f, 2f, 1f)
        );
        distantTarget.layer = 8;
        grappling.whatIsGrappleable = 1 << distantTarget.layer;
        grappling.maxGrappleDistance = 20f;

        yield return null;
        Vector3 initialPosition = body.position;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
        }

        Assert.That(grappling.IsGrappling(), Is.False);
        Assert.That(line.enabled, Is.False);
        Assert.That(hook.gameObject.activeSelf, Is.False);
        Assert.That(Vector3.Distance(body.position, initialPosition), Is.LessThan(0.01f));
        AssertVectorIsFinite(body.linearVelocity, "velocidad tras intentos inválidos");
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_022")]
    public IEnumerator TST_S4_022_UnIntentoInvalidoNoImpideElSiguienteGanchoValido()
    {
        Grappling grappling = CreateGrapplingFixture(
            out _,
            out LineRenderer line,
            out Transform hook
        );
        CreateCube("IgnoredTarget", new Vector3(0f, 0f, 4f), new Vector3(2f, 2f, 1f));
        GameObject validTarget = CreateCube(
            "ValidTarget",
            new Vector3(0f, 0f, 8f),
            new Vector3(2f, 2f, 1f)
        );
        validTarget.layer = 8;
        grappling.whatIsGrappleable = 1 << validTarget.layer;

        yield return null;

        grappling.whatIsGrappleable = 1 << 9;
        Press(mouse.rightButton);
        yield return null;
        Release(mouse.rightButton);
        yield return null;
        Assert.That(grappling.IsGrappling(), Is.False);

        grappling.whatIsGrappleable = 1 << validTarget.layer;
        Press(mouse.rightButton);
        yield return null;

        Assert.That(grappling.IsGrappling(), Is.True);
        Assert.That(line.enabled, Is.True);
        Assert.That(hook.gameObject.activeSelf, Is.True);

        Release(mouse.rightButton);
        yield return null;
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_023")]
    public IEnumerator TST_S4_023_CorrerYSaltarConservaLaCarreraEnElAire()
    {
        CreateGround();
        global::Move move = CreateMovePlayer(out _);
        yield return WaitUntilGrounded(move);

        Press(CurrentKeyboard().wKey);
        Press(CurrentKeyboard().leftShiftKey);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.True);

        Press(CurrentKeyboard().spaceKey);
        yield return null;
        Release(CurrentKeyboard().spaceKey);
        yield return WaitUntilAirborne(move);

        Assert.That(GetPrivateField<bool>(move, "running"), Is.True);

        Release(CurrentKeyboard().leftShiftKey);
        Release(CurrentKeyboard().wKey);
        yield return null;
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_024")]
    public IEnumerator TST_S4_024_PresionarCorrerEnElAireNoActivaLaCarrera()
    {
        CreateGround();
        global::Move move = CreateMovePlayer(out _);
        yield return WaitUntilGrounded(move);

        Press(CurrentKeyboard().wKey);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.False);

        Press(CurrentKeyboard().spaceKey);
        yield return null;
        Release(CurrentKeyboard().spaceKey);
        yield return WaitUntilAirborne(move);

        Press(CurrentKeyboard().leftShiftKey);
        yield return null;

        Assert.That(GetPrivateField<bool>(move, "running"), Is.False);

        Release(CurrentKeyboard().leftShiftKey);
        Release(CurrentKeyboard().wKey);
        yield return null;
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_025")]
    public IEnumerator TST_S4_025_SoltarYReintentarCorrerEnElAireEsSeguro()
    {
        CreateGround();
        global::Move move = CreateMovePlayer(out _);
        yield return WaitUntilGrounded(move);

        Press(CurrentKeyboard().wKey);
        Press(CurrentKeyboard().leftShiftKey);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.True);

        Press(CurrentKeyboard().spaceKey);
        yield return null;
        Release(CurrentKeyboard().spaceKey);
        yield return WaitUntilAirborne(move);

        Release(CurrentKeyboard().leftShiftKey);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.False);

        Press(CurrentKeyboard().leftShiftKey);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.False);

        yield return WaitUntilGrounded(move, 240);
        yield return null;
        Assert.That(GetPrivateField<bool>(move, "running"), Is.True);

        Release(CurrentKeyboard().leftShiftKey);
        Release(CurrentKeyboard().wKey);
        yield return null;
    }

    [Test]
    [Category("Sprint4Automated")]
    [Category("TST_S4_026")]
    public void TST_S4_026_LaVelocidadNoAtraviesaParedesNiEsquinas()
    {
        Vector3 requestedVelocity = new Vector3(3f, 0f, 4f);

        Vector3 againstWall = global::Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            new[] { Vector3.left }
        );
        Vector3 againstCorner = global::Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            new[] { Vector3.left, Vector3.back }
        );

        Assert.That(Vector3.Dot(againstWall, Vector3.left), Is.GreaterThanOrEqualTo(-0.0001f));
        Assert.That(againstWall.z, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(againstCorner, Is.EqualTo(Vector3.zero));
    }

    [UnityTest]
    [Category("Sprint4Automated")]
    [Category("TST_S4_028")]
    public IEnumerator TST_S4_028_SaltoGravedadYAterrizajeSiguenFuncionando()
    {
        CreateGround();
        global::Move move = CreateMovePlayer(out Rigidbody body);
        yield return WaitUntilGrounded(move);

        float initialHeight = body.position.y;
        Press(CurrentKeyboard().spaceKey);
        yield return null;
        Release(CurrentKeyboard().spaceKey);
        yield return WaitUntilAirborne(move);

        bool rose = false;

        for (int i = 0; i < 60; i++)
        {
            yield return new WaitForFixedUpdate();

            if (body.position.y > initialHeight + 0.1f)
                rose = true;

            if (rose && move.IsGrounded() && Mathf.Abs(body.linearVelocity.y) < 0.2f)
                break;
        }

        Assert.That(rose, Is.True, "El salto no elevó al jugador.");
        Assert.That(move.IsGrounded(), Is.True, "El jugador no volvió a aterrizar.");
        AssertVectorIsFinite(body.position, "posición después del aterrizaje");
        AssertVectorIsFinite(body.linearVelocity, "velocidad después del aterrizaje");

        Vector3 horizontalVelocity = new Vector3(2f, 0f, 3f);
        Assert.That(
            global::Move.ProjectVelocityAlongWalls(horizontalVelocity, new[] { Vector3.up }),
            Is.EqualTo(horizontalVelocity),
            "Una normal de suelo no debe alterar el movimiento horizontal."
        );
    }

    private Grappling CreateGrapplingFixture(
        out Rigidbody body,
        out LineRenderer line,
        out Transform hook)
    {
        GameObject player = CreateGameObject("Sprint4GrapplingPlayer", Vector3.zero);
        body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.freezeRotation = true;

        GameObject cameraObject = CreateGameObject("Sprint4GrapplingCamera", Vector3.zero);
        cameraObject.transform.SetParent(player.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
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

    private global::Move CreateMovePlayer(out Rigidbody body)
    {
        GameObject player = CreateGameObject("Sprint4MovePlayer", Vector3.up);
        player.AddComponent<CapsuleCollider>();

        body = player.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.None;

        Transform orientation = CreateChild(player.transform, "Orientation", Vector3.zero);
        Transform groundCheck = CreateChild(
            player.transform,
            "GroundCheck",
            Vector3.down * 0.95f
        );

        global::Move move = player.AddComponent<global::Move>();
        move.orientation = orientation;
        move.groundCheck = groundCheck;
        move.groundDistance = 0.2f;
        move.groundMask = 1 << 0;
        move.speed = 6f;
        move.runSpeed = 10f;
        move.acceleration = 30f;
        move.jumpForce = 7f;

        Physics.SyncTransforms();
        return move;
    }

    private GameObject CreateGround()
    {
        return CreateCube(
            "Sprint4Ground",
            new Vector3(0f, -0.5f, 0f),
            new Vector3(100f, 1f, 100f)
        );
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

    private static Transform CreateChild(
        Transform parent,
        string name,
        Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private static IEnumerator WaitUntilGrounded(global::Move move, int maximumSteps = 60)
    {
        for (int i = 0; i < maximumSteps; i++)
        {
            if (move.IsGrounded())
                yield break;

            yield return new WaitForFixedUpdate();
        }

        Assert.Fail("El jugador no alcanzó el suelo dentro del tiempo esperado.");
    }

    private static IEnumerator WaitUntilAirborne(global::Move move, int maximumSteps = 30)
    {
        for (int i = 0; i < maximumSteps; i++)
        {
            if (!move.IsGrounded())
                yield break;

            yield return new WaitForFixedUpdate();
        }

        Assert.Fail("El jugador no despegó dentro del tiempo esperado.");
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null, $"No se encontró el campo {fieldName}.");
        return (T)field.GetValue(target);
    }

    private Keyboard CurrentKeyboard()
    {
        Keyboard current = Keyboard.current;

        Assert.That(current, Is.Not.Null, "No hay un teclado de prueba activo.");
        Assert.That(current.added, Is.True, "El teclado de prueba no está registrado.");
        keyboard = current;
        return keyboard;
    }

    private static void AssertVectorIsFinite(Vector3 value, string label)
    {
        Assert.That(float.IsFinite(value.x), Is.True, $"{label}.x no es finito.");
        Assert.That(float.IsFinite(value.y), Is.True, $"{label}.y no es finito.");
        Assert.That(float.IsFinite(value.z), Is.True, $"{label}.z no es finito.");
    }
}
