using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarbeakGalacticRebellion;

/// <summary>
/// One-click project bootstrap. Builds every scene, prefab, and serialized reference
/// through Unity's own APIs so the generated YAML is always schema-valid.
///
/// Menu: Starbeak > Build Project Content   (Ctrl+Shift+B / Cmd+Shift+B)
/// </summary>
public static class StarbeakProjectBuilder
{
    private const string MenuPath = "Starbeak/Build Project Content %#b";
    private const string PrefabRoot = "Assets/Prefabs";
    private const string SceneRoot = "Assets/Scenes";
    private const string SpriteRoot = "Assets/Sprites";

    private const string PlayerLayer = "PlayerShip";
    private const string EnemyLayer = "Enemy";
    private const string PlayerProjectileLayer = "PlayerProjectile";
    private const string EnemyProjectileLayer = "EnemyProjectile";
    private const string HazardLayer = "HazardArea";
    private const string UILayer = "UI";

    private static readonly string[] SceneNames = { "Boot", "MainMenu", "SectorMap", "Gameplay" };
    private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.18f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.22f, 0.38f, 1f);

    private static Dictionary<string, Sprite> SpriteCache { get; } = new Dictionary<string, Sprite>();
    private static Font UiFont { get; set; }

    [MenuItem(MenuPath, priority = 0)]
    public static void BuildAll()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Build Starbeak Content",
            "Creates all 4 scenes, 7 prefabs, and wires every serialized reference.\nExisting assets with the same names will be overwritten.\n\nContinue?",
            "Build", "Cancel");
        if (!proceed) return;

        EditorApplication.LockReloadAssemblies();
        try
        {
            EnsureDirectories();
            BuildPrefabs();
            BuildBootScene();
            BuildMainMenuScene();
            BuildSectorMapScene();
            BuildGameplayScene();
            RegisterScenesInBuildSettings();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Build Complete",
                "All scenes, prefabs and references are ready.\n\nThe Boot scene is at index 0 in Build Settings.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StarbeakProjectBuilder] Build failed: {e}");
            EditorUtility.DisplayDialog("Build Failed", e.Message, "OK");
        }
        finally
        {
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    [MenuItem("Starbeak/Validate References", priority = 1)]
    public static void ValidateReferences()
    {
        int missing = 0;
        missing += ValidatePrefabReferences();
        missing += ValidateScene("MainMenu", typeof(MainMenuController));
        missing += ValidateScene("SectorMap", typeof(SectorMapUI));
        missing += ValidateScene("Gameplay", typeof(HUDController));

        EditorUtility.DisplayDialog("Validation",
            missing == 0 ? "All references are wired. No gaps found." : $"{missing} problem(s) detected - see console.",
            "OK");
    }

    // ---------------------------------------------------------------- Setup helpers
    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder(PrefabRoot)) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(SceneRoot)) AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    private static Sprite LoadSprite(string name)
    {
        if (SpriteCache.TryGetValue(name, out Sprite cached)) return cached;

        string[] guids = AssetDatabase.FindAssets($"{name} t:sprite");
        Sprite sprite = guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

        if (guids.Length == 0) Debug.LogWarning($"Sprite '{name}' not found - using fallback.");
        SpriteCache[name] = sprite;
        return sprite;
    }

    private static Font GetUiFont()
    {
        if (UiFont != null) return UiFont;
        UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return UiFont;
    }

    private static T LoadComponent<T>(string prefabName) where T : Component
    {
        return AssetDatabase.LoadAssetAtPath<T>($"{PrefabRoot}/{prefabName}.prefab");
    }

    private static GameObject SavePrefab(GameObject source, string name)
    {
        string path = $"{PrefabRoot}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(source, path);
        UnityEngine.Object.DestroyImmediate(source);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // ---------------------------------------------------------------- Prefabs
    private static void BuildPrefabs()
    {
        BuildProjectilePrefab("Projectile", "laser", PlayerProjectileLayer, 0.6f, new Vector2(72f, 320f));
        BuildProjectilePrefab("EnemyProjectile", "egg", EnemyProjectileLayer, 0.6f, new Vector2(80f, 170f));
        BuildProjectilePrefab("Shrapnel", "feather", EnemyProjectileLayer, 0.5f, new Vector2(80f, 160f));
        BuildChickenPrefab();
        BuildHazardPrefab();
        BuildNodeButtonPrefab();
        BuildEdgeLinePrefab();
        Debug.Log("[StarbeakProjectBuilder] 7 prefabs built.");
    }

    private static void BuildProjectilePrefab(string name, string sprite, string layer, float scale, Vector2 colliderSize)
    {
        GameObject go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(layer);
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite(sprite);
        renderer.sortingLayerName = "Projectiles";
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = colliderSize;

        if (name == "Projectile") go.AddComponent<Projectile>();
        else go.AddComponent<EnemyProjectile>();

        SavePrefab(go, name);
    }

    private static void BuildChickenPrefab()
    {
        GameObject go = new GameObject("Chicken");
        go.layer = LayerMask.NameToLayer(EnemyLayer);
        // chicken.png is 256x256 at 1 PPU; 0.75 fits the 1080-wide portrait playfield.
        go.transform.localScale = Vector3.one * 0.75f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite("chicken");
        renderer.sortingLayerName = "Entities";

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        // Dynamic (gravity-free) so kinematic player projectiles register collisions.
        body.bodyType = RigidbodyType2D.Dynamic;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        // Local units (sprite is 256x256); slight inset for fair grazing.
        collider.size = new Vector2(190f, 180f);

        go.AddComponent<Chicken>();
        SavePrefab(go, "Chicken");
    }

    private static void BuildHazardPrefab()
    {
        GameObject go = new GameObject("HazardArea");
        go.layer = LayerMask.NameToLayer(HazardLayer);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite("hazard");
        renderer.sortingLayerName = "Effects";

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        // hazard.png is 512x256 at 1 PPU.
        collider.size = new Vector2(430f, 200f);
        collider.isTrigger = true;

        go.AddComponent<HazardArea>();
        SavePrefab(go, "HazardArea");
    }

    private static void BuildNodeButtonPrefab()
    {
        GameObject go = new GameObject("NodeButton");
        go.layer = LayerMask.NameToLayer(UILayer);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 120f);

        Image image = go.AddComponent<Image>();
        image.sprite = LoadSprite("node_combat");
        image.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject label = new GameObject("Label");
        label.layer = LayerMask.NameToLayer(UILayer);
        label.transform.SetParent(go.transform, false);
        Text text = label.AddComponent<Text>();
        text.font = GetUiFont();
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        SavePrefab(go, "NodeButton");
    }

    private static void BuildEdgeLinePrefab()
    {
        GameObject go = new GameObject("EdgeLine");
        go.layer = LayerMask.NameToLayer(UILayer);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 6f);

        Image image = go.AddComponent<Image>();
        image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        image.color = new Color(0.55f, 0.72f, 1f, 0.65f);
        image.raycastTarget = false;

        SavePrefab(go, "EdgeLine");
    }

    // ---------------------------------------------------------------- Boot scene
    private static void BuildBootScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Parallax nebula background (sits behind everything, scrolls forever)
        AddBackground("SpaceBackground");

        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 960f; // half of the 1920-unit portrait playfield
        camera.transform.position = new Vector3(0f, 0f, -1000f);
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.12f, 1f);
        camera.depth = -1f;

        // ---- Player ship (lives in Boot; DontDestroyOnLoad carries it between scenes)
        GameObject playerGo = new GameObject("PlayerShip");
        playerGo.layer = LayerMask.NameToLayer(PlayerLayer);
        SpriteRenderer playerRenderer = playerGo.AddComponent<SpriteRenderer>();
        playerRenderer.sprite = LoadSprite("ship");
        playerRenderer.sortingLayerName = "Entities";
        playerRenderer.sortingOrder = 5;
        // ship.png is 256x256 at 1 PPU; slight downscale keeps it nimble on screen.
        playerGo.transform.localScale = Vector3.one * 0.85f;

        Rigidbody2D playerBody = playerGo.AddComponent<Rigidbody2D>();
        playerBody.gravityScale = 0f;
        playerBody.bodyType = RigidbodyType2D.Dynamic;
        playerBody.freezeRotation = true;
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D playerCollider = playerGo.AddComponent<BoxCollider2D>();
        playerCollider.size = new Vector2(150f, 190f);
        PlayerShip playerShip = playerGo.AddComponent<PlayerShip>();

        // ---- Singletons (each is DontDestroyOnLoad in its own Awake)
        SaveSystem saveSystem = AddManagerGO<SaveSystem>("SaveSystem");
        GameManager gameManager = AddManagerGO<GameManager>("GameManager");
        AudioManager audioManager = AddManagerGO<AudioManager>("AudioManager");
        VFXManager vfxManager = AddManagerGO<VFXManager>("VFXManager");
        ObjectPooler pooler = AddManagerGO<ObjectPooler>("ObjectPooler");
        WeaponCraftingEngine crafting = AddManagerGO<WeaponCraftingEngine>("WeaponCraftingEngine");
        SectorMapGenerator mapGenerator = AddManagerGO<SectorMapGenerator>("SectorMapGenerator");
        EnemyAIManager aiManager = AddManagerGO<EnemyAIManager>("EnemyAIManager");
        CombatDirector director = AddManagerGO<CombatDirector>("CombatDirector");
        InputController input = AddManagerGO<InputController>("InputController");

        // ---- Wire VFX particle textures
        vfxManager.glowSprite = LoadSprite("glow");
        vfxManager.ringSprite = LoadSprite("ring");

        // ---- Wire pooler prefabs
        pooler.playerProjectilePrefab = LoadComponent<Projectile>("Projectile");
        pooler.enemyProjectilePrefab = LoadComponent<EnemyProjectile>("EnemyProjectile");
        pooler.chickenEnemyPrefab = LoadComponent<Chicken>("Chicken");
        pooler.hazardPrefab = LoadComponent<HazardArea>("HazardArea");
        pooler.shrapnelPrefab = LoadComponent<EnemyProjectile>("Shrapnel");

        // ---- Wire combat director
        director.pooler = pooler;
        director.aiManager = aiManager;
        director.playerShip = playerShip;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"{SceneRoot}/Boot.unity");
        Debug.Log("[StarbeakProjectBuilder] Boot scene built (10 managers + player ship + background).");
    }

    /// <summary>Adds the scrolling nebula quad behind the gameplay.</summary>
    private static void AddBackground(string name)
    {
        Sprite bgSprite = LoadSprite("background");
        if (bgSprite == null) return;

        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(0f, 0f, 900f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = bgSprite;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = -1;
        renderer.drawMode = SpriteDrawMode.Sliced;

        ParallaxBackground parallax = go.AddComponent<ParallaxBackground>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static T AddManagerGO<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        return go.AddComponent<T>();
    }

    // ---------------------------------------------------------------- MainMenu scene
    private static void BuildMainMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AddBackground("SpaceBackground");
        AddUICamera();

        Canvas canvas = AddCanvas("MenuCanvas");
        AddEventSystem();

        RectTransform root = AddPanel("RootPanel", canvas.transform, true);

        // Title
        Text title = AddText("Title", root, 0f, 620f, 900f, 140f, "STARBEAK", 92, TextAnchor.UpperCenter);
        title.color = new Color(0.6f, 0.9f, 1f);
        Text subtitle = AddText("Subtitle", root, 0f, 500f, 900f, 70f, "GALACTIC REBELLION", 44, TextAnchor.UpperCenter);
        subtitle.color = new Color(0.85f, 0.8f, 0.55f);

        // Resource bar
        RectTransform resourceBar = AddPanel("ResourceBar", root, true);
        resourceBar.anchoredPosition = new Vector2(0f, 390f);
        resourceBar.sizeDelta = new Vector2(980f, 90f);
        AddText("ScrapText", resourceBar, -320f, 0f, 300f, 60f, "Scrap 0", 34, TextAnchor.MiddleCenter);
        AddText("YolkText", resourceBar, 0f, 0f, 300f, 60f, "Yolk 0", 34, TextAnchor.MiddleCenter);
        AddText("FeatherText", resourceBar, 320f, 0f, 300f, 60f, "Feathers 0", 34, TextAnchor.MiddleCenter);

        // Buttons
        AddButton("ContinueButton", root, 0f, 150f, 620f, 130f, "CONTINUE");
        AddButton("NewRunButton", root, 0f, -10f, 620f, 130f, "NEW GALAXY");
        AddButton("SettingsButton", root, 0f, -170f, 620f, 130f, "SETTINGS");
        AddButton("QuitButton", root, 0f, -330f, 620f, 130f, "QUIT");

        // Settings sub-panel (hidden by default)
        RectTransform settings = AddPanel("SettingsPanel", root, false);
        AddText("SettingsTitle", settings, 0f, 240f, 800f, 80f, "SETTINGS", 48, TextAnchor.MiddleCenter);
        AddButton("CloseSettingsButton", settings, 0f, -240f, 500f, 110f, "CLOSE");
        settings.gameObject.SetActive(false);

        MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();
        controller.rootPanel = root.gameObject;
        controller.settingsPanel = settings.gameObject;
        controller.continueButton = FindComponent<Button>(canvas, "RootPanel/ContinueButton");
        controller.newRunButton = FindComponent<Button>(canvas, "RootPanel/NewRunButton");
        controller.settingsButton = FindComponent<Button>(canvas, "RootPanel/SettingsButton");
        controller.quitButton = FindComponent<Button>(canvas, "RootPanel/QuitButton");
        controller.scrapText = FindComponent<Text>(canvas, "RootPanel/ResourceBar/ScrapText");
        controller.yolkText = FindComponent<Text>(canvas, "RootPanel/ResourceBar/YolkText");
        controller.featherText = FindComponent<Text>(canvas, "RootPanel/ResourceBar/FeatherText");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"{SceneRoot}/MainMenu.unity");
        Debug.Log("[StarbeakProjectBuilder] MainMenu scene built.");
    }

    // ---------------------------------------------------------------- SectorMap scene
    private static void BuildSectorMapScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AddBackground("SpaceBackground");
        AddUICamera();

        Canvas canvas = AddCanvas("MapCanvas");
        AddEventSystem();

        // Header
        AddText("HeaderText", canvas.transform, 0f, 840f, 1000f, 80f, "GALAXY MAP", 46, TextAnchor.MiddleCenter);

        // Graph container: centered, large enough for 5 tiers
        RectTransform graph = AddPanel("GraphContainer", canvas.transform, true);
        graph.anchoredPosition = Vector2.zero;
        graph.sizeDelta = new Vector2(1000f, 1600f);
        Image graphBg = graph.gameObject.GetComponent<Image>();
        if (graphBg != null) graphBg.color = new Color(0.05f, 0.07f, 0.14f, 0.6f);

        SectorMapUI mapUI = canvas.gameObject.AddComponent<SectorMapUI>();
        mapUI.rootPanel = canvas.gameObject;
        mapUI.graphContainer = graph;
        mapUI.nodeButtonPrefab = LoadComponent<Button>("NodeButton");
        mapUI.edgeLinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/EdgeLine.prefab");
        mapUI.combatIcon = LoadSprite("node_combat");
        mapUI.anomalyIcon = LoadSprite("node_anomaly");
        mapUI.marketIcon = LoadSprite("node_market");
        mapUI.bossIcon = LoadSprite("node_boss");
        mapUI.headerText = FindComponent<Text>(canvas, "HeaderText");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"{SceneRoot}/SectorMap.unity");
        Debug.Log("[StarbeakProjectBuilder] SectorMap scene built.");
    }

    // ---------------------------------------------------------------- Gameplay scene
    private static void BuildGameplayScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AddBackground("SpaceBackground");
        AddUICamera();

        Canvas canvas = AddCanvas("HUDCanvas");
        AddEventSystem();

        // Top bars
        Image hullFill = AddFillBar("HullBar", canvas.transform, 0f, new Color(0.35f, 1f, 0.5f));
        Image shieldFill = AddFillBar("ShieldBar", canvas.transform, -90f, new Color(0.4f, 0.85f, 1f));

        // Resource readout
        RectTransform resourceBar = AddPanel("ResourceBar", canvas.transform, true);
        resourceBar.anchorMin = new Vector2(0.5f, 1f);
        resourceBar.anchorMax = new Vector2(0.5f, 1f);
        resourceBar.pivot = new Vector2(0.5f, 1f);
        resourceBar.anchoredPosition = new Vector2(0f, -210f);
        resourceBar.sizeDelta = new Vector2(900f, 70f);
        AddText("ScrapText", resourceBar, -200f, 0f, 380f, 56f, "0", 32, TextAnchor.MiddleCenter);
        AddText("YolkText", resourceBar, 200f, 0f, 380f, 56f, "0", 32, TextAnchor.MiddleCenter);

        // Sector label
        AddText("SectorLabel", canvas.transform, 0f, -760f, 700f, 60f, "SECTOR", 30, TextAnchor.MiddleCenter);

        // Pause button + overlay
        RectTransform pauseButton = AddButton("PauseButton", canvas.transform, 460f, 880f, 130f, 100f, "II");
        RectTransform overlay = AddPanel("PauseOverlay", canvas.transform, true);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        Image dim = overlay.gameObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        AddText("PausedLabel", overlay, 0f, 100f, 700f, 90f, "PAUSED", 56, TextAnchor.MiddleCenter);
        overlay.gameObject.SetActive(false);

        HUDController hud = canvas.gameObject.AddComponent<HUDController>();
        hud.rootPanel = canvas.gameObject;
        hud.hullFill = hullFill;
        hud.shieldFill = shieldFill;
        hud.scrapText = FindComponent<Text>(canvas, "ResourceBar/ScrapText");
        hud.yolkText = FindComponent<Text>(canvas, "ResourceBar/YolkText");
        hud.sectorLabel = FindComponent<Text>(canvas, "SectorLabel");
        hud.pauseButton = pauseButton.GetComponent<Button>();
        hud.pauseOverlay = overlay.gameObject;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, $"{SceneRoot}/Gameplay.unity");
        Debug.Log("[StarbeakProjectBuilder] Gameplay scene built.");
    }

    // ---------------------------------------------------------------- UI scaffolding
    private static Camera AddUICamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 960f;
        camera.transform.position = new Vector3(0f, 0f, -1000f);
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.12f, 1f);
        camera.depth = 0f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        return camera;
    }

    private static Canvas AddCanvas(string name)
    {
        GameObject go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(UILayer);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = 10;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void AddEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.layer = LayerMask.NameToLayer(UILayer);
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static RectTransform AddPanel(string name, Transform parent, bool withBackground)
    {
        GameObject go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(UILayer);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1000f, 1600f);

        if (withBackground)
        {
            Image image = go.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = true;
        }
        return rect;
    }

    private static Text AddText(string name, Transform parent, float x, float y, float w, float h,
                                string content, int fontSize, TextAnchor anchor)
    {
        GameObject go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(UILayer);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = GetUiFont();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return text;
    }

    private static RectTransform AddButton(string name, Transform parent, float x, float y, float w, float h, string label)
    {
        GameObject go = new GameObject(name);
        go.layer = LayerMask.NameToLayer(UILayer);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);

        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;
        image.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGo = new GameObject("Label");
        labelGo.layer = LayerMask.NameToLayer(UILayer);
        labelGo.transform.SetParent(go.transform, false);
        Text text = labelGo.AddComponent<Text>();
        text.font = GetUiFont();
        text.text = label;
        text.fontSize = 40;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform labelRect = text.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image AddFillBar(string name, Transform parent, float yOffset, Color color)
    {
        // Track (background) + fill image.
        RectTransform track = AddPanel(name, parent, true);
        track.anchorMin = new Vector2(0.5f, 1f);
        track.anchorMax = new Vector2(0.5f, 1f);
        track.pivot = new Vector2(0.5f, 1f);
        track.anchoredPosition = new Vector2(0f, yOffset);
        track.sizeDelta = new Vector2(480f, 44f);
        track.gameObject.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

        RectTransform fill = AddPanel($"{name}Fill", track, true);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Image fillImage = fill.gameObject.GetComponent<Image>();
        fillImage.color = color;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        return fillImage;
    }

    private static T FindComponent<T>(Component root, string path) where T : Component
    {
        Transform t = root.transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    // ---------------------------------------------------------------- Build settings + player
    private static void RegisterScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(SceneNames.Length);
        for (int i = 0; i < SceneNames.Length; i++)
        {
            scenes.Add(new EditorBuildSettingsScene($"{SceneRoot}/{SceneNames[i]}.unity", true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[StarbeakProjectBuilder] Scenes registered in Build Settings (Boot first).");
    }

    private static void ApplyPlayerSettings()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteRoot}/app_icon.png");
        if (icon != null) TrySetApplicationIcon(icon);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        Debug.Log("[StarbeakProjectBuilder] Player settings applied.");
    }

    private static void TrySetApplicationIcon(Texture2D icon)
    {
        // IconGroupKind lives in an assembly this one may not reference directly, so resolve it at runtime.
        Type iconGroupKind = null;
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            iconGroupKind = asm.GetType("UnityEditor.IconGroupKind");
            if (iconGroupKind != null) break;
        }
        if (iconGroupKind == null) { Debug.LogWarning("IconGroupKind not found; skipping icon."); return; }

        object applicationValue = Enum.Parse(iconGroupKind, "Application");
        System.Reflection.MethodInfo setIcons = typeof(PlayerSettings).GetMethod(
            "SetIcons", new[] { iconGroupKind, typeof(Texture2D[]) });
        if (setIcons != null)
        {
            setIcons.Invoke(null, new object[] { applicationValue, new[] { icon } });
        }
        else
        {
            // Fallback: older single-argument overload.
            System.Reflection.MethodInfo legacy = typeof(PlayerSettings).GetMethod(
                "SetIcons", new[] { typeof(Texture2D[]) });
            legacy?.Invoke(null, new object[] { new[] { icon } });
        }
    }

    // ---------------------------------------------------------------- Validation
    private static int ValidatePrefabReferences()
    {
        int missing = 0;
        ObjectPooler pooler = LoadComponent<ObjectPooler>("ObjectPooler");
        if (pooler == null) { Debug.LogError("ObjectPooler prefab missing."); return 1; }

        if (pooler.playerProjectilePrefab == null) { Debug.LogError("ObjectPooler.playerProjectilePrefab unassigned."); missing++; }
        if (pooler.enemyProjectilePrefab == null) { Debug.LogError("ObjectPooler.enemyProjectilePrefab unassigned."); missing++; }
        if (pooler.chickenEnemyPrefab == null) { Debug.LogError("ObjectPooler.chickenEnemyPrefab unassigned."); missing++; }
        if (pooler.hazardPrefab == null) { Debug.LogError("ObjectPooler.hazardPrefab unassigned."); missing++; }
        if (pooler.shrapnelPrefab == null) { Debug.LogError("ObjectPooler.shrapnelPrefab unassigned."); missing++; }
        return missing;
    }

    private static int ValidateScene(string sceneName, Type controllerType)
    {
        string path = $"{SceneRoot}/{sceneName}.unity";
        if (!File.Exists(path)) { Debug.LogError($"Scene missing: {path}"); return 1; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        int missing = 0;
        Component controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren(controllerType, true);
            if (controller != null) break;
        }
        if (controller == null) { Debug.LogError($"{controllerType.Name} not found in {sceneName}."); missing++; }

        EditorSceneManager.CloseScene(scene, true);
        return missing;
    }
}
