# STARBEAK: GALACTIC REBELLION — WORK PROGRESS / RESTORE FILE
> Keep this file updated. If a chat dies, open a new chat and say:
> "read PROGRESS.md in C:\Users\DELL\Downloads\Starbeak Galactic Rebellion and continue".
> Last update: 2026-09-23 (session graph: graphics-overhaul + APK)

## STATE @ 10:40 — agent sessions DIED on restart
Prior agents produced ONLY: Assets/Resources/Shaders/*.shader (6), Assets/Resources/VFX/{glow,ring}.png copies.
Everything else (Visuals/*.cs, Flow, builder edits, combat, art swap) NOT done. LauncherManifest fix committed.

## GOAL (current task)
1. Professional near-3D graphics upgrade (real 3D background elements + pre-baked 3D-shaded art + bloom lighting, no pipeline change).
2. Confirm audio works (wiring is done; verify in build).
3. Make gameplay fun (autofire, bosses, hit feedback, juice).
4. Ship a working signed APK at `Builds\Starbeak-Galactic-Rebellion.apk`, then install via adb.

## KEY FACTS ABOUT THIS MACHINE
- Unity 2022.3.30f1: `C:\Program Files\Unity 2022.3.30f1\Editor\Unity.exe`
- Android module (SDK+NDK r23b) bundled INSIDE Unity at
  `C:\Program Files\Unity 2022.3.30f1\Editor\Data\PlaybackEngines\AndroidPlayer\` (SDK, NDK, OpenJDK+keytool). Verified working in prior attempts: scripts compile, IL2CPP, Gradle reaches final tasks.
- Extra staged toolchain: `C:\Users\DELL\UnityAndroid\staging\{SDK,NDK,OpenJDK}` + install scripts in that folder. SDK in Program Files is READ-ONLY (package.xml marshal warnings, harmless). Staged SDK only has platforms android-31/32 → keep targetSdk=32 (set in PlayerSettings via batch script; manifest must NOT contain <uses-sdk>).
- adb: `C:\Program Files\Unity 2022.3.30f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` (no devices connected yet as of last check).
- python 3.12.6, PIL 12.2 available. Internet OK (packages.unity.com reachable).
- Unity batch entrypoints: `StarbeakBatchmodeBuild.BuildContent` then `.BuildApk` (env `STARBEAK_APK_PATH` optional).
  Content build logs previously: `C:\Users\DELL\UnityAndroid\{content-build,unity-content}.log`; APK attempt: `apk-build.log`.
- Git repo initialized (GitHub Desktop by user, remote omariit/Starbeak-Galactic-Rebellion, push in progress by USER — do NOT run git while user may touch it; coordinate commits at end). NOTE: Library/ may already be tracked — should add .gitignore (Library, Temp, Logs, Builds) and `git rm -r --cached` when safe. Also earlier zombie git processes blocked index.lock; killed 2 GitHub-Desktop git commits during session (user may retry — harmless).

## APK BUILD STATUS (from previous attempt, fully diagnosed)
IL2CPP + Gradle got all the way to `:launcher:processReleaseMainManifest FAILED`, exactly 2 merge collisions:
- `android:smallScreens` custom=false vs unityLibrary=true  (Unity-generated lib template)
- `android:label` "Starbeak: Galactic Rebellion" collision (Unity PlayerSettings product name)
**PLANNED FIX (in progress, owned by integration):** add custom
`Assets/Plugins/Android/LauncherManifest.xml` (full launcher manifest incl. tools:replace for smallScreens + label) and strip `<uses-sdk>`/`<supports-screens>`/label conflicts from `Assets/Plugins/Android/AndroidManifest.xml` (keep permissions + activity additions minimal; Unity template already declares activity+intent-filter — the custom AndroidManifest.xml activity DUPLICATES it which is merge-safe but drop <uses-sdk> anyway since 2022 errors on it later).
Other fixes already applied: productName without colon (Burst folder), targetSdk 32, `activeInputHandler: -1 → 2 (Both)` patched in ProjectSettings/ProjectSettings.asset.
Signing: release keystore created at `Assets\Plugins\Android\user-keystore-stub.keystore`? NO — keystore kept OUTSIDE Assets:
`C:\Users\DELL\UnityAndroid\starbeak-release.keystore` alias `starbeak` pass `Starbeak!2026` (keytool -genkeypair RSA2048 10000d). Wire via PlayerSettings.Android.keystorePass/keyaliasPass/keyaliasName in BuildApk.
Splash: set RunInBackground? N/A. Disable splash not possible w/o Pro → fine.
Keystore meta: if placed in Assets it imports as DefaultAsset — keep outside Assets as done.

## CONTENT-BUILD WARNINGS TO FIX (previous run)
- `Sprite 'laser' not found - using fallback.` etc: StarbeakProjectBuilder.LoadSprite FindAssets timing — replace name search with explicit `Assets/Sprites/<name>.png` LoadAssetAtPath (+ AssetDatabase.Refresh before). **owner: A3**

## FILE OWNERSHIP (3 parallel subagents — DO NOT cross-edit)
- A1 VISUALS (own: Assets/Scripts/Visuals/** new, Assets/Shaders/** new, Assets/Scripts/Core/VFXManager.cs edit):
  SimpleBloom (OnRenderImage, Hidden/StarbeakBloom shader, quality-tier aware, null-guard shader),
  VisualDirector self-bootstrap ([RuntimeInitializeOnLoad]) that: attaches bloom to Camera.main on scene changes,
  spawns real-3D procedural asteroid field (low-poly noise spheres, Mobile/Diffuse, Background sorting layer, slow tumble, recycle) + soft directional light + 3-layer parallax star billboards behind gameplay,
  VFXManager API ADDED (keep all existing): `void AddHitFlash(GameObject target, Color tint, float duration=0.12f)` (MaterialPropertyBlock flash for SpriteRenderers, no GC per-frame), engine-trail helper `void AttachEngineTrail(GameObject ship, Transform muzzleParent)`.
  KEEP every existing public member of VFXManager signatures-identical.
- A2 ART (own: Assets/Sprites/**.png ONLY — overwrite pixels, never filenames/.meta):
  regenerate ALL sprites in place with a pseudo-3D render look: top-left key light, strong specular, AO bottom shading, rim light cyan/orange, smooth gradients, transparent AA edges. Names/sizes MUST stay: app_icon512 background1080x1920 boss256 chicken256 egg128x192 feather128x192 glow256 hazard512x256 laser128x384 node_*128 ring256 ship256. Scripts: `C:\Users\DELL\starbeak_art\gen\*.py` (new dir, no repo pollution).
- A3 INTEGRATION/UI/FUN-BASE (own: Assets/Editor/**, Assets/Scripts/UI/**, Core/{GameManager,AudioManager,ObjectPooler}.cs, Persistence/**, Assets/Plugins/Android/**, InputController, PlayerShip ONLY for fields it needs from contract):
  ObjectPooler: + `bossPrefab` field + `SpawnBoss/ReturnBoss`; builder: chicken boss prefab (ChickenBoss, isBoss via serialized? Chicken has NO public setter for isBoss/variant → A3 must ADD to Chicken.cs? NO — Chicken.cs is B-owned. CONTRACT: Chicken.cs exposes exactly:
    `public void ConfigureAsBoss()`,
    `public ChickenVariant Variant`, `public void SetVariant(ChickenVariant v)`
    builder/EnemyAI call these),
  builder: explicit sprite paths, HubBase scene+UI (crafting bench wiring to HubBaseController fields), settings panel real controls (volumes/master/autofire/haptics/drag-mode), quality tier select,
  manifest LauncherManifest.xml fix + strip uses-sdk/supports-screens from AndroidManifest.xml,
  BuildApk: keystore wiring, targetSdk 32, minSdk 29, IL2CPP arm64 only, activeInputHandler Both,
  SaveSystem settings fields + clamp, AudioManager ducking on boss,
  MainMenuController resource bar live + version label, HUDController combo/clear banners.
- B COMBAT FUN (own: Player/PlayerShip.cs (keep public API), AI/**, Weapons/WeaponInstance.cs+WeaponMods.cs, AI/{Chicken,EnemyAIManager,EnemyProjectile,HazardArea}.cs, Weapons/Projectile+StatusEffect):
  autofire ON during gameplay (InputController: fireZone touch removed? NO — InputController is A3-owned. Contract: PlayerShip.IsFiring => InputController.Instance.IsFiring || Settings.autofire-default true via constant in PlayerShip);
  Chicken contact damage stagger/knockback pulse, hit flash calls VFXManager.AddHitFlash, elite weapons get distinct colors + telegraph warning (boss roar + sprite flash for 1s before boss shots),
  EnemyAIManager: real BOSS: in SpawnFleet if node is boss → pooler.SpawnBoss at 0,650 + adds elite weapon cycle, boss health 12x already,
  WeaponInstance: bullet speed 1500, slight screen-shock on kill (AddTrauma 0.05), combo counter event `public static event Action<int> KillStreak` on GameManager? NO — GameManager A3-owns. Put `public static event Action<int> EnemyKilled` on EnemyAIManager; HUD (A3) subscribes.
  All audio hooks keep names: sfx_laser/hit/explosion/player_hit/player_death/pickup/ui_click/ui_back/craft/hazard/warning/boss_roar/boss_explosion, music_menu, music_combat.

## HARD RULES FOR ALL AGENTS
- Unity asmdef: all scripts live under Assets/Scripts/** (namespace StarbeakGalacticRebellion). Visual files: SAME namespace to avoid asmdef refs.
- No `git` commands. No launching Unity. No editing files outside your list. No new Assets dirs except your own. Keep code compiles-with-Unity-2022 (no URP/HDRP APIs, no LINQ-heavy, no async). Mobile perf: no allocations in Update hot paths (cache components, pre-warm).
- Zero serialized-field renames of existing names used by Assets/Editor/StarbeakProjectBuilder.cs (it re-runs BuildContent and overwrites scenes/prefabs — any scene object you hand-create in scenes will be CLOBBERED: everything must be created either by builder code or self-bootstrapping scripts).

## STATUS BOARD
[x] .gitignore + untrack Library/Temp/Logs/Builds
[x] LauncherManifest.xml merge fix committed (label + smallScreens)
[x] activeInputHandler=2, sprite metas PPU1/Simple committed
[x] keystore C:\Users\DELL\UnityAndroid\starbeak-release.keystore alias=starbeak pass=Starbeak!2026
[x] Art sprites regenerated (old session gen_sprites.py, 21 Sep — verify quality at end)
[x] Assets/Resources/Shaders/*.shader (6) + VFX copies exist
[~] Agent sessions DIED repeatedly → implementing directly (no more delegation)
TODO direct: GameManager LoadScene+VictoryBanner | BatchmodeBuild keystore | builder camera farClip+audioListener+explicit sprites | VFXManager AddHitFlash/AttachEngineTrail/SpawnBossTelegraph | Visuals BloomStack+VisualWorld | EnemyAIManager EnemyKilled/BossActive+ConfigureAsBoss+autofire | builder Hub scene + Settings sliders | content build → APK loop → adb install

## COMMANDS
$U='C:\Program Files\Unity 2022.3.30f1\Editor\Unity.exe'; $P='C:\Users\DELL\Downloads\Starbeak Galactic Rebellion'
Content: & $U -batchmode -nographics -quit -projectPath $P -executeMethod StarbeakBatchmodeBuild.BuildContent -logFile 'C:\Users\DELL\UnityAndroid\content-build2.log'
APK: & $U -batchmode -nographics -quit -projectPath $P -executeMethod StarbeakBatchmodeBuild.BuildApk -logFile 'C:\Users\DELL\UnityAndroid\apk-build2.log'
Install: adb install -r "Builds\Starbeak-Galactic-Rebellion.apk"
(Unity batch takes 5–15 min/run; run with generous timeout, one Unity at a time.)
