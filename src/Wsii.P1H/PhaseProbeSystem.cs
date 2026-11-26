using Game;

namespace Wsii.P1H
{
    /// <summary>
    /// Common heartbeat body. Subclasses fix the update phase and the log cadence so a log line
    /// states which phase actually invoked the system.
    /// </summary>
    public abstract class PhaseProbeSystem : GameSystemBase
    {
        private int _ticks;

        protected abstract string Phase { get; }

        protected abstract int Heartbeat { get; }

        protected override void OnUpdate()
        {
            _ticks++;

            if (_ticks % Heartbeat == 0)
            {
                Mod.Log("heartbeat phase=" + Phase + " ticks=" + _ticks);
            }
        }
    }

    /// <summary>
    /// Registered by <see cref="Mod.OnLoad"/> at <see cref="SystemUpdatePhase.GameSimulation"/>.
    /// Only ticks while a city is simulating.
    /// </summary>
    public sealed class P1HProbeSystem : PhaseProbeSystem
    {
        protected override string Phase
        {
            get { return "GameSimulation"; }
        }

        protected override int Heartbeat
        {
            get { return 256; }
        }
    }

    /// <summary>
    /// Registered at <see cref="SystemUpdatePhase.LateUpdate"/>, which runs even with no city
    /// loaded. This exists to prove that <c>UpdateSystem</c> actually invokes our systems rather
    /// than merely accepting the registration; it is not evidence of simulation.
    /// </summary>
    public sealed class P1HMenuProbeSystem : PhaseProbeSystem
    {
        protected override string Phase
        {
            get { return "LateUpdate"; }
        }

        protected override int Heartbeat
        {
            get { return 600; }
        }
    }
}
