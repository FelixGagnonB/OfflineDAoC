using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// Lets a tool outside the game drive the 8v8 mode on a running server without a game client:
    /// it writes <c>pvp-request.json</c> beside the server, the server does the work on the game
    /// loop and answers in <c>pvp-result.json</c>. The same pattern as the launcher's bot-world
    /// request. Scrims (roadmap M2) and build checks (M1.2) both use it.
    ///
    /// Request: { "Id": "any new text", "Command": "audit-builds", "Args": { "Build": "hero" } }
    /// Result:  { "Id": ..., "Command": ..., "Ok": true, "Error": null, "Data": ... }
    /// </summary>
    public static class PvpControlChannel
    {
        public const int PollIntervalMilliseconds = 500;

        public sealed record Request(string Id, string Command, Dictionary<string, string> Args);
        public sealed record Result(string Id, string Command, bool Ok, string Error, object Data, DateTime FinishedUtc);

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpControlChannel));
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
        private static Timer _timer;
        private static string _lastRequestId = string.Empty;
        private static int _busy;

        public static string RequestPath => Path.Combine(AppContext.BaseDirectory, "pvp-request.json");
        public static string ResultPath => Path.Combine(AppContext.BaseDirectory, "pvp-result.json");

        [GameServerStartedEvent]
        public static void Start(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            _lastRequestId = string.Empty;
            _timer = new Timer(_ => Poll(), null, 2_000, PollIntervalMilliseconds);
        }

        [GameServerStoppedEvent]
        public static void Stop(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            _timer = null;
        }

        private static void Poll()
        {
            if (Volatile.Read(ref _busy) != 0 || !File.Exists(RequestPath))
                return;
            Request request;
            try
            {
                using var stream = new FileStream(RequestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                request = JsonSerializer.Deserialize<Request>(stream, Json);
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                return;   // half-written file: the next poll reads it whole
            }
            if (request == null || string.IsNullOrWhiteSpace(request.Id) || request.Id == _lastRequestId ||
                Interlocked.Exchange(ref _busy, 1) != 0)
                return;
            _lastRequestId = request.Id;
            // Bots are created and inspected on the game loop, like every other world change.
            NpcService.Instance.Post(Execute, request);
        }

        private static void Execute(Request request)
        {
            Result result;
            try
            {
                result = new(request.Id, request.Command, true, null, Run(request), DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Log.Error($"8v8 control request '{request.Command}' failed", exception);
                result = new(request.Id, request.Command, false, exception.ToString(), null, DateTime.UtcNow);
            }
            try
            {
                string temporary = ResultPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(result, Json));
                File.Move(temporary, ResultPath, true);
            }
            catch (Exception exception)
            {
                Log.Error("8v8 control result could not be written", exception);
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }

        private static object Run(Request request)
        {
            string Arg(string name) => request.Args != null && request.Args.TryGetValue(name, out string value) ? value : null;
            switch (request.Command?.ToLowerInvariant())
            {
                case "ping":
                    return new { Builds = PvpBuildCatalog.Builds.Count, Groups = PvpBuildCatalog.Groups.Select(group => group.Id) };

                case "audit-builds":
                {
                    // Each bot is built, described and dropped. It never enters the world.
                    string only = Arg("Build");
                    IEnumerable<PvpBuild> builds = only == null
                        ? PvpBuildCatalog.Builds
                        : [PvpBuildCatalog.Find(only) ?? throw new ArgumentException($"No build named '{only}'.")];
                    return builds.Select(build => PvpBuildAudit.Describe(new GameBot(build))).ToList();
                }

                case "spawn-group":
                {
                    // Args: Group (a PvpBuildCatalog group id), Region, X, Y. The height is taken from the navmesh.
                    PvpGroupTemplate template = PvpBuildCatalog.FindGroup(Arg("Group"))
                        ?? throw new ArgumentException($"No group named '{Arg("Group")}'.");
                    IReadOnlyList<GameBot> bots = PvpArena.SpawnGroup(template,
                        ushort.Parse(Arg("Region") ?? "200"), int.Parse(Arg("X")), int.Parse(Arg("Y")),
                        // RealmRank=5 is rank 5 level 0; the default is rank 1 level 0, where everyone starts.
                        (int.Parse(Arg("RealmRank") ?? "1") - 1) * 10);
                    return bots.Select(bot => new { bot.Name, Build = bot.PvpBuild.Id, Position = $"{bot.X},{bot.Y},{bot.Z}" }).ToList();
                }

                case "arena-move":
                    return new { Moved = PvpArena.MoveGroups(int.Parse(Arg("DX") ?? "0"), int.Parse(Arg("DY") ?? "0")) };

                case "arena-strip":
                    return new { Stripped = PvpArena.StripBuffs(Arg("Name")) };

                case "arena-status":
                    return PvpArena.Status();

                case "arena-clear":
                    return new { Removed = PvpArena.Clear() };

                case "stop-server":
                    // The same orderly stop as typing "exit" in the server console. It runs after
                    // this answer is written, off the game loop the stop is about to end.
                    new Thread(() =>
                    {
                        Thread.Sleep(1_500);
                        try { GameServer.Instance?.Stop(); }
                        catch (Exception exception) { Log.Error("8v8 stop-server failed", exception); }
                        Environment.Exit(0);
                    }) { IsBackground = false, Name = "PvpStopServer" }.Start();
                    return new { Stopping = true };

                default:
                    throw new ArgumentException($"Unknown 8v8 command '{request.Command}'.");
            }
        }
    }
}
