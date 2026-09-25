# STARBEAK: GALACTIC REBELLION — WORK PROGRESS / RESTORE FILE
> Restore: open a new chat and say "read PROGRESS.md in C:\Users\DELL\Downloads\Starbeak Galactic Rebellion and continue".
> Last update: 2026-09-25 20:30 (v1.0.6 APK built: one-second crash root-caused and fixed)

## CURRENT STATE (2026-09-25)
APK v1.0.6 (versionCode 6) BUILT, RELEASE-SIGNED (CN=Starbeak), v2 signature,
launcher entry + icon present, GLES3 pinned, 25.08 MB at
`Builds\Starbeak-Galactic-Rebellion.apk`. Commit 3d3d428.
-> USER MUST TEST ON PHONE. This version fixes the "shows space+rocket+sound
   for ~1s then closes" bug that persisted through v1.0.3-v1.0.5.

## ROOT CAUSE OF THE ONE-SECOND CRASH (found 2026-09-25)
Reproduced locally by building a Windows standalone of the same project
(`StarbeakWindowsProbe.cs`, Mono backend) and watching it die. Native crash in
UnityPlayer.dll right after AudioManager.Awake started menu music. Binary-searched
the boot path by disabling subsystems one at a time (command-line switches in
CrashLog.HasArgument): the crash vanished only when the player object was
disabled, and reappeared the moment PlayerShip.Update ran.
ACTUAL BUG: `PlayerShip.Update` did NOT check the game state, so while the game
sat in Boot/MainMenu it autofired pooled projectiles every frame before combat,
pools and weapon were warm -> native death on the first menu frame.
FIX (Assets/Scripts/Player/PlayerShip.cs:140):
    if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay) return;
Verified: with the guard, the Windows probe runs a full minute without crashing.
The probe switches are command-line gated (`-starbeakNo*`) and INERT on Android,
so every subsystem ships enabled.

## IF IT STILL CRASHES ON DEVICE
1. The in-game diagnostic overlay prints any managed exception in red over the
   screen for 60s (CrashOverlay) - one screenshot is enough to diagnose.
2. Read the crash log on the phone (file manager ->
   Android/data/com.starbeak.galacticrebellion/files/starbeak_log.txt) OR connect
   USB and run:
   `& "C:\Program Files\Unity 2022.3.30f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" logcat -d | Select-String "Unity|AndroidRuntime|FATAL|DEBUG"`
3. Suspects remaining, in order: (a) BloomStack OnRenderImage on that GPU (removed
   from the Boot camera in v1.0.4; gameplay still has NO bloom - add only after
   stability); (b) Particles/Rendering path on low-end GPU.

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
- LOCAL CRASH REPRO (no phone needed): `StarbeakWindowsProbe.BuildProbe` builds a Windows
  standalone of the same project (Mono; IL2CPP for Windows is NOT installed). Run the exe
  from `C:\Users\DELL\UnityAndroid\WinProbe\StarbeakProbe.exe`. This is how the one-second
  crash was root-caused. Probe switches (inert on Android): -starbeakNoAudio -starbeakNoVfx
  -starbeakNoBelt -starbeakNoBanner -starbeakNoParallax -starbeakNoBackground
  -starbeakNoPlayer -starbeakNoMenu -starbeakNoSceneLoad -starbeakNoOverlay.

## GAME FEATURES IMPLEMENTED
Scene-flow roguelite: Boot -> MainMenu -> SectorMap (graph nodes) -> Gameplay -> Hub/Map.
Boss nodes (ConfigureForNode isBoss) spawn boss chicken with ConfigureAsBoss (12x HP, boss sprite,
weapon rotation). Autofire by default. Settings sliders (master/sfx/music), autofire toggle, drag mode,
quality cycle, all persisted through SaveSystem. Hit flash, engine trail, kill banners, belt parallax
field, additive particles.

## GIT
Repo `Starbeak-Galactic-Rebellion` (user remote). I commit with identity
Starbeak/Starbeak <dev@starbeak.local> and do NOT push. Library/ is gitignored.