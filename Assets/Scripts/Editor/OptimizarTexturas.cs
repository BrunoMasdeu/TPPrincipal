using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [OPTIMIZACION] Limita el tamaño de las texturas grandes de los packs de mapas
/// y modelos, para que el juego use menos memoria y el build pese menos.
///
/// Menú: Tools > Optimizacion > Limitar texturas a 1024
///
/// - Solo toca texturas comunes y normal maps de más de 1024 px.
/// - No toca sprites ni íconos del HUD, ni lightmaps, ni texturas que ya son chicas.
/// - Se puede volver atrás con git (solo cambia los archivos .meta).
/// </summary>
public static class OptimizarTexturas
{
    private const int TamanoMaximo = 1024;

    [MenuItem("Tools/Optimizacion/Limitar texturas a 1024")]
    private static void Limitar()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int cambiadas = 0;
        StringBuilder lista = new StringBuilder();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < guids.Length; i++)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Limitando texturas", ruta, (float)i / guids.Length);

                if (!(AssetImporter.GetAtPath(ruta) is TextureImporter importador))
                    continue;

                bool esComun = importador.textureType == TextureImporterType.Default
                               || importador.textureType == TextureImporterType.NormalMap;
                if (!esComun)
                    continue;

                bool cambio = false;

                if (importador.maxTextureSize > TamanoMaximo)
                {
                    importador.maxTextureSize = TamanoMaximo;
                    cambio = true;
                }

                // Si la PC tiene una configuración propia para Windows, también se limita.
                TextureImporterPlatformSettings windows = importador.GetPlatformTextureSettings("Standalone");
                if (windows.overridden && windows.maxTextureSize > TamanoMaximo)
                {
                    windows.maxTextureSize = TamanoMaximo;
                    importador.SetPlatformTextureSettings(windows);
                    cambio = true;
                }

                if (cambio)
                {
                    importador.SaveAndReimport();
                    cambiadas++;
                    lista.AppendLine(ruta);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[OPTIMIZACION] Texturas limitadas a {TamanoMaximo}: {cambiadas}\n{lista}");
        EditorUtility.DisplayDialog("Texturas", $"Listo: {cambiadas} texturas limitadas a {TamanoMaximo} px.", "OK");
    }
}
