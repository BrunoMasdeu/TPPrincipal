using NUnit.Framework;

// [COG-587] Reglas del nombre del jugador.
public class NombreJugadorRulesTests
{
    [TestCase("Tomas")]
    [TestCase("Ñandu_99")]
    [TestCase("Juan Gomez")]
    public void AceptaAsciiYLaEnie(string nombre)
    {
        Assert.That(NombreJugadorRules.Validar(nombre), Is.Null);
    }

    [TestCase("Juan Pérez")] // la é no es ASCII
    [TestCase("Ñandú")]      // la ú no es ASCII
    public void RechazaOtrosCaracteres(string nombre)
    {
        Assert.That(NombreJugadorRules.Validar(nombre), Is.Not.Null);
    }

    [Test]
    public void RechazaNombreVacioOSoloEspacios()
    {
        Assert.That(NombreJugadorRules.Validar(""), Is.Not.Null);
        Assert.That(NombreJugadorRules.Validar("   "), Is.Not.Null);
        Assert.That(NombreJugadorRules.Validar(null), Is.Not.Null);
    }

    [Test]
    public void AceptaHasta13Caracteres()
    {
        Assert.That(NombreJugadorRules.Validar("1234567890123"), Is.Null);
        Assert.That(NombreJugadorRules.Validar("12345678901234"), Is.Not.Null);
    }

    [Test]
    public void RechazaLosSignosMenorYMayor()
    {
        Assert.That(NombreJugadorRules.Validar("<b>Tomas"), Is.Not.Null);
        Assert.That(NombreJugadorRules.Validar("Tomas>"), Is.Not.Null);
    }

    [Test]
    public void NombresIgualesSinImportarMayusculas()
    {
        Assert.That(NombreJugadorRules.SonIguales("Tomas", "tOMAS "), Is.True);
        Assert.That(NombreJugadorRules.SonIguales("Ñoño", "ñOÑO"), Is.True);
        Assert.That(NombreJugadorRules.SonIguales("Tomas", "Felipe"), Is.False);
    }

    [Test]
    public void ElNombreViajaComoBytesSinCambiar()
    {
        byte[] datos = NombreJugadorRules.ABytes("Ñandú");
        Assert.That(NombreJugadorRules.DesdeBytes(datos), Is.EqualTo("Ñandú"));
        Assert.That(NombreJugadorRules.DesdeBytes(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void LobbyPlayerDataGuardaElNombre()
    {
        LobbyPlayerData jugador = new LobbyPlayerData(3, TeamId.Red, false, "Tomas");

        Assert.That(jugador.Nombre.ToString(), Is.EqualTo("Tomas"));
        Assert.That(jugador, Is.Not.EqualTo(new LobbyPlayerData(3, TeamId.Red, false, "Felipe")));
    }
}
