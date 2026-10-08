using System;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Consola local de pruebas: sólo el host puede ajustar el reloj.</summary>
public class MatchDebugConsole : MonoBehaviour
{
    public static bool IsInputCaptured { get; private set; }

    private bool isOpen;
    private bool focusInput;
    private int openedFrame;
    private string commandInput = string.Empty;
    private string lastOutput = string.Empty;

    private void Update()
    {
        if (!CanDisplay())
        {
            if (isOpen)
                Close();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!isOpen && keyboard.tKey.wasPressedThisFrame)
        {
            Open();
            return;
        }

        if (isOpen && (keyboard.escapeKey.wasPressedThisFrame ||
            (commandInput.Length == 0 && keyboard.tKey.wasPressedThisFrame)))
            Close();
    }

    private bool CanDisplay()
    {
        NetworkLobbySession lobby = NetworkLobbySession.Instance;
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkMatchManager.Instance != null &&
            lobby != null && lobby.Phase == SessionPhase.InMatch &&
            CombatValidationRules.IsCombatMode(lobby.SelectedGameModeId);
    }

    private void Open()
    {
        isOpen = true;
        IsInputCaptured = true;
        focusInput = true;
        openedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        isOpen = false;
        IsInputCaptured = false;
        focusInput = false;

        // El resultado y el lobby necesitan el cursor libre para sus botones.
        if (CanDisplay() && NetworkMatchManager.Instance.Phase != MatchPhase.Finished)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        if (isOpen)
            Close();
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        GUI.depth = -100;
        float x = (Screen.width - 500f) * 0.5f;
        float y = Screen.height - 125f;
        GUI.Box(new Rect(x, y, 500f, 110f), "COMANDO DE PRUEBA [T / Esc]");
        GUI.Label(new Rect(x + 12f, y + 25f, 476f, 20f), "match time <segundos>");

        Event current = Event.current;
        if (Time.frameCount == openedFrame && current.type == EventType.KeyDown &&
            current.keyCode == KeyCode.T)
            current.Use();

        if (current.type == EventType.KeyDown &&
            (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter))
        {
            Execute();
            current.Use();
        }

        GUI.SetNextControlName("MatchDebugCommand");
        commandInput = GUI.TextField(new Rect(x + 12f, y + 47f, 476f, 23f), commandInput);
        if (focusInput && current.type == EventType.Repaint)
        {
            GUI.FocusControl("MatchDebugCommand");
            focusInput = false;
        }

        GUI.Label(new Rect(x + 12f, y + 77f, 476f, 22f), lastOutput);
    }

    private void Execute()
    {
        NetworkMatchManager match = NetworkMatchManager.Instance;
        lastOutput = TryParseTimeCommand(commandInput, out int seconds) &&
            match != null && match.TrySetRemainingTimeForDebug(seconds)
                ? $"Tiempo restante: {seconds} s"
                : "Comando inválido";
        commandInput = string.Empty;
        focusInput = true;
    }

    public static bool TryParseTimeCommand(string input, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string[] parts = input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 &&
            string.Equals(parts[0], "match", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[1], "time", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
                out seconds) && seconds >= 1 && seconds <= 3600;
    }
}
