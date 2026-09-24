# STARBEAK: GALACTIC REBELLION — WORK PROGRESS / RESTORE FILE
> Restore: open a new chat and say "read PROGRESS.md in C:\Users\DELL\Downloads\Starbeak Galactic Rebellion and continue".
> Last update: 2026-09-24 17:45 (v1.0.3 APK built, signed, icon+launcher fixed, crash guards in)

## CURRENT STATE (2026-09-24)
APK v1.0.3 (versionCode 3) BUILT, RELEASE-SIGNED (CN=Starbeak), v2 signature,
launcher entry + icon present, 25.2 MB at `Builds\Starbeak-Galactic-Rebellion.apk`.
-> USER MUST TEST ON PHONE. Last known symptom: app closed ~1s after splash (device-only crash).
   Fixes shipped in v1.0.3: managed-only save cipher (removed System.Security.Cryptography
   static-init crash risk), guarded RunStateInit, safe bloom RenderTextures
   (RenderTextureFormat.Default + null guards), no DestroyImmediate at teardown,
   bounded belt-field wrap math (no ANR), CrashLog writes every error to
   `Android/data/com.starbeak.galacticrebellion/files/starbeak_log.txt`.

## IF IT STILL CRASHES ON DEVICE
1. Read the crash log on the phone (file manager -> Android/data/com.starbeak.galacticrebellion/files/starbeak_log.txt)
   OR connect USB and run:
   `& "C:\Program Files\Unity 2022.3.30f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" logcat -d | Select-String "Unity|AndroidRuntime|FATAL|DEBUG"`
2. Suspects remaining, in order: (a) Vulkan vs GLES on that GPU -> set
   `PlayerSettings.SetGraphicsAPIs(BuildTargetGroup.Android, new[]{GraphicsDeviceType.OpenGLES3})`
   in BuildApk; (b) BloomStack OnRenderImage on that GPU -> remove `cameraGo.AddComponent<BloomStack>()`
   from BuildBootScene (bloom is currently only on the Boot camera, which is destroyed on first
   scene load, so gameplay has NO bloom yet; adding it is a follow-up only after stability);
   (c) Particles/Rendering path on low-end GPU.

## WHAT WAS ALREADY FIXED (do not regress)
- Scene flow: GameManager.ChangeState now loads scenes (MainMenu/SectorMap/Gameplay/Hub) and
  re-runs state init via SceneManager.sceneLoaded. Boot scene = managers + player + background.
- `Plugins/Android/res` is OBSOLETE in 2022.3 (build aborts) -> icons live in
  `Assets/Plugins/Android/StarbeakLauncherRes.androidlib/` (res/mipmap-*dpi/ic_launcher*.png + anydpi-v26 XML).
- LauncherManifest.xml MUST keep the MAIN/LAUNCHER intent-filter on UnityPlayerActivity
  (its absence = app installs but has NO drawer entry/icon - that was the "no icon" bug).
- Sprite .meta files: textureType 8, spriteMode 1, spritePixelsToUnits 1, mipmaps on, ETC2.
- Release signing: BuildApk sets PlayerSettings.Android.useCustomKeystore via reflection
  (property exists in 2022.3) + keystore `C:\Users\DELL\UnityAndroid\starbeak-release.keystore`
  (alias starbeak / Starbeak!2026). Verified CN=Starbeak, v2 scheme.

## TOOLCHAIN (verified working)
- Unity: `C:\Program Files\Unity 2022.3.30f1\Editor\Unity.exe`
- Android module bundled in Unity (SDK platforms android-31/32, build-tools 32.0.0, NDK, OpenJDK).
- Build sequence: `StarbeakBatchmodeBuild.BuildContent` (rebuilds all scenes/prefabs) then
  `StarbeakBatchmodeBuild.BuildApk`. APK build ~12-18 min. Logs: C:\Users\DELL\UnityAndroid\apk-buildN.log
- Content build must be re-run after ANY editor script (builder) change.
- adb: ...\AndroidPlayer\SDK\platform-tools\adb.exe (device was not connected at last check).

## GAME FEATURES IMPLEMENTED
Scene-flow roguelite: Boot -> MainMenu -> SectorMap (graph nodes) -> Gameplay -> Hub/Map.
Boss nodes (ConfigureForNode isBoss) spawn boss chicken with ConfigureAsBoss (12x HP, boss sprite,
weapon rotation). Autofire by default. Settings sliders (master/sfx/music), autofire toggle, drag mode,
quality cycle, all persisted through SaveSystem. Hit flash, engine trail, kill banners, belt parallax
field, additive particles.

## GIT
Repo `Starbeak-Galactic-Rebellion` (user remote). I commit with identity
Starbeak/Starbeak <dev@starbeak.local> and do NOT push. Library/ is gitignored.