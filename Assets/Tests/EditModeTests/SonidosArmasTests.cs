using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [ANIMACIONES] Revisa que cada arma tenga sonido de disparo (menú
/// Tools > Player > Configurar Sonidos de Armas).
/// </summary>
public class SonidosArmasTests
{
    private const string PrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

    [TestCase(WeaponId.Knife, false)]
    [TestCase(WeaponId.Pistol, true)]
    [TestCase(WeaponId.Revolver, true)]
    [TestCase(WeaponId.AK, true)]
    [TestCase(WeaponId.M4, true)]
    [TestCase(WeaponId.Sniper, true)]
    public void CadaArmaTieneSonidoDeDisparoYRecarga(WeaponId arma, bool conRecarga)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var audio = new SerializedObject(prefab.GetComponent<PlayerWeaponAudio>());
        SerializedProperty lista = audio.FindProperty("weaponSounds");

        WeaponShotAudio sonido = null;
        for (int i = 0; i < lista.arraySize; i++)
        {
            SerializedProperty entrada = lista.GetArrayElementAtIndex(i);
            if (entrada.FindPropertyRelative("weapon").intValue == (int)arma)
                sonido = entrada.FindPropertyRelative("sound").objectReferenceValue as WeaponShotAudio;
        }

        Assert.That(sonido, Is.Not.Null, $"{arma} no tiene Weapon Shot Audio conectado.");
        Assert.That(sonido.GetComponent<AudioSource>(), Is.Not.Null);

        var so = new SerializedObject(sonido);
        Assert.That(so.FindProperty("shot1P").arraySize, Is.GreaterThan(0),
            $"{arma} no tiene sonido de disparo.");
        if (arma == WeaponId.Knife)
            Assert.That(so.FindProperty("hitClips").arraySize, Is.GreaterThan(0),
                "El cuchillo necesita el sonido de golpe.");
        if (conRecarga)
            Assert.That(so.FindProperty("reloadSteps").arraySize, Is.GreaterThan(0),
                $"{arma} no tiene sonido de recarga.");
    }
}
