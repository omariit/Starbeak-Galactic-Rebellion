using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batchmode-safe driver for StarbeakProjectBuilder.
/// EditorUtility.DisplayDialog returns false under -batchmode, which would make
/// StarbeakProjectBuilder.BuildAll() bail out, so we invoke its private steps
/// directly via reflection in exactly the same order.
///
///   Unity -batchmode -nographics -quit -projectPath ... -executeMethod StarbeakBatchmodeBuild.BuildContent
///   Unity -batchmode -nographics -quit -projectPath ... -executeMethod StarbeakBatchmodeBuild.BuildApk
/// </summary>
public static class StarbeakBatchmodeBuild
{
    private const string BundleId = "com.starbeak.galacticrebellion";
    private const string DefaultApk = @"C:\Users\DELL\Downloads\Starbeak Galactic Rebellion\Builds\Starbeak-Galactic-Rebellion.apk";

    // ------------------------------------------------------------------ content
    public static void BuildContent()
    {
        Log("=== Starbeak content build (batchmode) ===");
        Type t = typeof(StarbeakProjectBuilder);
        try
        {
            EditorApplication.LockReloadAssemblies();
            InvokePrivate(t, "EnsureDirectories");
            InvokePrivate(t, "BuildPrefabs");
            InvokePrivate(t, "BuildBootScene");
            InvokePrivate(t, "BuildMainMenuScene");
            InvokePrivate(t, "BuildSectorMapScene");
            InvokePrivate(t, "BuildGameplayScene");
            InvokePrivate(t, "BuildHubScene");
            InvokePrivate(t, "RegisterScenesInBuildSettings");
            InvokePrivate(t, "ApplyPlayerSettings");
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Log("=== content build OK ===");
        }
        catch (Exception e)
        {
            Log("FATAL content build: " + e);
            throw;
        }
        finally
        {
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    // ------------------------------------------------------------------ apk
    public static void BuildApk()
    {
        Log("=== Starbeak APK build (batchmode) ===");
        try
        {
            // Input System throws at build time if the active input handler is unset (-1);
            // must be resolved before anything touches the Android build pipeline.
            TrySetActiveInputHandlerBoth();

            PlayerSettings.companyName = "StarbeakStudios";
            // Colon is invalid in Windows folder names and breaks the Burst AOT output folder;
            // the on-device app name still comes from AndroidManifest.xml (with the colon).
            PlayerSettings.productName = "Starbeak Galactic Rebellion";
            PlayerSettings.bundleVersion = "1.0.6";
            PlayerSettings.Android.bundleVersionCode = 6;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.applicationIdentifier = BundleId;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;

            // Force GLES3. With the default API list Unity also offers Vulkan, and a broken
            // or old Vulkan driver shows up exactly as "the app shows one frame, plays a
            // sound, then the process dies". GLES3 is universally supported and costs us
            // nothing on this title (2D + additive sprites).
            try
            {
                PlayerSettings.SetGraphicsAPIs(
                    BuildTarget.Android,
                    new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
                Log("Graphics APIs for Android: " +
                    string.Join(",", Array.ConvertAll(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                        a => a.ToString())));
            }
            catch (System.Exception e)
            {
                Log("WARN: could not pin graphics APIs: " + e.Message);
            }

            // Release signing. Keystore was created with Unity's bundled OpenJDK keytool.
            // Without useCustomKeystore=1 Unity silently ships the DEBUG key even when the
            // keystore fields are set — the previous APK was debug-signed for this reason.
            string keystore = @"C:/Users/DELL/UnityAndroid/starbeak-release.keystore";
            if (System.IO.File.Exists(keystore))
            {
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = "Starbeak!2026";
                PlayerSettings.Android.keyaliasName = "starbeak";
                PlayerSettings.Android.keyaliasPass = "Starbeak!2026";

                var useCustom = typeof(PlayerSettings.Android).GetProperty("useCustomKeystore");
                if (useCustom != null)
                {
                    useCustom.SetValue(null, true, null);
                    Log("useCustomKeystore = true");
                }
                else
                {
                    Log("WARNING: useCustomKeystore property not found on this Unity version.");
                }
                Log("Using release keystore " + keystore);
            }
            else
            {
                Log("WARNING: keystore not found - APK will be debug-signed.");
            }

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            Log("Switching active build target to Android...");
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Log("SwitchActiveBuildTarget -> " + switched);

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new Exception("No scenes in Build Settings - run BuildContent first.");
            Log("Scenes: " + string.Join(", ", scenes));

            string apk = Environment.GetEnvironmentVariable("STARBEAK_APK_PATH");
            if (string.IsNullOrEmpty(apk)) apk = DefaultApk;
            EnsureParentDir(apk);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Log("Building APK -> " + apk);
            BuildPipeline.BuildPlayer(options);
            System.Threading.Thread.Sleep(2000);
            if (!System.IO.File.Exists(apk))
                throw new Exception("BuildPlayer did not produce the APK at " + apk);
            long bytes = new System.IO.FileInfo(apk).Length;
            Log("APK produced: " + bytes + " bytes (" + (bytes / 1048576.0).ToString("F2") + " MB)");
            Log("=== APK BUILD OK ===");
        }
        catch (Exception e)
        {
            Log("FATAL apk build: " + e);
            throw;
        }
    }

    // ------------------------------------------------------------------ helpers
    private static void TrySetActiveInputHandlerBoth()
    {
        // 1) Direct property (Unity 2021+ exposes activeInputHandler on PlayerSettings).
        try
        {
            var prop = typeof(PlayerSettings).GetProperty("activeInputHandler");
            if (prop != null)
            {
                // 0 = Old, 1 = New, 2 = Both
                prop.SetValue(null, 2, null);
                Log("active input handler set to Both via property");
                return;
            }
        }
        catch (Exception e) { Log("direct activeInputHandler set failed: " + e.Message); }

        // 2) Reflection fallback through the platform-specific overload.
        try
        {
            Type ihm = null;
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                ihm = asm.GetType("UnityEngine.InputHandlingMode");
                if (ihm != null) break;
            }
            if (ihm == null) { Log("InputHandlingMode type not found; leaving input handler default."); return; }

            System.Reflection.MethodInfo mi = typeof(PlayerSettings).GetMethod(
                "SetPlatformActiveInputHandler", new[] { typeof(BuildTargetGroup), ihm });
            if (mi == null) { Log("SetPlatformActiveInputHandler not found; leaving input handler default."); return; }

            object both = Enum.Parse(ihm, "Both");
            mi.Invoke(null, new object[] { BuildTargetGroup.Android, both });
            Log("active input handler set to Both for Android");
        }
        catch (Exception e) { Log("could not set input handler: " + e.Message); }
    }

    private static void InvokePrivate(Type type, string method)
    {
        MethodInfo mi = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (mi == null) throw new MissingMethodException(type.Name, method);
        Log("  -> " + method);
        mi.Invoke(null, null);
    }

    private static void EnsureParentDir(string path)
    {
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    private static void Log(string msg)
    {
        Debug.Log("[StarbeakBatchmodeBuild] " + msg);
    }
}
