using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Player-side runner for the IL2CPP check. Copies the demos bundled in StreamingAssets/demos
/// (listed in index.txt) into the app's own cache, dumps them to persistentDataPath/out and
/// logs the summary (visible in logcat). The app keeps running and shows the result on screen.
/// </summary>
public class DumpOnStart : MonoBehaviour
{
    private string _status = "S2D: running...";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create() => new GameObject(nameof(DumpOnStart)).AddComponent<DumpOnStart>();

    private async void Start()
    {
        var log = new StringWriter();
        try
        {
            var demos = await CopyBundledDemos();
            var output = Path.Combine(Application.persistentDataPath, "out");
            await Task.Run(() => DemoDump.RunAsync(demos, output, log));
            _status = "S2D_DUMP_OK " + Application.unityVersion + "\n" + log;
            Debug.Log(_status);
        }
        catch (Exception e)
        {
            _status = "S2D_DUMP_FAIL\n" + log + e;
            Debug.LogError(_status);
        }
    }

    // Files pushed with adb into the app's external folder belong to the shell user and the app
    // may not read them (Android 11+), so the demos ship inside the APK instead
    private static async Task<string> CopyBundledDemos()
    {
        var target = Path.Combine(Application.temporaryCachePath, "demos");
        Directory.CreateDirectory(target);

        var index = Encoding.UTF8.GetString(await ReadStreamingAsset("index.txt"));
        foreach (var name in index.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            File.WriteAllBytes(Path.Combine(target, name.Trim()), await ReadStreamingAsset(name.Trim()));
        }

        return target;
    }

    // StreamingAssets live inside the APK on Android, so they can only be read via UnityWebRequest
    private static async Task<byte[]> ReadStreamingAsset(string name)
    {
        using var request = UnityWebRequest.Get($"{Application.streamingAssetsPath}/demos/{name}");
        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            throw new IOException($"Failed to read StreamingAssets/demos/{name}: {request.error}");

        return request.downloadHandler.data;
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 32;
        GUILayout.Label(_status);
    }
}
