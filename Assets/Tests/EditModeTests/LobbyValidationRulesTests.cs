using System.Collections.Generic;
using NUnit.Framework;

public class LobbyValidationRulesTests
{
    [TestCase(4, 2, 8, true)]
    [TestCase(1, 2, 8, false)]
    [TestCase(9, 2, 8, false)]
    [TestCase(4, 0, 8, false)]
    [TestCase(4, 8, 2, false)]
    public void CantidadPermitidaRespetaMinimoYMaximo(
        int players,
        int minimum,
        int maximum,
        bool expected)
    {
        Assert.That(
            LobbyValidationRules.IsPlayerCountAllowed(players, minimum, maximum),
            Is.EqualTo(expected)
        );
    }

    [TestCase(0, 4, true)]
    [TestCase(3, 4, true)]
    [TestCase(4, 4, false)]
    [TestCase(-1, 4, false)]
    public void CapacidadSoloAceptaUnJugadorSiQuedaLugar(
        int current,
        int maximum,
        bool expected)
    {
        Assert.That(
            LobbyValidationRules.HasCapacity(current, maximum),
            Is.EqualTo(expected)
        );
    }

    [TestCase(TeamId.Red, true)]
    [TestCase(TeamId.Blue, true)]
    [TestCase(TeamId.None, false)]
    public void EquipoValidoRequiereRojoOAzul(TeamId teamId, bool expected)
    {
        Assert.That(LobbyValidationRules.IsValidTeam(teamId), Is.EqualTo(expected));
    }

    [TestCase(GameModeId.TDM, true)]
    [TestCase(GameModeId.CTF, true)]
    [TestCase(GameModeId.Race, true)]
    [TestCase(GameModeId.None, false)]
    public void ModoDebeEstarSeleccionado(GameModeId gameModeId, bool expected)
    {
        Assert.That(
            LobbyValidationRules.HasSelectedMode(gameModeId),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void ModoSinEquiposAceptaJugadorListoSinTeamId()
    {
        Assert.That(
            LobbyValidationRules.CanStartMatch(
                GameModeId.Race,
                1,
                Players(new LobbyPlayerData(1, TeamId.None, true)),
                true,
                false
            ),
            Is.True
        );
    }

    [Test]
    public void EquiposBalanceadosPermitenComoMaximoUnJugadorDeDiferencia()
    {
        Assert.That(
            LobbyValidationRules.AreTeamsBalanced(
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(2, TeamId.Blue, true),
                    new LobbyPlayerData(3, TeamId.Red, true)
                )
            ),
            Is.True
        );

        Assert.That(
            LobbyValidationRules.AreTeamsBalanced(
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(2, TeamId.Red, true)
                )
            ),
            Is.False
        );
    }

    [Test]
    public void ReadyRequiereCantidadExactaYTodosPreparados()
    {
        Assert.That(
            LobbyValidationRules.AreAllPlayersReady(
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(2, TeamId.Blue, true)
                ),
                2
            ),
            Is.True
        );
        Assert.That(
            LobbyValidationRules.AreAllPlayersReady(
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(2, TeamId.Blue, false)
                ),
                2
            ),
            Is.False
        );
        Assert.That(
            LobbyValidationRules.AreAllPlayersReady(
                Players(new LobbyPlayerData(1, TeamId.Red, true)),
                2
            ),
            Is.False
        );
    }

    [Test]
    public void InicioRequiereModoIdsUnicosEquiposValidosBalanceYReady()
    {
        List<LobbyPlayerData> validPlayers = Players(
            new LobbyPlayerData(1, TeamId.Red, true),
            new LobbyPlayerData(2, TeamId.Blue, true)
        );

        Assert.That(
            LobbyValidationRules.CanStartMatch(GameModeId.TDM, 2, validPlayers),
            Is.True
        );
        Assert.That(
            LobbyValidationRules.CanStartMatch(GameModeId.None, 2, validPlayers),
            Is.False
        );
        Assert.That(
            LobbyValidationRules.CanStartMatch(
                GameModeId.TDM,
                2,
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(1, TeamId.Blue, true)
                )
            ),
            Is.False
        );
        Assert.That(
            LobbyValidationRules.CanStartMatch(
                GameModeId.TDM,
                2,
                Players(
                    new LobbyPlayerData(1, TeamId.Red, true),
                    new LobbyPlayerData(2, TeamId.Red, true)
                )
            ),
            Is.False
        );
    }

    [Test]
    public void AsignacionDeEquipoRespetaCapacidadYBalance()
    {
        List<LobbyPlayerData> players = Players(
            new LobbyPlayerData(1, TeamId.None, false),
            new LobbyPlayerData(2, TeamId.None, false),
            new LobbyPlayerData(3, TeamId.None, false),
            new LobbyPlayerData(4, TeamId.None, false)
        );

        Assert.That(
            LobbyValidationRules.CanAssignPlayerToTeam(
                players,
                1,
                TeamId.Red,
                4),
            Is.True
        );

        players[0] = new LobbyPlayerData(1, TeamId.Red, false);

        Assert.That(
            LobbyValidationRules.CanAssignPlayerToTeam(
                players,
                2,
                TeamId.Red,
                4),
            Is.False,
            "Dos asignaciones rojas seguidas dejarían una diferencia mayor a uno."
        );

        Assert.That(
            LobbyValidationRules.CanAssignPlayerToTeam(
                players,
                2,
                TeamId.Blue,
                4),
            Is.True
        );
    }

    private static List<LobbyPlayerData> Players(
        params LobbyPlayerData[] players)
    {
        return new List<LobbyPlayerData>(players);
    }
}
