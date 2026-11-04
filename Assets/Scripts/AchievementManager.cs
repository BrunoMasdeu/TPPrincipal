using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

// 1. Estructura de datos para un logro individual
[System.Serializable]
public class Achievement
{
    public string id;
    public bool isUnlocked;
    public string unlockDate; // Almacenamos la fecha como texto (ej. "10/10/2026")
}

// 2. Contenedor para la lista (JsonUtility requiere un objeto raíz)
[System.Serializable]
public class AchievementData
{
    public List<Achievement> achievements = new List<Achievement>();
}

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    private string saveFilePath;
    private AchievementData data = new AchievementData();
    public static event Action<string> OnAchievementUnlocked;

    private void Awake()
    {
        // Configurar el Singleton para que persista entre escenas
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Definir la ruta segura de guardado (persistentDataPath funciona en PC, Android, iOS)
        saveFilePath = Path.Combine(Application.persistentDataPath, "logros.json");

        LoadAchievements();
    }

    // Cargar los datos desde el JSON
    private void LoadAchievements()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            data = JsonUtility.FromJson<AchievementData>(json);
        }
        else
        {
            // Si no existe el archivo, inicializamos los 5 logros por defecto
            InitializeDefaultAchievements();
        }
    }

    // Guardar los datos en el JSON
    private void SaveAchievements()
    {
        // El 'true' formatea el JSON para que sea legible por humanos
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFilePath, json);
    }

    // Crear la base de datos inicial
    private void InitializeDefaultAchievements()
    {
        data.achievements = new List<Achievement>
        {
            new Achievement { id = "win_zombies", isUnlocked = false, unlockDate = "" },
            new Achievement { id = "win_ctf", isUnlocked = false, unlockDate = "" },
            new Achievement { id = "win_tdm", isUnlocked = false, unlockDate = "" },
            new Achievement { id = "tutorial_done", isUnlocked = false, unlockDate = "" },
            new Achievement { id = "easter_egg", isUnlocked = false, unlockDate = "" }
        };
        SaveAchievements();
    }

    // Función pública para desbloquear un logro
    public void UnlockAchievement(string achievementId)
    {
        Achievement ach = data.achievements.Find(a => a.id == achievementId);

        if (ach != null && !ach.isUnlocked)
        {
            ach.isUnlocked = true;
            ach.unlockDate = DateTime.Now.ToString("dd/MM/yyyy");
            SaveAchievements();

            Debug.Log($"[Logros] ¡Logro Desbloqueado!: {achievementId}");

            // Aquí podrías disparar un evento para mostrar un popup en la UI
            OnAchievementUnlocked?.Invoke(achievementId);
        }
    }

    // Función pública para que tu UI de menú lea el estado de un logro
    public Achievement GetAchievement(string achievementId)
    {
        return data.achievements.Find(a => a.id == achievementId);
    }
}