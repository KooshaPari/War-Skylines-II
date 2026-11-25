using System;
using System.Collections.Generic;
using System.IO;
using Wsii.P1H;

namespace Wsii.P1H.Semantics
{
    /// <summary>
    /// Zero-dependency check of the P1-H admission contract. Runs the same fixture the mod
    /// replays at load time, so offline evidence and in-game behavior use identical input.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            bool emit = args != null && args.Length > 0 && args[0] == "emit";

            string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "commands.txt");
            if (!File.Exists(fixture))
            {
                Console.WriteLine("FAIL fixture-missing " + fixture);
                return 1;
            }

            var ledger = new CommandLedger(new[] { "noop", "transfer", "produce", "project", "purchase" }, 1);
            var results = new List<string>();

            foreach (string raw in File.ReadAllLines(fixture))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string[] parts = line.Split('|');
                if (parts.Length < 5)
                {
                    results.Add("Rejected:fixture-malformed");
                    continue;
                }

                var envelope = new Envelope(
                    parts[0],
                    parts[1],
                    long.Parse(parts[2]),
                    int.Parse(parts[3]),
                    parts[4]);

                AdmissionResult result = ledger.Admit(envelope, currentTick: 0);
                results.Add(result.ToString());

                if (emit)
                {
                    Console.WriteLine("VERDICT|" + envelope.Id + "|" + result);
                }
            }

            if (emit)
            {
                return 0;
            }

            Check("line-count", results.Count, 8);
            Check("accepted", ledger.Accepted, 2);
            Check("replayed", ledger.Replayed, 1);
            Check("rejected", ledger.RejectedCount, 5);
            Check("applied-equals-accepted", ledger.Applied, ledger.Accepted);

            Check("r1-accept", results[0], "Accepted:ok");
            Check("r2-replay", results[1], "Replayed:duplicate-id");
            Check("r3-conflict", results[2], "Rejected:payload-conflict:cmd-1");
            Check("r4-type", results[3], "Rejected:unsupported-type:teleport");
            Check("r5-revision", results[4], "Rejected:stale-revision:0");
            Check("r6-past-tick", results[5], "Rejected:past-tick:-5<0");
            Check("r7-accept", results[6], "Accepted:ok");
            Check("r8-missing-id", results[7], "Rejected:missing-id");

            Check("idempotent-replay-does-not-apply", ledger.Accepted, 2);

            Console.WriteLine(_failures == 0
                ? "PASS " + results.Count + " fixture lines, 2 accepted, 1 replayed, 5 rejected"
                : "FAIL " + _failures + " check(s)");

            return _failures == 0 ? 0 : 1;
        }

        private static void Check<T>(string name, T actual, T expected)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            if (!ok)
            {
                _failures++;
            }

            Console.WriteLine((ok ? "ok   " : "FAIL ") + name + " expected=" + expected + " actual=" + actual);
        }
    }
}
