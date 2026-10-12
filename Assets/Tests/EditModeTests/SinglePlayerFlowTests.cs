using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SinglePlayerFlowTests
{
    private const string RespawnPrefabPath =
        "Assets/Scenes/llegada_respawn/Respawn.prefab";

    [Test]
    public void RespawnDelTutorialAdmiteJugadorSinEquipo()
    {
        GameObject respawn =
            AssetDatabase.LoadAssetAtPath<GameObject>(RespawnPrefabPath);

        Assert.That(respawn, Is.Not.Null);

        TeamSpawnPoint spawnPoint = respawn.GetComponent<TeamSpawnPoint>();
        Assert.That(spawnPoint, Is.Not.Null,
            "El punto Respawn debe participar del spawner de red.");
        Assert.That(spawnPoint.TeamId, Is.EqualTo(TeamId.None),
            "Single player necesita un spawn sin equipo.");
    }
}
