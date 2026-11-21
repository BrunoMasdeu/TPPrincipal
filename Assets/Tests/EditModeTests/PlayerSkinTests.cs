using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Un solo PlayerPrefab con un skin por equipo (Soldier y Survivalist).
public class PlayerSkinTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string SessionRootPath = "Assets/Prefabs/NetworkSessionRoot.prefab";

    private static PlayerSkin.Skin Skin(TeamId equipo) => new() { equipo = equipo };

    [TestCase(16f / 9f)]
    [TestCase(16f / 10f)]
    [TestCase(21f / 9f)]
    public void OjosYPlanoCercanoQuedanDentroDelColliderAlMirarEnCualquierDireccion(float aspect)
    {
        GameObject prefab = Prefab();
        CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
        Camera camera = prefab.transform.Find("MainCamera").GetComponent<Camera>();
        CameraMovement movement = camera.GetComponent<CameraMovement>();
        Assert.That(camera.nearClipPlane, Is.EqualTo(movement.nearClipPlane));

        SerializedProperty skins = new SerializedObject(prefab.GetComponent<PlayerSkin>())
            .FindProperty("skins");
        float scale = prefab.transform.lossyScale.x;
        float radius = capsule.radius * scale;
        float halfSegment = (capsule.height * 0.5f - capsule.radius) * scale;
        Vector3 center = prefab.transform.TransformPoint(capsule.center);
        Vector3 bottom = center - prefab.transform.up * halfSegment;
        Vector3 top = center + prefab.transform.up * halfSegment;
        float near = camera.nearClipPlane;
        float halfHeight = near * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

        for (int skin = 0; skin < skins.arraySize; skin++)
        {
            Vector3 eye = prefab.transform.TransformPoint(skins.GetArrayElementAtIndex(skin)
                .FindPropertyRelative("posicionCamara").vector3Value);
            for (int pitch = -90; pitch <= 90; pitch += 5)
            for (int yaw = 0; yaw < 360; yaw += 10)
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 corner = eye + prefab.transform.rotation * Quaternion.Euler(pitch, yaw, 0f) *
                    new Vector3(x * halfHeight * aspect, y * halfHeight, near);
                Vector3 segment = top - bottom;
                float t = Mathf.Clamp01(Vector3.Dot(corner - bottom, segment) / segment.sqrMagnitude);
                float distance = Vector3.Distance(corner, bottom + segment * t);
                Assert.That(distance, Is.LessThan(radius),
                    $"Skin {skin}, pitch {pitch}, yaw {yaw}: la cámara puede ver a través de una pared.");
            }
        }
    }

    // ---- Elegir el skin ----

    [Test]
    public void CadaEquipoUsaSuSkin()
    {
        PlayerSkin.Skin rojo = Skin(TeamId.Red);
        PlayerSkin.Skin azul = Skin(TeamId.Blue);
        PlayerSkin.Skin[] lista = { rojo, azul };

        Assert.That(PlayerSkin.BuscarSkin(lista, TeamId.Red), Is.SameAs(rojo));
        Assert.That(PlayerSkin.BuscarSkin(lista, TeamId.Blue), Is.SameAs(azul));
    }

    [Test]
    public void SinEquipoSeUsaElPrimero()
    {
        PlayerSkin.Skin rojo = Skin(TeamId.Red);
        PlayerSkin.Skin[] lista = { rojo, Skin(TeamId.Blue) };

        Assert.That(PlayerSkin.BuscarSkin(lista, TeamId.None), Is.SameAs(rojo));
    }

    [Test]
    public void SinSkinsNoDevuelveNada()
    {
        Assert.That(PlayerSkin.BuscarSkin(null, TeamId.Red), Is.Null);
        Assert.That(PlayerSkin.BuscarSkin(new PlayerSkin.Skin[0], TeamId.Red), Is.Null);
    }

    // ---- Cómo quedó armado el prefab ----

    [Test]
    public void PlayerSkinVaAntesQuePlayerAnimationController()
    {
        // Cambiar el Avatar reinicia el Animator: tiene que pasar antes de que
        // PlayerAnimationController prepare sus capas al aparecer el jugador.
        Component[] componentes = Prefab().GetComponents<Component>();
        int skin = System.Array.FindIndex(componentes, c => c is PlayerSkin);
        int animacion = System.Array.FindIndex(
            componentes, c => c is PlayerAnimationController);

        Assert.That(skin, Is.GreaterThanOrEqualTo(0), "Falta PlayerSkin.");
        Assert.That(animacion, Is.GreaterThanOrEqualTo(0), "Falta PlayerAnimationController.");
        Assert.That(skin, Is.LessThan(animacion));
    }

    [Test]
    public void HayUnSkinParaCadaEquipoConSuModeloYAvatar()
    {
        GameObject prefab = Prefab();
        SerializedObject skin = new(prefab.GetComponent<PlayerSkin>());

        Assert.That(skin.FindProperty("animator").objectReferenceValue, Is.Not.Null);
        Assert.That(skin.FindProperty("armas").objectReferenceValue, Is.Not.Null);
        Assert.That(skin.FindProperty("camara").objectReferenceValue, Is.Not.Null);

        SerializedProperty skins = skin.FindProperty("skins");
        Assert.That(skins.arraySize, Is.EqualTo(3));

        TeamId[] equipos = new TeamId[skins.arraySize];
        for (int i = 0; i < skins.arraySize; i++)
        {
            SerializedProperty s = skins.GetArrayElementAtIndex(i);
            equipos[i] = (TeamId)s.FindPropertyRelative("equipo").enumValueIndex;

            GameObject modelo = s.FindPropertyRelative("modelo").objectReferenceValue as GameObject;
            Avatar avatar = s.FindPropertyRelative("avatar").objectReferenceValue as Avatar;

            Assert.That(modelo, Is.Not.Null, $"Skin {equipos[i]} sin modelo.");
            Assert.That(modelo.transform.IsChildOf(prefab.transform), Is.True,
                $"El modelo del skin {equipos[i]} tiene que estar dentro del prefab.");
            Assert.That(avatar, Is.Not.Null, $"Skin {equipos[i]} sin Avatar.");
            Assert.That(avatar.isValid && avatar.isHuman, Is.True,
                $"El Avatar del skin {equipos[i]} tiene que ser Humanoid.");
        }

        // El primero es el de "sin equipo" (tutorial, zombies, carrera).
        Assert.That(equipos, Is.EqualTo(new[] { TeamId.None, TeamId.Red, TeamId.Blue }));
    }

    // [ANIMACIONES] Original, rojo y azul usan el mismo soldado; cambia la ropa.
    [Test]
    public void LosTresSkinsUsanElMismoSoldadoYCadaEquipoSuRopa()
    {
        SerializedProperty skins =
            new SerializedObject(Prefab().GetComponent<PlayerSkin>()).FindProperty("skins");
        SerializedProperty original = skins.GetArrayElementAtIndex(0);

        Assert.That(Modelo(skins, 0).activeSelf, Is.True);
        Assert.That(original.FindPropertyRelative("materialesRopa").arraySize, Is.Zero,
            "El skin original (tutorial, zombies) no se tiñe.");

        for (int equipo = 1; equipo <= 2; equipo++)
        {
            SerializedProperty skin = skins.GetArrayElementAtIndex(equipo);
            Assert.That(Modelo(skins, equipo), Is.SameAs(Modelo(skins, 0)));

            foreach (string campo in new[] { "posicionArmas", "escalaArmas", "posicionCamara" })
                Assert.That(skin.FindPropertyRelative(campo).vector3Value,
                    Is.EqualTo(original.FindPropertyRelative(campo).vector3Value), campo);
            Assert.That(skin.FindPropertyRelative("cabeza").arraySize,
                Is.EqualTo(original.FindPropertyRelative("cabeza").arraySize));

            SerializedProperty ropa = skin.FindPropertyRelative("materialesRopa");
            SerializedProperty tenidos = skin.FindPropertyRelative("materialesTenidos");
            Assert.That(ropa.arraySize, Is.GreaterThan(0));
            Assert.That(tenidos.arraySize, Is.EqualTo(ropa.arraySize));
            for (int i = 0; i < ropa.arraySize; i++)
            {
                Assert.That(ropa.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
                Assert.That(tenidos.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null,
                    "Falta el material del equipo: correr Tools > Player > " +
                    "Usar el Soldado en los Dos Equipos.");
            }
        }

        // Cada equipo tiene sus propios materiales, para poder cambiarles la textura.
        Assert.That(
            skins.GetArrayElementAtIndex(1).FindPropertyRelative("materialesTenidos")
                .GetArrayElementAtIndex(0).objectReferenceValue,
            Is.Not.SameAs(
                skins.GetArrayElementAtIndex(2).FindPropertyRelative("materialesTenidos")
                    .GetArrayElementAtIndex(0).objectReferenceValue));
    }

    [Test]
    public void CadaModeloTieneLaManoDerechaDondeVanLasArmas()
    {
        // Mira el Avatar de cada skin: el hueso que mapea a la mano derecha
        // tiene que existir dentro de su modelo, así el Animator lo encuentra
        // y PlayerSkin puede llevar las armas ahí.
        SerializedProperty skins =
            new SerializedObject(Prefab().GetComponent<PlayerSkin>()).FindProperty("skins");

        for (int i = 0; i < skins.arraySize; i++)
        {
            GameObject modelo = Modelo(skins, i);
            Avatar avatar = skins.GetArrayElementAtIndex(i)
                .FindPropertyRelative("avatar").objectReferenceValue as Avatar;

            HumanBone mano = avatar.humanDescription.human
                .FirstOrDefault(h => h.humanName == "RightHand");
            Assert.That(mano.boneName, Is.Not.Null.And.Not.Empty,
                $"El Avatar de {modelo.name} no mapea la mano derecha.");

            bool existe = modelo.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name == mano.boneName);
            Assert.That(existe, Is.True,
                $"{modelo.name} no tiene el hueso '{mano.boneName}'.");
        }
    }

    [Test]
    public void EnElPrefabNingunaMallaEstaEnLaCapaDeLaCabezaPropia()
    {
        // La capa "Head" no la dibuja la cámara del jugador. Si una malla
        // queda ahí en el prefab, nadie la ve: ni el dueño ni los rivales.
        // PlayerSkin la pasa a esa capa solo en la PC del dueño.
        int capa = LayerMask.NameToLayer("Head");
        Assert.That(capa, Is.GreaterThanOrEqualTo(0), "Falta la capa Head.");

        string[] enLaCapa = Prefab().GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.layer == capa)
            .Select(r => r.name)
            .ToArray();

        Assert.That(enLaCapa, Is.Empty);
    }

    [Test]
    public void LaCamaraDelJugadorNoDibujaLaCapaDeLaCabezaPropia()
    {
        Camera camara = Prefab().transform.Find("MainCamera").GetComponent<Camera>();
        int capa = LayerMask.NameToLayer("Head");

        Assert.That(camara.cullingMask & (1 << capa), Is.Zero);
    }

    [Test]
    public void LaCabezaDelSoldierEstaCargadaYEsDeSuModelo()
    {
        SerializedProperty soldier =
            new SerializedObject(Prefab().GetComponent<PlayerSkin>())
                .FindProperty("skins").GetArrayElementAtIndex(0);
        GameObject modelo = soldier.FindPropertyRelative("modelo").objectReferenceValue as GameObject;
        SerializedProperty cabeza = soldier.FindPropertyRelative("cabeza");

        Assert.That(cabeza.arraySize, Is.EqualTo(4));
        for (int i = 0; i < cabeza.arraySize; i++)
        {
            Renderer parte = cabeza.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
            Assert.That(parte, Is.Not.Null, $"Cabeza {i} vacía.");
            Assert.That(parte.transform.IsChildOf(modelo.transform), Is.True,
                $"{parte.name} no es parte del Soldier.");
        }
    }

    [Test]
    public void LasArmasNoSeDuplicaron()
    {
        // Del Survivalist se copió el cuerpo, no sus armas: hay un solo juego.
        GunSystem[] armas = Prefab().GetComponentsInChildren<GunSystem>(true);
        Assert.That(armas.Select(a => a.name).Distinct().Count(), Is.EqualTo(armas.Length));
    }

    [Test]
    public void LosDosEquiposAparecenConElMismoPrefab()
    {
        GameObject sesion = AssetDatabase.LoadAssetAtPath<GameObject>(SessionRootPath);
        SerializedObject spawner = new(sesion.GetComponent<NetworkPlayerSpawner>());

        Object rojo = spawner.FindProperty("redTeamPlayerPrefab").objectReferenceValue;
        Object azul = spawner.FindProperty("blueTeamPlayerPrefab").objectReferenceValue;

        Assert.That(rojo, Is.SameAs(Prefab()));
        Assert.That(azul, Is.SameAs(Prefab()));
    }

    private static GameObject Prefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null, PlayerPrefabPath);
        return prefab;
    }

    private static GameObject Modelo(SerializedProperty skins, int i) =>
        skins.GetArrayElementAtIndex(i).FindPropertyRelative("modelo").objectReferenceValue
            as GameObject;
}
