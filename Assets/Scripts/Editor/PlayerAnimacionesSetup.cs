using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [ANIMACIONES] Configura las animaciones del jugador con el pack de Kevin
/// Iglesias (el mismo de las poses de pistola y rifle), para que piernas y
/// torso salgan del mismo autor. De Mixamo queda solo el agachado (quieto y
/// caminando), que ese pack no trae.
///
/// Menú: Tools > Player > Configurar Animaciones.
/// Se puede correr las veces que haga falta: busca todo por nombre y no
/// duplica nada. No toca las capas Wall Run ni Death, ni los estados de
/// pistola y rifle de la capa Upper Body (solo agrega los del cuchillo).
/// </summary>
public static class PlayerAnimacionesSetup
{
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";

    // Pack de Kevin Iglesias: ya viene Humanoid, en loop y sin desplazamiento.
    private const string Pack = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";

    // De Mixamo solo se usa el agachado, que el pack no trae. Todo lo demás
    // que haya en la carpeta Mixamo va a la papelera de Windows.
    private const string Mixamo = "Assets/Animations/Mixamo";
    private const string AgachadoQuieto = Mixamo + "/Lite Rifle Pack/idle crouching.fbx";
    private const string AgachadoAdelante =
        Mixamo + "/Rifle 8-Way Locomotion Pack/walk crouching forward.fbx";
    private const string AgachadoAtras =
        Mixamo + "/Rifle 8-Way Locomotion Pack/walk crouching backward.fbx";
    private const string AvatarMixamo = "Assets/Animations/Pistol1/X Bot.fbx";

    // Mezcla al agacharse y al pararse, ajustada a mano en Unity.
    private const float MezclaAgachado = 0.12f;

    // Valor de WeaponPose del cuchillo (el mismo de PlayerAnimationController).
    private const int PoseCuchillo = 3;

    // Velocidad del ataque del cuchillo, ajustada a mano en Unity.
    private const float VelocidadAtaqueCuchillo = 1.57f;

    // Gesto de lanzar la granada, con los valores ajustados a mano en Unity.
    // La granada sale de la mano a los "Throw Delay Seconds" de
    // NetworkPlayerGrenades (PlayerPrefab).
    private const float VelocidadLanzamiento = 1.2f;
    private const float SalidaLanzamiento = 0.7f;
    private const float MezclaLanzamiento = 0.35f;

