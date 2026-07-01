using System;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Shared.Events
{
    /// <summary>
    /// Centralized static event bus (Observer pattern) for global game state transitions.
    /// Manages decoupled communication between features such as Match, Board, and HUD.
    /// </summary>
    public static class StaticGameEvents
    {
        public static event Action<MatchConfig> MatchStarted;
        public static event Action<int> PhaseChanged;
        public static event Action<IBoardGrid> GridReady;

        /// <summary>
        /// Safely invokes the MatchStarted event.
        /// </summary>
        /// <param name="config">The starting configuration for the match.</param>
        public static void InvokeMatchStarted(MatchConfig config) => MatchStarted?.Invoke(config);

        /// <summary>
        /// Safely invokes the PhaseChanged event.
        /// </summary>
        /// <param name="phase">The new phase index.</param>
        public static void InvokePhaseChanged(int phase) => PhaseChanged?.Invoke(phase);

        /// <summary>
        /// Safely invokes the GridReady event.
        /// </summary>
        /// <param name="grid">The hex grid instance.</param>
        public static void InvokeGridReady(IBoardGrid grid) => GridReady?.Invoke(grid);

        /// <summary>
        /// Clears all event delegates to prevent memory leaks in the editor when Domain Reload is disabled.
        /// Automatically called during Subsystem Registration.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetEvents()
        {
            MatchStarted = null;
            PhaseChanged = null;
            GridReady = null;
        }
    }
}
