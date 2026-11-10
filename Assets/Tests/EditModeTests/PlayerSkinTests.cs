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
        Assert.That(skins.arraySize, Is.EqualTo(2));

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
        Assert.That(equipos, Is.EqualTo(new[] { TeamId.Red, TeamId.Blue }));
    }

    [Test]
    public void ElPrefabArrancaConElSoldierPrendidoYElSurvivalistApagado()
    {
        SerializedProperty skins =
            new SerializedObject(Prefab().GetComponent<PlayerSkin>()).FindProperty("skins");
        GameObject soldier = Modelo(skins, 0);
        GameObject survivalist = Modelo(skins, 1);

        Assert.That(soldier.activeSelf, Is.True);
        Assert.That(survivalist.activeSelf, Is.False);
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