    [MenuItem("Tools/Player/Configurar Animaciones")]
    public static void Configurar()
    {
        AssetDatabase.Refresh();

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine capaBase = Capa(controller, "Base Layer");

        ArmarLocomocion(controller, capaBase);
        ArmarAgachado(controller, capaBase);
        ArmarCuchillo(Capa(controller, "Upper Body"));
        ArmarGranada(controller, Capa(controller, "Upper Body"));

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Al final, cuando el Animator ya no usa los clips viejos.
        string[] usados = { AgachadoQuieto, AgachadoAdelante, AgachadoAtras };
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Mixamo }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!usados.Contains(ruta))
                AssetDatabase.MoveAssetToTrash(ruta);
        }

        Debug.Log("[ANIMACIONES] Listo: locomoción, salto, agachado, cuchillo y granada.");
    }

    // ---------------------------------------------------------------------
    // Skins
    // ---------------------------------------------------------------------

    private const string PrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string MaterialesSoldado = "Assets/Prefabs/Soldier/Materials/";
    // Lo que se tiñe con el color del equipo. El chaleco (Plates) y los
    // accesorios (Pouches) quedan con su color original.
    private static readonly string[] Ropa = { "Kitel", "Pants", "Helmet" };

    /// <summary>
    /// Deja tres skins con el mismo soldado (mismo modelo, avatar, cabeza,
    /// cámara y posición de armas):
    ///   Element 0: original, para tutorial, zombies y carrera (sin equipo).
    ///   Element 1: equipo rojo.   Element 2: equipo azul.
    /// Cada equipo tiene sus propios materiales (Kitel_Rojo, Kitel_Azul...)
    /// para poder ponerles otra textura, y un Color Ropa que los tiñe.
    /// </summary>
    [MenuItem("Tools/Player/Usar el Soldado en los Dos Equipos")]
    public static void SoldadoEnLosDosEquipos()
    {
        AssetDatabase.Refresh(); // por si hay texturas nuevas
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        var skin = new SerializedObject(prefab.GetComponent<PlayerSkin>());
        SerializedProperty skins = skin.FindProperty("skins");

        // De dos skins (rojo y azul) a tres: se agrega el original adelante.
        if (skins.arraySize == 2)
            skins.InsertArrayElementAtIndex(0);

        SerializedProperty original = skins.GetArrayElementAtIndex(0);
        original.FindPropertyRelative("equipo").enumValueIndex = (int)TeamId.None;
        original.FindPropertyRelative("materialesRopa").arraySize = 0;
        original.FindPropertyRelative("materialesTenidos").arraySize = 0;
        original.FindPropertyRelative("colorRopa").colorValue = Color.white;

        ArmarEquipo(skins, 1, TeamId.Red, "_Rojo");
        ArmarEquipo(skins, 2, TeamId.Blue, "_Azul");

        skin.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefab);

        // Copias compartidas de la versión anterior: ya no se usan.
        foreach (string nombre in Ropa)
            AssetDatabase.DeleteAsset(MaterialesSoldado + nombre + "_Tenible.mat");

        AssetDatabase.SaveAssets();
        Debug.Log("[ANIMACIONES] Listo: skin original, rojo y azul con el mismo soldado.");
    }

    private static void ArmarEquipo(
        SerializedProperty skins, int indice, TeamId equipo, string sufijo)
    {
        SerializedProperty original = skins.GetArrayElementAtIndex(0);
        SerializedProperty skin = skins.GetArrayElementAtIndex(indice);
        skin.FindPropertyRelative("equipo").enumValueIndex = (int)equipo;

        foreach (string campo in new[] { "modelo", "avatar" })
            skin.FindPropertyRelative(campo).objectReferenceValue =
                original.FindPropertyRelative(campo).objectReferenceValue;
        foreach (string campo in new[] { "posicionArmas", "escalaArmas", "posicionCamara" })
            skin.FindPropertyRelative(campo).vector3Value =
                original.FindPropertyRelative(campo).vector3Value;
        skin.FindPropertyRelative("rotacionArmas").quaternionValue =
            original.FindPropertyRelative("rotacionArmas").quaternionValue;

        SerializedProperty cabeza = original.FindPropertyRelative("cabeza");
        SerializedProperty cabezaSkin = skin.FindPropertyRelative("cabeza");
        cabezaSkin.arraySize = cabeza.arraySize;
        for (int i = 0; i < cabeza.arraySize; i++)
            cabezaSkin.GetArrayElementAtIndex(i).objectReferenceValue =
                cabeza.GetArrayElementAtIndex(i).objectReferenceValue;

        SerializedProperty ropa = skin.FindPropertyRelative("materialesRopa");
        SerializedProperty tenidos = skin.FindPropertyRelative("materialesTenidos");
        ropa.arraySize = tenidos.arraySize = Ropa.Length;
        for (int i = 0; i < Ropa.Length; i++)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialesSoldado + Ropa[i] + ".mat");
            ropa.GetArrayElementAtIndex(i).objectReferenceValue = material;
            tenidos.GetArrayElementAtIndex(i).objectReferenceValue =
                CopiaTenible(material, sufijo);
        }

        // El color ya viene en la textura de cada equipo: Color Ropa queda en
        // blanco (sin teñir) salvo que se haya elegido otro a mano.
        SerializedProperty color = skin.FindPropertyRelative("colorRopa");
        Color actual = color.colorValue;
        bool deArranque = actual.a == 0f ||
            actual == new Color(1f, 0.45f, 0.4f) ||
            actual == new Color(0.5f, 0.65f, 1f) ||
            actual == new Color(0.55f, 0.65f, 1f);
        if (deArranque)
            color.colorValue = Color.white;
    }

    /// <summary>
    /// Copia de un material del soldado con el shader Lit de URP, que sí acepta
    /// color (el shader original no). Usa la textura del equipo si existe
    /// (por ejemplo Textures/Kitel_Rojo_albedo.jpg); si no, la original.
    /// </summary>
    private static Material CopiaTenible(Material original, string sufijo)
    {
        string ruta = MaterialesSoldado + original.name + sufijo + ".mat";
        var texturaDelEquipo = AssetDatabase.LoadAssetAtPath<Texture>(
            MaterialesSoldado + "Textures/" + original.name + sufijo + "_albedo.jpg");

        var copia = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (copia != null)
        {
            if (texturaDelEquipo != null)
            {
                copia.SetTexture("_BaseMap", texturaDelEquipo);
                EditorUtility.SetDirty(copia);
            }
            return copia;
        }

        copia = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        copia.SetTexture("_BaseMap",
            texturaDelEquipo != null ? texturaDelEquipo : Textura(original, "_BaseMap"));
        copia.SetColor("_BaseColor", Color.white);
        copia.SetFloat("_Smoothness", 0.2f);

        Texture normal = Textura(original, "_BumpMap");
        copia.SetTexture("_BumpMap", normal);
        if (normal != null) copia.EnableKeyword("_NORMALMAP");

        Texture oclusion = Textura(original, "_OcclusionMap");
        copia.SetTexture("_OcclusionMap", oclusion);
        if (oclusion != null) copia.EnableKeyword("_OCCLUSIONMAP");

        AssetDatabase.CreateAsset(copia, ruta);
        return copia;
    }

    private static Texture Textura(Material material, string nombre) =>
        material.HasProperty(nombre) ? material.GetTexture(nombre) : null;

    // ------------------------------------------------------------------
    // Efecto y sonido de la granada (asset de Aegis77).
    // ------------------------------------------------------------------

    private const string GranadaPrefab = "Assets/Prefabs/Granada.prefab";
    private const string CarpetaGranada = "Assets/Game Assets/Aegis77/Grenades testing/";
    private const string SonidosGranada = "Assets/Audio/Granada/Explosion/";

    /// <summary>
    /// Menú: Tools > Player > Agregar Efectos a la Granada. Pasa los
    /// materiales del efecto a URP y le agrega GrenadeEffects al prefab
    /// Granada con el efecto y los sonidos de Assets/Audio/Granada.
    /// Se puede correr las veces que haga falta.
    /// </summary>
    [MenuItem("Tools/Player/Agregar Efectos a la Granada")]
    public static void EfectosDeLaGranada()
    {
        AssetDatabase.Refresh();

        string rutaEfecto = CarpetaGranada + "Prefabs/Explosion.prefab";
        var efecto = AssetDatabase.LoadAssetAtPath<GameObject>(rutaEfecto);
        AudioClip[] lanzar = Clips("Granada/Explosion/Throw.wav");
        AudioClip[] explosiones = Clips(
            "Granada/Explosion/Explosion1.wav",
            "Granada/Explosion/Explosion2.wav",
            "Granada/Explosion/Explosion3.wav");

        if (efecto == null || lanzar == null || explosiones == null)
        {
            Debug.LogError("[ANIMACIONES] Faltan archivos del efecto en " + CarpetaGranada);
            return;
        }

        foreach (ParticleSystemRenderer renderer in efecto.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && AssetDatabase.GetAssetPath(material).StartsWith("Assets/"))
                    PasarAUrp(material);
            }
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(GranadaPrefab);
        try
        {
            GrenadeEffects efectos = raiz.GetComponent<GrenadeEffects>();
            if (efectos == null)
                efectos = raiz.AddComponent<GrenadeEffects>();

            var so = new SerializedObject(efectos);
            so.FindProperty("explosionPrefab").objectReferenceValue = efecto;
            so.FindProperty("throwSound").objectReferenceValue = lanzar[0];
            SerializedProperty sonidos = so.FindProperty("explosionSounds");
            sonidos.arraySize = explosiones.Length;
            for (int i = 0; i < explosiones.Length; i++)
                sonidos.GetArrayElementAtIndex(i).objectReferenceValue = explosiones[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, GranadaPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ANIMACIONES] Granada lista: efecto y sonidos asignados.");
    }

    // El efecto trae materiales del pipeline viejo (se verían rosa en URP).
    // Se pasan al shader de partículas de URP conservando textura y color.
    private static void PasarAUrp(Material material)
    {
        if (material.shader.name.Contains("Universal Render Pipeline"))
            return;

        Shader urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urp == null)
            return;

        Texture textura = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        bool aditivo = material.HasProperty("_DstBlend") && material.GetFloat("_DstBlend") == 1f;
        bool flipbook = material.HasProperty("_FlipbookMode") && material.GetFloat("_FlipbookMode") > 0.5f;
        float fuerza = Mathf.Max(color.r, color.g, color.b, 1f);

        material.shader = urp;
        material.SetTexture("_BaseMap", textura);
        material.SetColor("_BaseColor", new Color(color.r / fuerza, color.g / fuerza, color.b / fuerza, color.a));

        // Los colores muy brillantes (HDR) pasan a emisión.
        if (fuerza > 1f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            material.SetTexture("_EmissionMap", textura);
        }

        // Transparente: mezcla normal o aditiva según el material original.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", aditivo ? 2f : 0f);
        material.SetFloat("_SrcBlend", 5f);
        material.SetFloat("_DstBlend", aditivo ? 1f : 10f);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;

        material.SetFloat("_FlipbookBlending", flipbook ? 1f : 0f);
        if (flipbook)
            material.EnableKeyword("_FLIPBOOKBLENDING_ON");

        EditorUtility.SetDirty(material);
    }

    // ------------------------------------------------------------------
    // Limpieza de los paquetes importados (Kevin Iglesias y Aegis).
    // ------------------------------------------------------------------

    private static readonly string[] Paquetes =
    {
        "Assets/Kevin Iglesias/",
        "Assets/Game Assets/Aegis77/",
    };

    // Las animaciones del pack copian el esqueleto de este modelo: no se
    // nombra en ningún Animator, pero sin él se rompen.
    private const string ModeloDelPack =
        "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";

    /// <summary>
    /// Menú: Tools > Player > Limpiar Assets Importados. Manda a la papelera
    /// de Windows todo lo de Kevin Iglesias y Aegis que no use ninguna otra
    /// parte del proyecto (escenas, prefabs, Animators, materiales...).
    /// Antes muestra cuánto va a borrar y pide confirmación.
    /// </summary>
    [MenuItem("Tools/Player/Limpiar Assets Importados")]
    public static void LimpiarAssetsImportados()
    {
        // Primero la granada pasa a usar los sonidos de Assets/Audio/Granada,
        // así los del paquete dejan de hacer falta.
        EfectosDeLaGranada();

        EditorUtility.DisplayProgressBar("Limpiar assets importados", "Buscando qué se usa...", 0.5f);
        List<string> sobran;
        int conservados;
        try
        {
            string[] todos = AssetDatabase.GetAllAssetPaths()
                .Where(r => r.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(r))
                .ToArray();

            var usados = new HashSet<string>(
                AssetDatabase.GetDependencies(todos.Where(r => !EnPaquete(r)).ToArray(), true));
            usados.UnionWith(AssetDatabase.GetDependencies(ModeloDelPack, true));

            sobran = todos.Where(r => EnPaquete(r) && !usados.Contains(r)).ToList();
            conservados = todos.Count(EnPaquete) - sobran.Count;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        long bytes = sobran.Sum(r => new FileInfo(r).Length);
        string mensaje =
            $"Se van a mandar a la papelera {sobran.Count} archivos ({bytes / 1048576f:F1} MB) " +
            $"de Kevin Iglesias y Aegis que no usa el proyecto.\n\n" +
            $"Se conservan {conservados} archivos que sí se usan.";

        if (sobran.Count == 0 ||
            !EditorUtility.DisplayDialog("Limpiar assets importados", mensaje, "Borrar", "Cancelar"))
            return;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string ruta in sobran)
                AssetDatabase.MoveAssetToTrash(ruta);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // Carpetas que quedaron vacías, de la más profunda a la más externa.
        foreach (string paquete in Paquetes)
        {
            string raiz = paquete.TrimEnd('/');
            if (!Directory.Exists(raiz))
                continue;

            var carpetas = Directory.GetDirectories(raiz, "*", SearchOption.AllDirectories)
                .OrderByDescending(c => c.Length);
            foreach (string carpeta in carpetas)
            {
                bool vacia = !Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories)
                    .Any(f => !f.EndsWith(".meta"));
                if (vacia && Directory.Exists(carpeta))
                    AssetDatabase.MoveAssetToTrash(carpeta.Replace('\\', '/'));
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[ANIMACIONES] Limpieza lista: {sobran.Count} archivos a la papelera ({bytes / 1048576f:F1} MB).");
    }

    private static bool EnPaquete(string ruta) => Paquetes.Any(p => ruta.StartsWith(p));

    // ------------------------------------------------------------------
    // Sonidos de disparo y recarga de cada arma (carpeta Assets/Audio).
    // ------------------------------------------------------------------

    private const string Sonidos = "Assets/Audio/";

    /// <summary>
    /// Menú: Tools > Player > Configurar Sonidos de Armas. A cada arma del
    /// PlayerPrefab le pone su Weapon Shot Audio (disparo y recarga) y lo
    /// conecta en el Player Weapon Audio de la raíz. La pistola queda como
    /// estaba. Se puede correr las veces que haga falta.
    /// </summary>
    [MenuItem("Tools/Player/Configurar Sonidos de Armas")]
    public static void SonidosDeArmas()
    {
        AssetDatabase.Refresh();

        AudioClip[] disparoRifle = Clips(
            "Rifle/Disparo/sfx_wpn_ar_gunshot_01.wav",
            "Rifle/Disparo/sfx_wpn_ar_gunshot_02.wav",
            "Rifle/Disparo/sfx_wpn_ar_gunshot_03.wav");
        AudioClip[] recargaRifle = Clips(
            "Rifle/Recarga/sfx_wpn_ar_reload_01_magOut.wav",
            "Rifle/Recarga/sfx_wpn_ar_reload_02_magDraw.wav",
            "Rifle/Recarga/sfx_wpn_ar_reload_03_magIn.wav",
            "Rifle/Recarga/sfx_wpn_ar_reload_04_charge.wav");
        AudioClip[] disparoRevolver = Clips("Revolver/Disparo/revolver_mp3.mp3");
        // El revólver no tiene corredera: se saltea el slideLock.
        AudioClip[] recargaRevolver = Clips(
            "Revolver/Recarga/sfx_wpn_hgn_reload_02_magOut.wav",
            "Revolver/Recarga/sfx_wpn_hgn_reload_03_magDraw.wav",
            "Revolver/Recarga/sfx_wpn_hgn_reload_04_magIn.wav",
            "Revolver/Recarga/sfx_wpn_hgn_reload_05_charge.wav");
        AudioClip[] sniper1P = Clips(
            "Sniper/Disparo/AntiMaterialRifle_1p_01.wav",
            "Sniper/Disparo/AntiMaterialRifle_1p_02.wav",
            "Sniper/Disparo/AntiMaterialRifle_1p_03.wav");
        AudioClip[] sniper3P = Clips(
            "Sniper/Disparo/AntiMaterialRifle_3p_01.wav",
            "Sniper/Disparo/AntiMaterialRifle_3p_02.wav",
            "Sniper/Disparo/AntiMaterialRifle_3p_03.wav");
        AudioClip[] sniperLejos = Clips(
            "Sniper/Disparo/AntiMaterialRifle_far_01.wav",
            "Sniper/Disparo/AntiMaterialRifle_far_02.wav",
            "Sniper/Disparo/AntiMaterialRifle_far_03.wav");
        AudioClip[] recargaSniper = Clips(
            "Sniper/Recarga/sfx_wpn_ar_reload_01_magOut.wav",
            "Sniper/Recarga/sfx_wpn_ar_reload_02_magDraw.wav",
            "Sniper/Recarga/sfx_wpn_ar_reload_03_magIn.wav",
            "Sniper/Recarga/sfx_wpn_ar_reload_04_charge.wav");
        // Cuchillo: corte al aire o contra la pared, y golpe a un blanco.
        AudioClip[] cuchilloAire = Clips("Cuchillo/knife-solo.wav");
        AudioClip[] cuchilloGolpe = Clips("Cuchillo/knife-stab.wav");

        if (new[] { disparoRifle, recargaRifle, disparoRevolver, recargaRevolver,
                sniper1P, sniper3P, sniperLejos, recargaSniper, cuchilloAire, cuchilloGolpe }.Contains(null))
            return;

        GameObject raiz = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Dictionary<WeaponId, GameObject> armas = ArmasDelPrefab(raiz);
            var sonidos = new Dictionary<WeaponId, WeaponShotAudio>
            {
                // Mismos disparos en AK y M4, con distinto tono para que no suenen iguales.
                [WeaponId.Revolver] = SonidoDeArma(armas, WeaponId.Revolver,
                    disparoRevolver, disparoRevolver, disparoRevolver, recargaRevolver, 0.97f, 1.03f, 3f, 100f),
                [WeaponId.AK] = SonidoDeArma(armas, WeaponId.AK,
                    disparoRifle, disparoRifle, disparoRifle, recargaRifle, 0.9f, 0.96f, 3f, 120f),
                [WeaponId.M4] = SonidoDeArma(armas, WeaponId.M4,
                    disparoRifle, disparoRifle, disparoRifle, recargaRifle, 1.02f, 1.08f, 3f, 120f),
                [WeaponId.Sniper] = SonidoDeArma(armas, WeaponId.Sniper,
                    sniper1P, sniper3P, sniperLejos, recargaSniper, 0.97f, 1.03f, 5f, 150f),
                [WeaponId.Knife] = SonidoDeArma(armas, WeaponId.Knife,
                    cuchilloAire, cuchilloAire, cuchilloAire, new AudioClip[0], 0.95f, 1.05f, 1f, 25f,
                    cuchilloGolpe),
            };

            // La pistola ya estaba configurada a mano: se respeta.
            if (armas.TryGetValue(WeaponId.Pistol, out GameObject pistola) &&
                pistola.GetComponent<WeaponShotAudio>() != null)
                sonidos[WeaponId.Pistol] = pistola.GetComponent<WeaponShotAudio>();

            var audio = new SerializedObject(raiz.GetComponent<PlayerWeaponAudio>());
            SerializedProperty lista = audio.FindProperty("weaponSounds");
            lista.arraySize = sonidos.Count;
            int i = 0;
            foreach (KeyValuePair<WeaponId, WeaponShotAudio> par in sonidos.OrderBy(p => p.Key))
            {
                SerializedProperty entrada = lista.GetArrayElementAtIndex(i++);
                entrada.FindPropertyRelative("weapon").intValue = (int)par.Key;
                entrada.FindPropertyRelative("sound").objectReferenceValue = par.Value;
            }
            audio.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }

        Debug.Log("[ANIMACIONES] Sonidos de armas configurados.");
    }

    // Los objetos de cada arma, tal como los lista NetworkPlayerInventory.
    private static Dictionary<WeaponId, GameObject> ArmasDelPrefab(GameObject raiz)
    {
        var armas = new Dictionary<WeaponId, GameObject>();
        var inventario = new SerializedObject(raiz.GetComponent<NetworkPlayerInventory>());
        SerializedProperty visuales = inventario.FindProperty("weaponVisuals");
        for (int i = 0; i < visuales.arraySize; i++)
        {
            SerializedProperty v = visuales.GetArrayElementAtIndex(i);
            var arma = v.FindPropertyRelative("Visual").objectReferenceValue as GameObject;
            if (arma != null)
                armas[(WeaponId)v.FindPropertyRelative("WeaponId").intValue] = arma;
        }
        return armas;
    }

    // Pone (o actualiza) el AudioSource y el Weapon Shot Audio del arma. Los
    // pasos de la recarga se reparten en el Reload Time del GunSystem.
    private static WeaponShotAudio SonidoDeArma(
        Dictionary<WeaponId, GameObject> armas,
        WeaponId id,
        AudioClip[] disparo1P,
        AudioClip[] disparo3P,
        AudioClip[] disparoLejos,
        AudioClip[] recarga,
        float tonoMin,
        float tonoMax,
        float distanciaMin,
        float distanciaMax,
        AudioClip[] golpe = null)
    {
        GameObject arma = armas[id];

        AudioSource fuente = arma.GetComponent<AudioSource>();
        if (fuente == null)
            fuente = arma.AddComponent<AudioSource>();
        fuente.playOnAwake = false;

        WeaponShotAudio sonido = arma.GetComponent<WeaponShotAudio>();
        if (sonido == null)
            sonido = arma.AddComponent<WeaponShotAudio>();

        var so = new SerializedObject(sonido);
        Asignar(so.FindProperty("shot1P"), disparo1P);
        Asignar(so.FindProperty("shot3P"), disparo3P);
        Asignar(so.FindProperty("shotFar"), disparoLejos);
        Asignar(so.FindProperty("hitClips"), golpe ?? new AudioClip[0]);

        GunSystem disparo = arma.GetComponent<GunSystem>();
        float tiempo = disparo != null ? disparo.reloadTime * 0.85f : 0f;
        SerializedProperty pasos = so.FindProperty("reloadSteps");
        pasos.arraySize = recarga.Length;
        for (int i = 0; i < recarga.Length; i++)
        {
            SerializedProperty paso = pasos.GetArrayElementAtIndex(i);
            paso.FindPropertyRelative("clip").objectReferenceValue = recarga[i];
            paso.FindPropertyRelative("delay").floatValue =
                recarga.Length > 1 ? tiempo * i / (recarga.Length - 1) : 0f;
        }

        so.FindProperty("pitchMin").floatValue = tonoMin;
        so.FindProperty("pitchMax").floatValue = tonoMax;
        so.FindProperty("minDistance").floatValue = distanciaMin;
        so.FindProperty("maxDistance").floatValue = distanciaMax;
        so.FindProperty("debugLog").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sonido;
    }

    private static void Asignar(SerializedProperty lista, AudioClip[] clips)
    {
        lista.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    // Carga los clips de Assets/Audio. Si falta alguno avisa y devuelve null.
    private static AudioClip[] Clips(params string[] rutas)
    {
        var clips = rutas.Select(r => AssetDatabase.LoadAssetAtPath<AudioClip>(Sonidos + r)).ToArray();
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                Debug.LogError("[ANIMACIONES] No existe " + Sonidos + rutas[i]);
                return null;
            }
        }
        return clips;
    }

    // ---------------------------------------------------------------------
    // Importación
    // ---------------------------------------------------------------------

    /// <summary>Clip de un FBX del pack, que ya viene configurado.</summary>
    private static AnimationClip Clip(string rutaEnElPack)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Pack + rutaEnElPack)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null)
            throw new InvalidOperationException("[ANIMACIONES] Falta el clip " + Pack + rutaEnElPack);
        return clip;
    }

    /// <summary>
    /// Deja el FBX de Mixamo como Humanoid con el avatar del X Bot. Root Transform:
    /// rotación y altura con Bake Into Pose; Position (XZ) sin Bake, así el
    /// clip no arrastra al jugador (lo mueve el Rigidbody).
    /// </summary>
    private static AnimationClip Importar(string ruta, bool loop)
    {
        var importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("[ANIMACIONES] Falta el archivo " + ruta);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(AvatarMixamo).OfType<Avatar>().First();
        importer.importAnimation = true;

        ModelImporterClipAnimation clip = importer.defaultClipAnimations.First();
        clip.name = Path.GetFileNameWithoutExtension(ruta);
        clip.loopTime = loop;
        clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.lockRootPositionXZ = false;
        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();

        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));
        if (!resultado.humanMotion)
            throw new InvalidOperationException("[ANIMACIONES] No quedó Humanoid: " + ruta);
        return resultado;
    }

    // ---------------------------------------------------------------------
    // Capa base
    // ---------------------------------------------------------------------

    /// <summary>
    /// Blend tree de "Locomotion Base" con el parámetro Locomocion:
    /// -2 corre atrás, -1 camina atrás, 0 quieto, 1 camina, 2 corre.
    /// También cambia el clip del salto por el del pack.
    /// </summary>
    private static void ArmarLocomocion(AnimatorController controller, AnimatorStateMachine capaBase)
    {
        AnimatorState estado = Estado(capaBase, "Locomotion Base");
        var arbol = estado.motion as BlendTree;
        if (arbol == null)
        {
            arbol = new BlendTree { name = "Locomocion", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(arbol, controller);
            estado.motion = arbol;
        }

        arbol.blendType = BlendTreeType.Simple1D;
        arbol.blendParameter = "Locomocion";
        arbol.useAutomaticThresholds = false;
        arbol.children = new[]
        {
            Hijo(Clip("Movement/Run/HumanM@Run01_Backward.fbx"), -2f),
            Hijo(Clip("Movement/Walk/HumanM@Walk01_Backward.fbx"), -1f),
            Hijo(Clip("Idles/HumanM@Idle01.fbx"), 0f),
            Hijo(Clip("Movement/Walk/HumanM@Walk01_Forward.fbx"), 1f),
            Hijo(Clip("Movement/Run/HumanM@Run01_Forward.fbx"), 2f)
        };
        EditorUtility.SetDirty(arbol);

        // El estado conserva su nombre; solo cambia el clip.
        Estado(capaBase, "Pistol Jump").motion = Clip("Movement/Jump/HumanM@Jump01.fbx");
    }

    private static ChildMotion Hijo(Motion clip, float umbral) =>
        new ChildMotion { motion = clip, threshold = umbral, timeScale = 1f };

    /// <summary>
    /// Estado "Crouch Idle": entra y sale con el bool Agachado. Adentro usa el
    /// mismo parámetro Locomocion: -1 camina atrás, 0 quieto, 1 camina.
    /// </summary>
    private static void ArmarAgachado(AnimatorController controller, AnimatorStateMachine capaBase)
    {
        if (controller.parameters.All(p => p.name != "Agachado"))
            controller.AddParameter("Agachado", AnimatorControllerParameterType.Bool);

        AnimatorState parado = Estado(capaBase, "Locomotion Base");
        AnimatorState agachado = Estado(capaBase, "Crouch Idle");

        var arbol = agachado.motion as BlendTree;
        if (arbol == null)
        {
            arbol = new BlendTree { name = "Agachado", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(arbol, controller);
            agachado.motion = arbol;
        }

        arbol.blendType = BlendTreeType.Simple1D;
        arbol.blendParameter = "Locomocion";
        arbol.useAutomaticThresholds = false;
        arbol.children = new[]
        {
            Hijo(Importar(AgachadoAtras, true), -1f),
            Hijo(Importar(AgachadoQuieto, true), 0f),
            Hijo(Importar(AgachadoAdelante, true), 1f)
        };
        EditorUtility.SetDirty(arbol);

        AnimatorStateTransition agacharse = Transicion(parado, agachado);
        agacharse.duration = MezclaAgachado;
        agacharse.AddCondition(AnimatorConditionMode.If, 0f, "Agachado");

        AnimatorStateTransition pararse = Transicion(agachado, parado);
        pararse.duration = MezclaAgachado;
        pararse.AddCondition(AnimatorConditionMode.IfNot, 0f, "Agachado");
    }

    // ---------------------------------------------------------------------
    // Capa Upper Body
    // ---------------------------------------------------------------------

    /// <summary>
    /// Cuchillo, armado igual que pistola y rifle: pose mientras WeaponPose
    /// vale 3 y ataque con el trigger Fire desde cualquier estado.
    /// </summary>
    private static void ArmarCuchillo(AnimatorStateMachine capa)
    {
        AnimationClip ataqueClip = Clip("Combat/1H/HumanM@Attack1H01_R.fbx");

        AnimatorState neutral = Estado(capa, "Neutral Upper Body");
        AnimatorState pose = Estado(capa, "Knife Combat Pose");
        AnimatorState ataque = Estado(capa, "Knife Combat Attack");
        pose.motion = Clip("Combat/1H/HumanM@CombatIdle1H01.fbx");
        ataque.motion = ataqueClip;
        ataque.speed = VelocidadAtaqueCuchillo;

        Transicion(neutral, pose)
            .AddCondition(AnimatorConditionMode.Equals, PoseCuchillo, "WeaponPose");
        Transicion(pose, neutral)
            .AddCondition(AnimatorConditionMode.NotEqual, PoseCuchillo, "WeaponPose");

        AnimatorStateTransition atacar = DesdeCualquiera(capa, ataque);
        atacar.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
        atacar.AddCondition(AnimatorConditionMode.Equals, PoseCuchillo, "WeaponPose");

        // Al terminar el ataque vuelve sola a la pose.
        AnimatorStateTransition volver = Transicion(ataque, pose);
        volver.hasExitTime = true;
        volver.exitTime = 0.9f;
    }

    /// <summary>
    /// Granada: con el trigger Throw, desde cualquier estado, lanza con la
    /// mano izquierda y vuelve a la pose del arma que tenga.
    /// </summary>
    private static void ArmarGranada(AnimatorController controller, AnimatorStateMachine capa)
    {
        if (controller.parameters.All(p => p.name != "Throw"))
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);

        AnimationClip clip = Clip("Combat/Grenade/HumanM@ThrowGrenade01_L.fbx");
        AnimatorState lanzar = Estado(capa, "Grenade Throw");
        lanzar.motion = clip;
        lanzar.speed = VelocidadLanzamiento;

        DesdeCualquiera(capa, lanzar).AddCondition(AnimatorConditionMode.If, 0f, "Throw");

        // Pasa por el estado neutro, que ya sabe ir a la pose de cada arma.
        AnimatorStateTransition volver = Transicion(lanzar, Estado(capa, "Neutral Upper Body"));
        volver.hasExitTime = true;
        volver.exitTime = SalidaLanzamiento;
        volver.duration = MezclaLanzamiento;
    }

    // ---------------------------------------------------------------------
    // Ayudas: buscan por nombre antes de crear, para no duplicar.
    // ---------------------------------------------------------------------

    private static AnimatorStateMachine Capa(AnimatorController controller, string nombre) =>
        controller.layers.First(capa => capa.name == nombre).stateMachine;

    private static AnimatorState Estado(AnimatorStateMachine capa, string nombre)
    {
        foreach (ChildAnimatorState hijo in capa.states)
        {
            if (hijo.state.name == nombre)
                return hijo.state;
        }

        return capa.AddState(nombre);
    }

    /// <summary>
    /// Reemplaza la transición entre dos estados por una nueva, sin
    /// condiciones: las agrega quien la pide.
    /// </summary>
    private static AnimatorStateTransition Transicion(AnimatorState desde, AnimatorState hasta)
    {
        foreach (AnimatorStateTransition vieja in desde.transitions)
        {
            if (vieja.destinationState == hasta)
                desde.RemoveTransition(vieja);
        }

        AnimatorStateTransition transicion = desde.AddTransition(hasta);
        transicion.hasExitTime = false;
        transicion.hasFixedDuration = true;
        transicion.duration = 0.15f;
        return transicion;
    }

    /// <summary>Lo mismo, pero desde Any State.</summary>
    private static AnimatorStateTransition DesdeCualquiera(
        AnimatorStateMachine capa,
        AnimatorState hasta)
    {
        foreach (AnimatorStateTransition vieja in capa.anyStateTransitions)
        {
            if (vieja.destinationState == hasta)
                capa.RemoveAnyStateTransition(vieja);
        }

        AnimatorStateTransition transicion = capa.AddAnyStateTransition(hasta);
        transicion.hasExitTime = false;
        transicion.hasFixedDuration = true;
        transicion.duration = 0.05f;
        return transicion;
    }
}
