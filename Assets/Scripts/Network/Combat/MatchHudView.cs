using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentación de CanvasTDM. Todos los datos proceden de la sesión.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class MatchHudView : MonoBehaviour
{
    [Serializable]
    private class PlayerRow
    {
        public GameObject root;
        public GameObject rootFinal;
        public TMP_Text name;
        public TMP_Text kills;
        public TMP_Text deaths;
        public TMP_Text nameFinal;
        public TMP_Text killsFinal;
        public TMP_Text deathsFinal;
    }

    [SerializeField] private GameObject principalPanel; 
    [SerializeField] private TMP_Text redScore;
    [SerializeField] private TMP_Text blueScore;
    [SerializeField] private TMP_Text tabRedScore;
    [SerializeField] private TMP_Text tabBlueScore;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreLimit;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text modeTitle;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text deathFeed;
    [SerializeField] private GameObject deathFeedPanel;
    [SerializeField] private TMP_Text flagsText;
    [SerializeField] private GameObject tabPanel;
    [SerializeField] private GameObject tableHint;
    [SerializeField] private PlayerRow[] redPlayers;
    [SerializeField] private PlayerRow[] bluePlayers;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text winningTeam;
    [SerializeField] private TMP_Text resultRedScore;
    [SerializeField] private TMP_Text resultBlueScore;
    [SerializeField] private TMP_Text modeScore;
    [SerializeField] private TMP_Text resultHint;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Button leaveButton;

    public bool HasRequiredReferences => redScore != null && blueScore != null &&
        tabRedScore != null && tabBlueScore != null && timeText != null && scoreLimit != null &&
        moneyText != null && modeTitle != null && phaseText != null && deathFeed != null &&
        deathFeedPanel != null && flagsText != null && tabPanel != null && tableHint != null && resultPanel != null && resultTitle != null &&
        resultRedScore != null && resultBlueScore != null && resultHint != null &&
        lobbyButton != null && leaveButton != null && ValidRows(redPlayers) && ValidRows(bluePlayers);

    public void SetVisible(bool visible) => GetComponent<Canvas>().enabled = visible;
    public void SetTableVisible(bool visible) => tabPanel.SetActive(visible);
    public void SetMoney(int money) => moneyText.text = $"${Mathf.Max(0, money)}";
    public void SetDeathFeed(string text)
    {
        deathFeed.text = text;
        deathFeedPanel.SetActive(!string.IsNullOrEmpty(text));
    }
    public void SetFlags(string text) => flagsText.text = text;
    public void SetPhase(string text) => phaseText.text = text;
    public void SetTime(float seconds) => timeText.text = FormatTime(seconds);

    public void SetMode(GameModeId mode)
    {
        modeTitle.text = mode == GameModeId.CTF ? "CAPTURA LA BANDERA" : "TEAM DEATHMATCH";
        flagsText.gameObject.SetActive(mode == GameModeId.CTF);
    }

    public void SetScores(int red, int blue, int limit, GameModeId mode)
    {
        redScore.text = tabRedScore.text = Mathf.Max(0, red).ToString();
        blueScore.text = tabBlueScore.text = Mathf.Max(0, blue).ToString();
        string units = mode == GameModeId.CTF ? "capturas" : "eliminaciones";
        scoreLimit.text = $"Meta: {limit} {units}";
        resultRedScore.text = $"{red}";
        resultBlueScore.text = $"{blue}";
        modeScore.text = $"{units}";
    }

    public void SetPlayers(IReadOnlyList<PlayerMatchStats> players, ulong localClientId, bool hasStats)
    {
        PopulateRows(redPlayers, TeamId.Red, players, localClientId, hasStats);
        PopulateRows(bluePlayers, TeamId.Blue, players, localClientId, hasStats);
    }

    public void SetResult(bool finished, MatchResultData result, bool isHost)
    {
        resultPanel.SetActive(finished);
        tableHint.SetActive(!finished);
        if (!finished)
            return;
        principalPanel.SetActive(false);
        SetTableVisible(false);
        resultTitle.text = result.IsPending ? "Esperando resultado..."
            : result.IsDraw ? "EMPATE"
            : $"VICTORIA";
        winningTeam.text = $"EQUIPO {(result.WinningTeam == TeamId.Red ? "ROJO" : "AZUL")}";
        lobbyButton.interactable = isHost && !result.IsPending;
        leaveButton.interactable = true;
        resultHint.text = isHost ? "Volvé al lobby para configurar la próxima partida."
            : "El host puede regresar a todos al lobby. También podés salir al menú.";
    }

    public void SetLeaving()
    {
        lobbyButton.interactable = leaveButton.interactable = false;
        resultHint.text = "Saliendo de la partida...";
    }

    public static string FormatTime(float seconds)
    {
        int wholeSeconds = Mathf.CeilToInt(Mathf.Max(0, seconds));
        return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
    }

    private static bool ValidRows(PlayerRow[] rows)
    {
        if (rows == null || rows.Length != MultiplayerSessionLimits.RoomCapacity / 2)
            return false;
        foreach (PlayerRow row in rows)
            if (row == null || row.root == null || row.name == null || row.kills == null || row.deaths == null)
                return false;
        return true;
    }

    private static void PopulateRows(PlayerRow[] rows, TeamId team,
        IReadOnlyList<PlayerMatchStats> players, ulong localClientId, bool hasStats)
    {
        int slot = 0;
        foreach (PlayerMatchStats player in players)
        {
            if (player.TeamId != team || slot >= rows.Length)
                continue;
            PlayerRow row = rows[slot++];
            row.root.SetActive(true);
            if (row.rootFinal != null)
                row.rootFinal.SetActive(true);
            row.name.text = $"Jugador {player.ClientId + 1}{(player.ClientId == localClientId ? " (vos)" : "")}";
            row.kills.text = hasStats ? player.Kills.ToString() : "--";
            row.deaths.text = hasStats ? player.Deaths.ToString() : "--";
            row.nameFinal.text = $"Jugador {player.ClientId + 1}{(player.ClientId == localClientId ? " (vos)" : "")}";
            row.killsFinal.text = hasStats ? player.Kills.ToString() : "--";
            row.deathsFinal.text = hasStats ? player.Deaths.ToString() : "--";
        }
        while (slot < rows.Length)
        {
            PlayerRow row = rows[slot++];
            row.root.SetActive(false);
            if (row.rootFinal != null)
                row.rootFinal.SetActive(false);
        }
    }
}
