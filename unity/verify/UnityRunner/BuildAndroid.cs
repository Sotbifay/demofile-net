using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Unity.exe -batchmode -buildTarget Android -executeMethod BuildAndroid.Build -apk path/to/out.apk -demos path/to/demos
/// Builds an IL2CPP ARM64 APK (Android 10 minimum, like the PICO G3) that runs DumpOnStart
/// on the CS2 demos bundled into StreamingAssets/demos.
/// </summary>
public static class BuildAndroid
{
    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        var apk = args[Array.IndexOf(args, "-apk") + 1];
        var demoDir = args[Array.IndexOf(args, "-demos") + 1];

        // StreamingAssets can't be listed on Android, so write an index next to the demos
        var bundled = Path.Combine(Application.dataPath, "StreamingAssets", "demos");
        Directory.CreateDirectory(bundled);
        var demos = Directory.GetFiles(demoDir, "*.dem").Where(p => !Path.GetFileName(p).StartsWith("deadlock")).ToArray();
        foreach (var demo in demos)
        {
            File.Copy(demo, Path.Combine(bundled, Path.GetFileName(demo)), overwrite: true);
        }
        File.WriteAllText(Path.Combine(bundled, "index.txt"), string.Join("\n", demos.Select(Path.GetFileName)));
        AssetDatabase.Refresh();

        const string scenePath = "Assets/Main.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), scenePath);

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.sotbifay.s2dverify");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = apk,
            target = BuildTarget.Android,
            options = BuildOptions.Development,
        });

        var errors = report.steps.SelectMany(s => s.messages).Where(m => m.type is LogType.Error or LogType.Exception);
        Debug.Log($"S2D_BUILD {report.summary.result} {report.summary.totalSize} bytes in {report.summary.totalTime}\n"
                  + string.Join("\n", errors.Select(m => m.content)));
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
