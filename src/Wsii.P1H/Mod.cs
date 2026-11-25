using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        private static readonly object Gate = new object();
        private static string _logPath;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log("OnLoad enter");

            try
            {
                string dir = ModDirectory();
                Log("mod-dir " + dir);

                CommandLedger ledger = LoadFixture(dir, out int fixtureCount);
                Log("fixture admitted accepted=" + ledger.Accepted
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

        internal static string ModDirectory()
        {
            string location = typeof(Mod).Assembly.Location;
            return string.IsNullOrEmpty(location) ? AppDomain.CurrentDomain.BaseDirectory : Path.GetDirectoryName(location);
        }

        internal static void Log(string message)
        {
            string line = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + " " + Tag + " " + message;
            lock (Gate)
            {
                try
                {
                    if (_logPath == null)
                    {
                        _logPath = Path.Combine(ModDirectory(), "p1h.log");
                    }

                    File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Logging must never break the mod; Player.log still carries UnityEngine output.
                }
            }

            UnityEngine.Debug.Log(line);
        }

        private static CommandLedger LoadFixture(string dir, out int lineCount)
        {
            var types = new List<string> { "noop", "transfer", "produce", "project", "purchase" };
            var ledger = new CommandLedger(types, 1);
            lineCount = 0;

            string fixture = Path.Combine(dir, "fixtures", "commands.txt");
            if (!File.Exists(fixture))
            {
                Log("fixture-missing " + fixture);
                return ledger;
            }

            foreach (string raw in File.ReadAllLines(fixture))
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
    }
}
