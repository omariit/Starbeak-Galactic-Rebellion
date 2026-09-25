# STARBEAK: GALACTIC REBELLION — WORK PROGRESS / RESTORE FILE
> Restore: open a new chat and say "read PROGRESS.md in C:\Users\DELL\Downloads\Starbeak Galactic Rebellion and continue".
> Last update: 2026-09-25 23:25 (v1.0.7 APK built: font + map fixes, endless galaxies, roguelite weapon upgrades)

## CURRENT STATE (2026-09-25)
APK v1.0.7 (versionCode 7) BUILT, RELEASE-SIGNED (CN=Starbeak), v2 signature,
launcher entry + icon present, GLES3 pinned, 25.61 MB at
`Builds\Starbeak Galactic Rebellion\Builds\Starbeak-Galactic-Rebellion.apk`. Commit fae2841.
-> v1.0.6 fixed the one-second crash (PlayerShip fired pooled projectiles during
   Boot/MainMenu). The app then ran but the SectorMap was broken: no text and the
   entry node was off-screen/untappable. Both are fixed in v1.0.7.

## v1.0.7 FIXES (diagnosed from the "rocket + circles, no text, no taps" report)
1. INVISIBLE TEXT: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` is
   stripped from IL2CPP release builds, so every `Text` rendered blank on-device.
   Fix: ship `Assets/Resources/StarbeakUI.ttf` (Segoe UI, 975 KB). Verified inside
   the APK at `assets/bin/Data/198c8c57f3484570ad34fefa8248938e` (its GUID).
   `StarbeakProjectBuilder.GetUiFont()` and the runtime `UiFont.Get()` provider both
   load it; the legacy font is now only an editor fallback.
2. UNTAPPABLE MAP: `GetNodeWorldPosition` used tier y = 0..1360, but the graph
   container is 1600 tall centered on 0 (half-height 800) -> the tier-0 ENTRY node
   sat 560px above the top edge, off-screen. Since the entry node is the only
   tappable node at run start, the map looked dead. Fix: center the whole graph
   (y = (tierCount-1-tier - (tierCount-1)*0.5) * spacing -> +/-680).

## v1.0.7 FEATURES (user request: endless stages + changing rockets/types)
- ENDLESS GALAXIES: `GameManager.GalaxyIndex` (from `ProfileData.galaxiesCleared`).
  Boss kill -> reward (3 feathers + 120+40*g scrap) -> `DiscardMap()` -> Hub ->
  DEPLOY -> `DeployToNextGalaxy()` (keeps run upgrades) -> fresh, deeper galaxy.
  Deeper galaxies grow tierCount 5..9 (spacing shrinks to fit the container),
  enemies get `activeTier = tier + galaxy` (tankier, more elites), flock descent
  speeds up (16..44), fleets grow +3/galaxy, bosses hit harder.
- ROGUELITE WEAPON UPGRADES (`Assets/Scripts/Weapons/WeaponUpgrades.cs`): clearing a
  non-boss combat node sets `pendingUpgradeOffer`; when the SectorMap opens,
  `WeaponUpgradeUI` shows 3 random upgrades (Twin Barrel +1 projectile, Overcharge
  +30% dmg, Rapid Fire -20% cd, Scatter +18 deg, Piercing Rounds, Mag-Accelerator
  +25% speed). Applied to the live weapon; `WeaponUpgrades.Reapply` puts them back
  after the per-sector weapon rebuild in `PlayerShip.Respawn`; reset only on
  `StartNewRun` (menu). Hub DEPLOY uses `DeployToNextGalaxy` (keeps upgrades).
- Map header now reads "GALAXY n - N SECTORS".

## KEY ARCHITECTURE (do not regress)
- Probe switches in CrashLog.HasArgument ("-starbeakNo*") are INERT on Android.
- Combat flow: HUDController.OnSectorCleared -> GameManager.CompleteNode(nodeId,true)
  -> boss? Hub : SectorMap(+upgrade offer). Death -> RegisterDefeat -> SectorMap.
- PlayerShip.Update returns early unless CurrentState == Gameplay (the v1.0.6 crash fix).

## IF IT STILL FAILS ON DEVICE
1. The in-game diagnostic overlay prints any managed exception in red over the screen
   for 60s (CrashOverlay) - one screenshot is enough to diagnose.
2. Read the crash log on the phone (file manager ->
   Android/data/com.starbeak.galacticrebellion/files/starbeak_log.txt) OR connect USB and run:
   `& "C:\Program Files\Unity 2022.3.30f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" logcat -d | Select-String "Unity|AndroidRuntime|FATAL|DEBUG"`

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