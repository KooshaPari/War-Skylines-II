using System;
using System.Collections.Generic;

namespace Wsii.P1H
{
    /// <summary>
    /// Outcome of admitting one command envelope.
    /// </summary>
    public enum Admission
    {
        Accepted,
        Replayed,
        Rejected
    }

    /// <summary>
    /// One command envelope as it crosses the mod boundary. Mirrors the P1-R envelope
    /// (id, type, tick, revision, payload) with no game types so it stays testable offline.
    /// </summary>
    public sealed class Envelope
    {
        public Envelope(string id, string type, long tick, int revision, string payload)
        {
            Id = id;
            Type = type;
            Tick = tick;
            Revision = revision;
            Payload = payload;
        }

        public string Id { get; }

        public string Type { get; }

        public long Tick { get; }

        public int Revision { get; }

        public string Payload { get; }
    }

    public sealed class AdmissionResult
    {
        public AdmissionResult(Admission admission, string reason)
        {
            Admission = admission;
            Reason = reason;
        }

        public Admission Admission { get; }

        public string Reason { get; }

        public override string ToString()
        {
            return Admission + ":" + Reason;
        }
    }

    /// <summary>
    /// Idempotent command admission. Same id + same payload replays; same id + different
    /// payload conflicts; a stale revision or a tick in the past is rejected. Nothing here
    /// mutates game state, so a rejected command can never leave half an action behind.
    /// </summary>
    public sealed class CommandLedger
    {
        private readonly Dictionary<string, Envelope> _seen = new Dictionary<string, Envelope>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownTypes;
        private readonly int _minRevision;

        public CommandLedger(IEnumerable<string> knownTypes, int minRevision)
        {
            if (knownTypes == null)
            {
                throw new ArgumentNullException(nameof(knownTypes));
            }

            _knownTypes = new HashSet<string>(knownTypes, StringComparer.Ordinal);
            _minRevision = minRevision;
        }

        public int Accepted { get; private set; }

        public int Replayed { get; private set; }

        public int RejectedCount { get; private set; }

        public AdmissionResult Admit(Envelope command, long currentTick)
        {
            if (command == null)
            {
                return Reject("null-command");
            }

            if (string.IsNullOrEmpty(command.Id))
            {
                return Reject("missing-id");
            }

            if (!_knownTypes.Contains(command.Type))
            {
                return Reject("unsupported-type:" + command.Type);
            }

            if (command.Tick < currentTick)
            {
                return Reject("past-tick:" + command.Tick + "<" + currentTick);
            }

            if (command.Revision < _minRevision)
            {
                return Reject("stale-revision:" + command.Revision);
            }

            Envelope prior;
            if (_seen.TryGetValue(command.Id, out prior))
            {
                if (string.Equals(prior.Payload, command.Payload, StringComparison.Ordinal)
                    && string.Equals(prior.Type, command.Type, StringComparison.Ordinal))
                {
                    Replayed++;
                    return new AdmissionResult(Admission.Replayed, "duplicate-id");
                }

                return Reject("payload-conflict:" + command.Id);
            }

            _seen.Add(command.Id, command);
            Accepted++;
            return new AdmissionResult(Admission.Accepted, "ok");
        }

        /// <summary>
        /// Landing marker: proves a replayed id did not mutate state twice.
        /// </summary>
        public int Applied
        {
            get { return Accepted; }
        }

        private AdmissionResult Reject(string reason)
        {
            RejectedCount++;
            return new AdmissionResult(Admission.Rejected, reason);
        }
    }
}
