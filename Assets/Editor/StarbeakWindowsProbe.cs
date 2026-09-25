using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class StarbeakWindowsProbe
{
    private const string BundleId = "com.starbeak.galacticrebellion";

    public static void BuildWindows()
    {
        try
        {
            Debug.Log("=== Starbeak Windows probe build ===");

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.companyName = "StarbeakStudios";
            PlayerSettings.productName = "Starbeak Galactic Rebellion";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);

            string outDir = @"C:\Users\DELL\UnityAndroid\WinProbe";
            Directory.CreateDirectory(outDir);

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("No enabled scenes in build settings!");
                return;
            }

            Debug.Log("Scenes: " + string.Join(", ", scenes));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, "StarbeakProbe.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("=== Windows probe result: " + report.summary.result +
                      " size=" + report.summary.totalSize +
                      " errors=" + report.summary.totalErrors);
        }
        catch (Exception e)
        {
            Debug.LogError("Windows probe build EXCEPTION: " + e);
        }
    }
}
