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

            EdgeCases();

            Console.WriteLine(_failures == 0
                ? "PASS " + results.Count + " fixture lines, 2 accepted, 1 replayed, 5 rejected"
                : "FAIL " + _failures + " check(s)");

            return _failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Boundary behaviour that the fixture does not cover: identity rules at their edges, and
        /// the fact that a long run of distinct commands stays accepted without degradation.
        /// </summary>
        private static void EdgeCases()
        {
            var types = new[] { "noop", "transfer" };

            // Same id, same payload, different type is a conflict, not a replay.
            var typeClash = new CommandLedger(types, 1);
            Check("edge-type-clash-first", typeClash.Admit(new Envelope("e1", "noop", 0, 1, "p"), 0).ToString(), "Accepted:ok");
            Check("edge-type-clash-second", typeClash.Admit(new Envelope("e1", "transfer", 0, 1, "p"), 0).ToString(), "Rejected:payload-conflict:e1");

            // Boundary values that must be accepted: tick == currentTick, revision == minRevision.
            var boundary = new CommandLedger(types, 3);
            Check("edge-tick-equals-current", boundary.Admit(new Envelope("b1", "noop", 7, 3, "p"), 7).ToString(), "Accepted:ok");
            Check("edge-revision-at-min", boundary.Admit(new Envelope("b2", "noop", 8, 3, "p"), 7).ToString(), "Accepted:ok");

            // One below each boundary must be rejected.
            Check("edge-tick-below", boundary.Admit(new Envelope("b3", "noop", 6, 3, "p"), 7).ToString(), "Rejected:past-tick:6<7");
            Check("edge-revision-below", boundary.Admit(new Envelope("b4", "noop", 8, 2, "p"), 7).ToString(), "Rejected:stale-revision:2");

            // Extreme numbers must not overflow or alias.
            var extremes = new CommandLedger(types, 1);
            Check("edge-long-max-tick", extremes.Admit(new Envelope("x1", "noop", long.MaxValue, int.MaxValue, "p"), long.MaxValue).ToString(), "Accepted:ok");

            // Opaque ids and payloads survive unchanged.
            var opaque = new CommandLedger(types, 1);
            const string weirdId = "order 42/\u00e9\u4e2d";
            const string weirdPayload = "{\"note\":\"caf\u00e9 \u4e2d\u6587\"}";
            Check("edge-opaque-id", opaque.Admit(new Envelope(weirdId, "transfer", 0, 1, weirdPayload), 0).ToString(), "Accepted:ok");
            Check("edge-opaque-replay", opaque.Admit(new Envelope(weirdId, "transfer", 0, 1, weirdPayload), 0).ToString(), "Replayed:duplicate-id");

            // A thousand distinct commands: every one lands, none replays, state stays bounded.
            var bulk = new CommandLedger(types, 1);
            for (int i = 0; i < 1000; i++)
            {
                bulk.Admit(new Envelope("bulk-" + i, "noop", i, 1, "{}"), 0);
            }

            Check("edge-bulk-accepted", bulk.Accepted, 1000);
            Check("edge-bulk-replayed", bulk.Replayed, 0);
            Check("edge-bulk-rejected", bulk.RejectedCount, 0);

            // Nulls must be rejected, never throw.
            var nulls = new CommandLedger(types, 1);
            Check("edge-null-command", nulls.Admit(null, 0).ToString(), "Rejected:null-command");
            Check("edge-null-command-counted", nulls.RejectedCount, 1);
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
