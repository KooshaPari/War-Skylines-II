using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Game;
using Game.Modding;

namespace Wsii.P1H
{
    /// <summary>
    /// Cities: Skylines II entry point. Verified against the installed Game.dll:
    /// Game.Modding.IMod declares OnLoad(Game.UpdateSystem) and OnDispose().
    /// </summary>
    public sealed class Mod : IMod
    {
        internal const string Tag = "[WsiiP1H]";
        internal const string FolderName = "WsiiP1H";
        internal const string FixtureResource = "Wsii.P1H.commands.txt";

        private static readonly object Gate = new object();
        private static string _logPath;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log("OnLoad enter");

            try
            {
                string dir = ResolveModDirectory();
                Log("mod-dir " + dir + " exists=" + Directory.Exists(dir));
                Log("log-path " + LogPath());

                CommandLedger ledger = ReplayFixture(dir, out int fixtureCount, out string source);
                Log("fixture source=" + source
                    + " accepted=" + ledger.Accepted
                    + " replayed=" + ledger.Replayed
                    + " rejected=" + ledger.RejectedCount
                    + " lines=" + fixtureCount);

                updateSystem.UpdateAt<P1HProbeSystem>(SystemUpdatePhase.GameSimulation);
                Log("system-registered P1HProbeSystem phase=GameSimulation");
            }
            catch (Exception ex)
            {
                Log("OnLoad-ERROR " + ex.GetType().Name + ": " + ex.Message);
            }

            Log("OnLoad exit");
        }

        public void OnDispose()
        {
            Log("OnDispose");
        }

        /// <summary>
        /// The game loads managed mods without a usable <see cref="Assembly.Location"/> (observed:
        /// empty, which silently resolved to the install root). Prefer a real assembly path only
        /// when it actually contains our manifest, otherwise use the Unity data path.
        /// </summary>
        internal static string ResolveModDirectory()
        {
            string asmDir = null;
            try
            {
                string location = typeof(Mod).Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    asmDir = Path.GetDirectoryName(location);
                }
            }
            catch (Exception)
            {
                asmDir = null;
            }

            if (!string.IsNullOrEmpty(asmDir) && File.Exists(Path.Combine(asmDir, "mod.json")))
            {
                return asmDir;
            }

            return Path.Combine(DataDirectory(), "Mods", FolderName);
        }

        internal static string DataDirectory()
        {
            try
            {
                string persistent = UnityEngine.Application.persistentDataPath;
                if (!string.IsNullOrEmpty(persistent))
                {
                    return persistent;
                }
            }
            catch (Exception)
            {
                // Application is unavailable outside Unity; fall through.
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "LocalLow",
                "Colossal Order",
                "Cities Skylines II");
        }

        internal static string LogPath()
        {
            if (_logPath == null)
            {
                _logPath = Path.Combine(ResolveModDirectory(), "p1h.log");
            }

            return _logPath;
        }

        internal static void Log(string message)
        {
            string line = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + " " + Tag + " " + message;

            lock (Gate)
            {
                try
                {
                    string path = LogPath();
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Logging must never break the mod; Player.log still carries UnityEngine output.
                }
            }

            UnityEngine.Debug.Log(line);
        }

        private static CommandLedger ReplayFixture(string dir, out int lineCount, out string source)
        {
            var types = new List<string> { "noop", "transfer", "produce", "project", "purchase" };
            var ledger = new CommandLedger(types, 1);
            lineCount = 0;
            source = "none";

            string[] lines = ReadEmbeddedFixture();
            if (lines != null)
            {
                source = "embedded:" + FixtureResource;
            }
            else
            {
                string fixture = Path.Combine(dir, "fixtures", "commands.txt");
                if (File.Exists(fixture))
                {
                    lines = File.ReadAllLines(fixture);
                    source = "file:" + fixture;
                }
            }

            if (lines == null)
            {
                return ledger;
            }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                lineCount++;
                string[] parts = line.Split('|');
                if (parts.Length < 5)
                {
                    Log("fixture-malformed line=" + line);
                    continue;
                }

                long tick = long.Parse(parts[2], CultureInfo.InvariantCulture);
                int revision = int.Parse(parts[3], CultureInfo.InvariantCulture);
                var envelope = new Envelope(parts[0], parts[1], tick, revision, parts[4]);
                AdmissionResult result = ledger.Admit(envelope, currentTick: 0);
                Log("admit id=" + envelope.Id + " type=" + envelope.Type + " result=" + result);
            }

            return ledger;
        }

        private static string[] ReadEmbeddedFixture()
        {
            try
            {
                Assembly asm = typeof(Mod).Assembly;
                using (Stream stream = asm.GetManifestResourceStream(FixtureResource))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var lines = new List<string>();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lines.Add(line);
                        }

                        return lines.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Log("fixture-embedded-error " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }
    }
}
