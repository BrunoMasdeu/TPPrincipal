using NUnit.Framework;
using UnityEngine;

public class WallCollisionResponseTests
{
    [Test]
    public void MovimientoDiagonalContraParedConservaComponenteParalela()
    {
        Vector3 requestedVelocity = new Vector3(3f, 0f, 4f);
        Vector3[] wallNormals = { Vector3.left };

        Vector3 result = Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            wallNormals
        );

        Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(result.z, Is.EqualTo(4f).Within(0.0001f));
    }

    [Test]
    public void MovimientoQueSeAlejaDeParedNoEsModificado()
    {
        Vector3 requestedVelocity = new Vector3(-3f, 0f, 4f);
        Vector3[] wallNormals = { Vector3.left };

        Vector3 result = Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            wallNormals
        );

        Assert.That(result, Is.EqualTo(requestedVelocity));
    }

    [Test]
    public void ParedEnAnguloDeslizaSinDependerDeLaOrientacionDelJugador()
    {
        Vector3 requestedVelocity = Vector3.forward * 6f;
        Vector3 wallNormal = new Vector3(-1f, 0f, -1f).normalized;
        Vector3[] wallNormals = { wallNormal };

        Vector3 result = Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            wallNormals
        );

        Assert.That(
            Vector3.Dot(result, wallNormal),
            Is.EqualTo(0f).Within(0.0001f)
        );
        Assert.That(result.sqrMagnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void MovimientoHaciaEsquinaNoAtraviesaNingunaPared()
    {
        Vector3 requestedVelocity = new Vector3(3f, 0f, 4f);
        Vector3[] wallNormals = { Vector3.left, Vector3.back };

        Vector3 result = Move.ProjectVelocityAlongWalls(
            requestedVelocity,
            wallNormals
        );

        Assert.That(result, Is.EqualTo(Vector3.zero));
    }
}
