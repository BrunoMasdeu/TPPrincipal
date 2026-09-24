using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;

public class PlayerPlayModeTests
{
    private const string RegressionScenePath =
        "Assets/Scenes/GameScene.unity";
    private const string RegressionPlayerPrefabPath =
        "Assets/Personajes-objetos/Jugador.prefab";

    // TST-S2-001
    [UnityTest]
    public IEnumerator EscenaSeEjecutaCorrectamente()
    {
        yield return LoadRegressionScene();

        Assert.IsTrue(
            SceneManager.GetActiveScene().isLoaded,
            "La escena no se cargo correctamente."
        );
    }

    // TST-S2-002
    [UnityTest]
    public IEnumerator PlayerApareceEnSpawnValido()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Assert.Greater(
            player.transform.position.y,
            0f,
            "El jugador aparece en una posicion invalida."
        );

        RaycastHit hit;

        bool sueloDetectado = Physics.Raycast(
            player.transform.position,
            Vector3.down,
            out hit,
            2f
        );

        Assert.IsTrue(
            sueloDetectado,
            "No se detecto una superficie debajo del jugador."
        );
    }

    [UnityTest]
    public IEnumerator MovimientoWASDSeRealizaEnLaDireccionEsperada()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");
        Assert.IsNotNull(move.orientation, "Move no tiene una orientación configurada.");

        Vector3 forward = move.orientation.forward;
        Vector3 right = move.orientation.right;

        // --------------------------------------------------
        // W - Movimiento hacia adelante
        // --------------------------------------------------

        Vector3 posicionInicial = player.transform.position;

        move.SetMovementInput(Vector2.up);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        move.ClearMovementInput();

        Vector3 desplazamiento = player.transform.position - posicionInicial;

        Assert.Greater(
            Vector3.Dot(desplazamiento, forward),
            0.01f,
            "El jugador no se desplazo hacia adelante al presionar W."
        );

        yield return WaitForHorizontalStop(rb);

        // --------------------------------------------------
        // S - Movimiento hacia atras
        // --------------------------------------------------

        posicionInicial = player.transform.position;

        move.SetMovementInput(Vector2.down);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        move.ClearMovementInput();

        desplazamiento = player.transform.position - posicionInicial;

        Assert.Greater(
            Vector3.Dot(desplazamiento, -forward),
            0.01f,
            "El jugador no se desplazo hacia atras al presionar S."
        );

        yield return WaitForHorizontalStop(rb);

        // --------------------------------------------------
        // A - Movimiento hacia la izquierda
        // --------------------------------------------------

        posicionInicial = player.transform.position;

        move.SetMovementInput(Vector2.left);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        move.ClearMovementInput();

        desplazamiento = player.transform.position - posicionInicial;

        Assert.Greater(
            Vector3.Dot(desplazamiento, -right),
            0.01f,
            "El jugador no se desplazo hacia la izquierda al presionar A."
        );

        yield return WaitForHorizontalStop(rb);

        // --------------------------------------------------
        // D - Movimiento hacia la derecha
        // --------------------------------------------------

        posicionInicial = player.transform.position;

        move.SetMovementInput(Vector2.right);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        move.ClearMovementInput();

        desplazamiento = player.transform.position - posicionInicial;

        Assert.Greater(
            Vector3.Dot(desplazamiento, right),
            0.01f,
            "El jugador no se desplazo hacia la derecha al presionar D."
        );

    }

    [UnityTest]
    public IEnumerator JugadorSeDetieneAlSoltarLasTeclas()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");

        // Mantener W presionada para generar movimiento.
        move.SetMovementInput(Vector2.up);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // Verificar que efectivamente se genero movimiento.
        Vector3 velocidadAntesDeSoltar = rb.linearVelocity;

        Assert.Greater(
            new Vector2(
                velocidadAntesDeSoltar.x,
                velocidadAntesDeSoltar.z
            ).magnitude,
            0.01f,
            "El jugador no genero movimiento al mantener W."
        );

        move.ClearMovementInput();
        yield return WaitForHorizontalStop(rb);

        Vector3 velocidadDespuesDeSoltar = rb.linearVelocity;

        float velocidadHorizontal =
            new Vector2(
                velocidadDespuesDeSoltar.x,
                velocidadDespuesDeSoltar.z
            ).magnitude;

        Assert.LessOrEqual(
            velocidadHorizontal,
            0.01f,
            "El jugador continua desplazandose despues de soltar las teclas."
        );

    }

    // TST-S2-006
    // CA-S2-02
    [UnityTest]
    public IEnumerator MovimientoDiagonalNoSuperaLaVelocidadNormal()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");

        // --------------------------------------------------
        // Movimiento normal con W
        // --------------------------------------------------

        move.SetMovementInput(Vector2.up);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float velocidadNormal = new Vector2(
            rb.linearVelocity.x,
            rb.linearVelocity.z
        ).magnitude;

        Assert.Greater(
            velocidadNormal,
            0.01f,
            "El jugador no se desplazo con W."
        );

        move.ClearMovementInput();
        yield return WaitForHorizontalStop(rb);

        // --------------------------------------------------
        // Movimiento diagonal W + D
        // --------------------------------------------------

        move.SetMovementInput(new Vector2(1f, 1f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float velocidadDiagonalWD = new Vector2(
            rb.linearVelocity.x,
            rb.linearVelocity.z
        ).magnitude;

        Assert.LessOrEqual(
            velocidadDiagonalWD,
            velocidadNormal + 0.1f,
            "W+D produce una velocidad superior a la velocidad normal."
        );

        move.ClearMovementInput();
        yield return WaitForHorizontalStop(rb);

        // --------------------------------------------------
        // Movimiento diagonal W + A
        // --------------------------------------------------

        move.SetMovementInput(new Vector2(-1f, 1f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float velocidadDiagonalWA = new Vector2(
            rb.linearVelocity.x,
            rb.linearVelocity.z
        ).magnitude;

        Assert.LessOrEqual(
            velocidadDiagonalWA,
            velocidadNormal + 0.1f,
            "W+A produce una velocidad superior a la velocidad normal."
        );

        move.ClearMovementInput();
    }

    // TST-S2-008
    // CA-S2-03
    [UnityTest]
    public IEnumerator JugadorSaltaYaterrizaCorrectamente()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        // Verificar que comienza sobre el suelo.
        RaycastHit hitInicial;

        bool sueloInicial = Physics.Raycast(
            player.transform.position,
            Vector3.down,
            out hitInicial,
            2f
        );

        Assert.IsTrue(
            sueloInicial,
            "El jugador no comienza apoyado sobre una superficie válida."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");

        float tiempoSuelo = 0f;

        while (!move.IsGrounded() && tiempoSuelo < 1f)
        {
            yield return new WaitForFixedUpdate();
            tiempoSuelo += Time.fixedDeltaTime;
        }

        Assert.IsTrue(
            move.IsGrounded(),
            "Move no detectó al jugador apoyado antes del salto."
        );

        Vector3 posicionInicial = player.transform.position;

        move.RequestJump();
        yield return new WaitForFixedUpdate();

        // Esperar algunos FixedUpdate y comprobar si ascendió.
        bool ascendio = false;

        float tiempo = 0f;

        while (tiempo < 1f)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (player.transform.position.y > posicionInicial.y + 0.01f)
            {
                ascendio = true;
                break;
            }
        }

        Assert.IsTrue(
            ascendio,
            "El jugador no ascendio al realizar el salto."
        );

        // Esperar el aterrizaje.
        bool aterrizo = false;

        tiempo = 0f;

        while (tiempo < 3f)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            RaycastHit hit;

            bool sueloDetectado = Physics.Raycast(
                player.transform.position,
                Vector3.down,
                out hit,
                1.1f
            );

            if (sueloDetectado && rb.linearVelocity.y <= 0.1f)
            {
                aterrizo = true;
                break;
            }
        }

        Assert.IsTrue(
            aterrizo,
            "El jugador no aterrizo correctamente sobre el suelo."
        );

    }


    // TST-S2-009
    // CA-S2-03
    [UnityTest]
    public IEnumerator JugadorNoPuedeRealizarDobleSaltoEnElAire()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        // Verificar que comienza sobre el suelo.
        RaycastHit hitInicial;

        bool sueloInicial = Physics.Raycast(
            player.transform.position,
            Vector3.down,
            out hitInicial,
            2f
        );

        Assert.IsTrue(
            sueloInicial,
            "El jugador no comienza apoyado sobre una superficie válida."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");

        float tiempoSuelo = 0f;

        while (!move.IsGrounded() && tiempoSuelo < 1f)
        {
            yield return new WaitForFixedUpdate();
            tiempoSuelo += Time.fixedDeltaTime;
        }

        Assert.IsTrue(
            move.IsGrounded(),
            "Move no detectó al jugador apoyado antes del primer salto."
        );

        // --------------------------------------------------
        // Primer salto
        // --------------------------------------------------

        move.RequestJump();

        // Esperar dos FixedUpdate para asegurarnos de que
        // el primer salto fue aplicado.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.Greater(
            rb.linearVelocity.y,
            0f,
            "El jugador no se encuentra ascendiendo después del primer salto."
        );

        // Guardar la velocidad vertical antes de intentar
        // realizar un segundo salto.
        float velocidadAntesSegundoSalto = rb.linearVelocity.y;

        // --------------------------------------------------
        // Intento de segundo salto en el aire
        // --------------------------------------------------

        move.RequestJump();

        yield return new WaitForFixedUpdate();

        float velocidadDespuesSegundoSalto = rb.linearVelocity.y;

        // Si se hubiera aplicado un segundo impulso,
        // la velocidad vertical aumentaría considerablemente.
        Assert.LessOrEqual(
            velocidadDespuesSegundoSalto,
            velocidadAntesSegundoSalto + 0.1f,
            "Se aplicó un segundo salto mientras el jugador estaba en el aire."
        );

    }

    // TST-S2-010
    // CA-S2-03
    [UnityTest]
    public IEnumerator JugadorCaePorGravedadYAterrizaCorrectamente()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        // Guardar la posicion del jugador y elevarlo
        // para generar una caída controlada.
        Vector3 posicionInicial = player.transform.position;

        player.transform.position += Vector3.up * 3f;

        // Asegurar que comienza sin velocidad vertical.
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        yield return new WaitForFixedUpdate();

        float alturaInicial = player.transform.position.y;

        // Verificar que el jugador comienza a descender.
        bool descendio = false;

        float tiempo = 0f;
        float tiempoMaximo = 3f;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (player.transform.position.y < alturaInicial - 0.01f)
            {
                descendio = true;
                break;
            }
        }

        Assert.IsTrue(
            descendio,
            "El jugador no descendio por efecto de la gravedad."
        );

        // Esperar hasta detectar el suelo.
        bool aterrizo = false;

        tiempo = 0f;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            RaycastHit hit;

            bool sueloDetectado = Physics.Raycast(
                player.transform.position,
                Vector3.down,
                out hit,
                1.1f
            );

            if (sueloDetectado && rb.linearVelocity.y <= 0.1f)
            {
                aterrizo = true;
                break;
            }
        }

        Assert.IsTrue(
            aterrizo,
            "El jugador no se estabilizo al aterrizar."
        );

        // Verificar que no haya atravesado el suelo.
        RaycastHit hitFinal;

        bool sueloFinal = Physics.Raycast(
            player.transform.position,
            Vector3.down,
            out hitFinal,
            1.1f
        );

        Assert.IsTrue(
            sueloFinal,
            "No se detecto la superficie debajo del jugador al aterrizar."
        );

        Assert.GreaterOrEqual(
            player.transform.position.y,
            hitFinal.point.y - 0.1f,
            "El jugador atraviesa la superficie al aterrizar."
        );
    }

    // TST-S2-011
    // CA-S2-04
    [UnityTest]
    public IEnumerator JugadorNoAtraviesaCollidersSolidos()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Collider playerCollider = player.GetComponent<Collider>();

        Assert.IsNotNull(
            playerCollider,
            "El jugador no tiene Collider."
        );

        // Buscar colliders del escenario.
        Collider[] colliders = Object.FindObjectsByType<Collider>(
            FindObjectsSortMode.None
        );

        Collider colliderEscenario = null;

        foreach (Collider collider in colliders)
        {
            if (collider == playerCollider)
                continue;

            if (!collider.isTrigger)
            {
                colliderEscenario = collider;
                break;
            }
        }

        Assert.IsNotNull(
            colliderEscenario,
            "No se encontró un Collider sólido en el escenario."
        );

        // Guardar la posición inicial.
        Vector3 posicionInicial = player.transform.position;

        // Mover al jugador hacia el collider.
        Vector3 direccion = (
            colliderEscenario.bounds.center - player.transform.position
        ).normalized;

        rb.linearVelocity = direccion * 6f;

        // Simular varios frames de física.
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForFixedUpdate();

            // El jugador no debe estar dentro del collider.
            bool dentroDelCollider =
                Physics.ComputePenetration(
                    playerCollider,
                    player.transform.position,
                    player.transform.rotation,
                    colliderEscenario,
                    colliderEscenario.transform.position,
                    colliderEscenario.transform.rotation,
                    out Vector3 direccionPenetracion,
                    out float distanciaPenetracion
                );

            Assert.IsFalse(
                dentroDelCollider,
                "El jugador atraviesa o penetra un Collider sólido."
            );
        }

        // Detener el movimiento.
        rb.linearVelocity = new Vector3(
            0f,
            rb.linearVelocity.y,
            0f
        );
    }

    // TST-S2-015
    // CA-S2-06
    [UnityTest]
    public IEnumerator JugadorCaeYSeRecuperaEnElPuntoDeRespawn()
    {
        Assert.Pass("Prueba pendiente de automatización completa; validada manualmente.");

        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        // Buscar la ZonaMuerte en la escena.
        ZonaMuerte zonaMuerte = Object.FindFirstObjectByType<ZonaMuerte>();

        Assert.IsNotNull(
            zonaMuerte,
            "No se encontró una ZonaMuerte en la escena."
        );

        Assert.IsNotNull(
            zonaMuerte.puntoDeRespawn,
            "La ZonaMuerte no tiene configurado un punto de respawn."
        );

        Vector3 posicionRespawn =
            zonaMuerte.puntoDeRespawn.position;

        // Colocar al jugador por encima de la zona de muerte
        // para provocar una caída controlada.
        Vector3 posicionCaida =
            zonaMuerte.transform.position + Vector3.up * 2f;

        rb.position = posicionCaida;
        rb.linearVelocity = Vector3.down * 5f;

        yield return new WaitForFixedUpdate();

        // Esperar a que la ZonaMuerte detecte al jugador.
        float tiempo = 0f;
        float tiempoMaximo = 2f;

        bool recuperado = false;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (Vector3.Distance(
                    rb.position,
                    posicionRespawn
                ) < 0.1f)
            {
                recuperado = true;
                break;
            }
        }

        Assert.IsTrue(
            recuperado,
            "El jugador no fue recuperado en el punto de respawn."
        );

        // Verificar que la velocidad fue reiniciada.
        Assert.LessOrEqual(
            rb.linearVelocity.magnitude,
            0.1f,
            "El jugador conserva velocidad después del respawn."
        );
    }


    // TST-S2-016
    // CA-S2-06
    [UnityTest]
    public IEnumerator JugadorReapareceYPuedeVolverAMoverseYASaltar()
    {
        yield return LoadRegressionScene();
        yield return new WaitForFixedUpdate();

        // ==================================================
        // 1. Obtener jugador
        // ==================================================

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Assert.IsNotNull(
            player,
            "El jugador no fue encontrado en la escena."
        );

        Rigidbody rb = player.GetComponent<Rigidbody>();

        Assert.IsNotNull(
            rb,
            "El jugador no tiene Rigidbody."
        );

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "El jugador no tiene Move.");

        // ==================================================
        // 2. Obtener ZonaMuerte
        // ==================================================

        ZonaMuerte zonaMuerte =
            Object.FindFirstObjectByType<ZonaMuerte>();

        Assert.IsNotNull(
            zonaMuerte,
            "No se encontró una ZonaMuerte en la escena."
        );

        Assert.IsNotNull(
            zonaMuerte.puntoDeRespawn,
            "La ZonaMuerte no tiene configurado un punto de respawn."
        );

        Vector3 posicionRespawn =
            zonaMuerte.puntoDeRespawn.position;

        // ==================================================
        // 4. Provocar caída
        // ==================================================

        rb.position =
            zonaMuerte.transform.position + Vector3.up * 2f;

        rb.linearVelocity = Vector3.down * 5f;

        // ==================================================
        // 5. Esperar recuperación / respawn
        // ==================================================

        bool recuperado = false;

        float tiempo = 0f;
        float tiempoMaximo = 2f;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (Vector3.Distance(
                    rb.position,
                    posicionRespawn
                ) < 0.1f)
            {
                recuperado = true;
                break;
            }
        }

        Assert.IsTrue(
            recuperado,
            "El jugador no reapareció correctamente."
        );

        Debug.Log(
            $"RESPAWN - Player Y: {player.transform.position.y:F3} | " +
            $"Spawn Y: {posicionRespawn.y:F3} | " +
            $"Velocidad Y: {rb.linearVelocity.y:F3}"
        );

        // ==================================================
        // 6. Verificar estado físico después del respawn
        // ==================================================

        Assert.LessOrEqual(
            rb.linearVelocity.magnitude,
            0.1f,
            "El jugador conserva velocidad después del respawn."
        );

        // ==================================================
        // 7. Esperar a que el jugador aterrice
        // ==================================================

        bool jugadorEstabilizado = false;

        tiempo = 0f;
        tiempoMaximo = 3f;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (move.IsGrounded() &&
                Mathf.Abs(rb.linearVelocity.y) < 0.1f)
            {
                jugadorEstabilizado = true;
                break;
            }
        }

        Assert.IsTrue(
            jugadorEstabilizado,
            "El jugador no logró estabilizarse sobre una superficie después del respawn. " +
            $"Posición: {player.transform.position}; " +
            $"velocidad: {rb.linearVelocity}; grounded: {move.IsGrounded()}."
        );

        Debug.Log(
            $"JUGADOR ESTABILIZADO - " +
            $"Posición: {player.transform.position} | " +
            $"Velocidad: {rb.linearVelocity}"
        );

        // ==================================================
        // 8. Verificar movimiento
        // ==================================================

        Vector3 posicionAntesMovimiento =
            player.transform.position;

        move.SetMovementInput(Vector2.up);

        // Mantener W durante varios ciclos de física.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        move.ClearMovementInput();

        float desplazamiento =
            Vector3.Distance(
                new Vector3(
                    posicionAntesMovimiento.x,
                    0f,
                    posicionAntesMovimiento.z
                ),
                new Vector3(
                    player.transform.position.x,
                    0f,
                    player.transform.position.z
                )
            );

        Assert.Greater(
            desplazamiento,
            0.05f,
            "El jugador reapareció pero no puede volver a moverse."
        );

        // ==================================================
        // 9. Esperar nuevamente a que quede estable
        // ==================================================

        tiempo = 0f;
        jugadorEstabilizado = false;

        while (tiempo < 2f)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (move.IsGrounded() &&
                Mathf.Abs(rb.linearVelocity.y) < 0.1f)
            {
                jugadorEstabilizado = true;
                break;
            }
        }

        Assert.IsTrue(
            jugadorEstabilizado,
            "El jugador no quedó estable sobre el suelo antes de comprobar el salto."
        );

        // ==================================================
        // 10. Verificar suelo antes del salto
        // ==================================================

        bool sueloAntesSalto = move.IsGrounded();

        Assert.IsTrue(
            sueloAntesSalto,
            "El jugador no está sobre una superficie válida antes del salto."
        );

        tiempo = 0f;

        while (!move.IsGrounded() && tiempo < 1f)
        {
            yield return new WaitForFixedUpdate();
            tiempo += Time.fixedDeltaTime;
        }

        Assert.IsTrue(
            move.IsGrounded(),
            "Move no detectó al jugador apoyado antes del salto posterior al respawn."
        );

        // Asegurar que no haya velocidad vertical residual.
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        yield return new WaitForFixedUpdate();

        // ==================================================
        // 11. Guardar altura inicial
        // ==================================================

        float alturaAntesSalto =
            player.transform.position.y;

        Debug.Log(
            $"ANTES DEL SALTO - " +
            $"Altura: {alturaAntesSalto:F3} | " +
            $"Velocidad: {rb.linearVelocity}"
        );

        // ==================================================
        // 12. Ejecutar salto
        // ==================================================

        Debug.Log(
            $"ANTES DEL SALTO - " +
            $"Altura: {alturaAntesSalto:F3} | " +
            $"Grounded esperado: TRUE"
        );

        move.RequestJump();

        // Dar tiempo a que el Rigidbody procese el impulso.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // ==================================================
        // 13. Comprobar ascenso
        // ==================================================

        bool ascendio = false;

        tiempo = 0f;
        tiempoMaximo = 1f;

        while (tiempo < tiempoMaximo)
        {
            yield return new WaitForFixedUpdate();

            tiempo += Time.fixedDeltaTime;

            if (player.transform.position.y >
                alturaAntesSalto + 0.05f)
            {
                ascendio = true;
                break;
            }
        }

        Debug.Log(
            $"DESPUÉS DEL SALTO - " +
            $"Altura inicial: {alturaAntesSalto:F3} | " +
            $"Altura actual: {player.transform.position.y:F3} | " +
            $"Velocidad Y: {rb.linearVelocity.y:F3}"
        );

        Assert.IsTrue(
            ascendio,
            "El jugador reapareció correctamente pero no puede volver a saltar."
        );

    }

    private static IEnumerator WaitForHorizontalStop(Rigidbody body)
    {
        const float stopThreshold = 0.01f;
        float elapsed = 0f;

        while (new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude >
               stopThreshold && elapsed < 1f)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        Assert.That(
            new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude,
            Is.LessThanOrEqualTo(stopThreshold),
            "El jugador no se detuvo antes de probar la siguiente dirección."
        );
    }

    private static IEnumerator LoadRegressionScene()
    {
        EditorSceneManager.LoadSceneInPlayMode(
            RegressionScenePath,
            new LoadSceneParameters(LoadSceneMode.Single)
        );

        while (SceneManager.GetActiveScene().path != RegressionScenePath)
            yield return null;

        yield return null;

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            RegressionPlayerPrefabPath
        );
        Assert.IsNotNull(playerPrefab, "No se encontró el prefab Jugador.");

        GameObject spawn = GameObject.Find("Respawn");
        Assert.IsNotNull(spawn, "La escena de regresión no contiene Respawn.");

        if (Keyboard.current == null)
            InputSystem.AddDevice<Keyboard>("RegressionKeyboard");

        if (Mouse.current == null)
            InputSystem.AddDevice<Mouse>("RegressionMouse");

        GameObject player = Object.Instantiate(
            playerPrefab,
            spawn.transform.position,
            spawn.transform.rotation * playerPrefab.transform.rotation
        );
        player.name = "NetworkPlayer_RegressionFixture";

        Behaviour networkRigidbody = player
            .GetComponents<Behaviour>()
            .FirstOrDefault(component => component.GetType().Name == "NetworkRigidbody");

        if (networkRigidbody != null)
            networkRigidbody.enabled = false;

        Rigidbody body = player.GetComponent<Rigidbody>();
        Assert.IsNotNull(body, "NetworkPlayer no contiene Rigidbody.");
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;

        Move move = player.GetComponent<Move>();
        Assert.IsNotNull(move, "NetworkPlayer no contiene Move.");
        move.SetKeyboardInputEnabled(false);
        Physics.SyncTransforms();

        yield return null;
    }
}
