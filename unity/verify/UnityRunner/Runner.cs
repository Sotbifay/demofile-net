using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class Runner
{
    // Unity.exe -batchmode -executeMethod Runner.Run -demos <dir> -out <dir>
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        var demos = args[Array.IndexOf(args, "-demos") + 1];
        var output = args[Array.IndexOf(args, "-out") + 1];

        var log = new StringWriter();
        try
        {
            // Off the main thread: blocking it would deadlock on UnitySynchronizationContext
            Task.Run(() => DemoDump.RunAsync(demos, output, log)).GetAwaiter().GetResult();
            Debug.Log("S2D_DUMP_OK " + Application.unityVersion + "\n" + log);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("S2D_DUMP_FAIL\n" + log + e);
            EditorApplication.Exit(1);
        }
    }
}
