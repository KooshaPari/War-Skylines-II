using Game;

namespace Wsii.P1H
{
    /// <summary>
    /// Proof that the mod is live inside the simulation loop. Registered by
    /// <see cref="Mod.OnLoad"/> at <see cref="SystemUpdatePhase.GameSimulation"/>.
    /// </summary>
    public sealed class P1HProbeSystem : GameSystemBase
    {
        private const int Heartbeat = 256;

        private int _ticks;

        protected override void OnUpdate()
        {
            _ticks++;

            if (_ticks % Heartbeat == 0)
            {
                Mod.Log("heartbeat ticks=" + _ticks);
            }
        }
    }
}
