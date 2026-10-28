// Dumps tick-by-tick state of every CS2 demo in a folder, so the upstream net9.0 build
// and the netstandard2.1 (Unity) build can be compared line by line.
// Shared by the console runners (Program.cs) and the Unity batch-mode runner.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DemoFile;
using DemoFile.Game.Cs;

public static class DemoDump
{
    public static async Task RunAsync(string demoDir, string outDir, TextWriter log)
    {
        Directory.CreateDirectory(outDir);

        foreach (var path in Directory.GetFiles(demoDir, "*.dem")
                     .Where(p => !Path.GetFileName(p).StartsWith("deadlock"))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".txt");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var writer = new StreamWriter(outPath, false, new UTF8Encoding(false)) { NewLine = "\n" })
            {
                try
                {
                    await DumpDemo(path, writer);
                }
                catch (Exception e)
                {
                    // Both builds must fail the same way on demos upstream can't parse
                    writer.WriteLine($"EXCEPTION {e.GetType().Name}: {e.Message}");
                }
            }

            var hash = BitConverter.ToString(Sha256(File.ReadAllBytes(outPath))).Replace("-", "").Substring(0, 16);
            log.WriteLine($"{Path.GetFileName(path),-18} {File.ReadLines(outPath).Count(),8} lines  {hash}  {sw.ElapsedMilliseconds,6} ms");
        }
    }

    private static async Task DumpDemo(string path, TextWriter w)
    {
        var demo = new CsDemoParser();
        demo.Source1GameEvents.Source1GameEvent += e => w.WriteLine($"E {demo.CurrentDemoTick.Value} {e.GameEventName}");

        var reader = DemoFileReader.Create(demo, new MemoryStream(File.ReadAllBytes(path)));
        await reader.StartReadingAsync(default);

        var lastTick = 0;
        while (await reader.MoveNextAsync(default))
        {
            WriteTick(demo, w);
            lastTick = demo.CurrentDemoTick.Value;
        }

        // Seeking backwards uses the full-packet snapshots, a separate code path from linear reading
        var seekTick = new DemoTick(lastTick / 2);
        await reader.SeekToTickAsync(seekTick, default);
        w.WriteLine($"SEEK {seekTick.Value}");
        WriteTick(demo, w);
    }

    private static void WriteTick(CsDemoParser demo, TextWriter w)
    {
        // Order-independent hash of every entity's class and position
        var entityCount = 0;
        ulong entityHash = 0;
        foreach (var entity in demo.Entities)
        {
            entityCount++;
            var line = entity is CBaseEntity baseEntity
                ? $"{entity.EntityIndex.Value}:{entity.ServerClass.Name}:{V(baseEntity.Origin)}:{A(baseEntity.Rotation)}"
                : $"{entity.EntityIndex.Value}:{entity.ServerClass.Name}";
            entityHash += BitConverter.ToUInt64(Sha256(Encoding.UTF8.GetBytes(line)), 0);
        }

        w.WriteLine($"T {demo.CurrentDemoTick.Value} {demo.CurrentGameTick.Value} ents={entityCount} hash={entityHash:X16}");

        foreach (var player in demo.Players)
        {
            var pawn = player.PlayerPawn;
            w.WriteLine(string.Join(" ",
                "P",
                player.PlayerName,
                player.SteamID,
                player.Team,
                pawn?.IsAlive,
                pawn?.Health,
                pawn?.ArmorValue,
                pawn != null ? V(pawn.Origin) : "-",
                pawn != null ? A(pawn.EyeAngles) : "-",
                pawn?.ActiveWeapon?.ServerClass.Name ?? "-",
                player.InGameMoneyServices?.Account,
                player.ActionTrackingServices?.MatchStats.Kills,
                player.ActionTrackingServices?.MatchStats.Deaths));
        }
    }

    // Portable to Mono/Unity, which lack SHA256.HashData
    private static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    // Raw bits only: float -> text formatting differs between .NET Core and Mono
    private static string F(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("X8", CultureInfo.InvariantCulture);

    private static string V(Vector v) => $"({F(v.X)},{F(v.Y)},{F(v.Z)})";

    private static string A(QAngle a) => $"({F(a.Pitch)},{F(a.Yaw)},{F(a.Roll)})";
}
