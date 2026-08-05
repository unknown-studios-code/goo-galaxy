using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Shared.Events
{
    /// <summary>
    /// The static bus carrying in-match facts across assembly boundaries.
    /// Every event is raised only through its <c>Raise*</c> method, so a subscriber can never invoke one.
    /// </summary>
    /// <remarks>
    /// Subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c> with a named handler: the bus outlives
    /// every subscriber, so a missed unsubscription survives scene loads. Domain reload is disabled in this
    /// project, which is why <see cref="ResetEvents"/> clears every field on subsystem registration.
    /// </remarks>
    public static class MatchEvents
    {
        /// <summary>Raised once the match configuration is settled and gameplay systems may initialize.</summary>
        public static event Action<MatchConfiguration> MatchStarted;

        /// <summary>Raised after the board has been generated, carrying the grid gameplay systems should read.</summary>
        public static event Action<IHexGrid> GridInitialized;

        /// <summary>
        /// Raised when the match enters a new phase. The payload is the phase identifier; it becomes a
        /// dedicated enum once the match-flow system owns the phase sequence.
        /// </summary>
        public static event Action<int> GamePhaseChanged;

        /// <summary>Raised whenever a player's energy total changes, carrying the player id and the new total.</summary>
        public static event Action<int, float> EnergyChanged;

        /// <summary>
        /// Raised on every spend attempt, carrying the player id, their energy after the attempt, and whether
        /// the spend succeeded. A rejected attempt reports the player's unchanged total.
        /// </summary>
        public static event Action<int, float, bool> EnergySpent;

        /// <summary>
        /// Raised after a move has been applied to the board, carrying the command and the coordinates whose
        /// contents changed. The coordinate list is owned by the publisher and is only valid for the duration
        /// of the callback; subscribers must copy it rather than retain the reference, and must read it with an
        /// indexed <c>for</c> loop — <c>foreach</c> over the interface boxes the backing enumerator, one
        /// allocation per subscriber per move.
        /// </summary>
        public static event Action<MoveCommand, IReadOnlyList<HexCoordinates>> MoveExecuted;

        /// <summary>Publishes <see cref="MatchStarted"/>.</summary>
        /// <param name="config">The configuration the match runs with.</param>
        public static void RaiseMatchStarted(MatchConfiguration config)
        {
            MatchStarted?.Invoke(config);
        }

        /// <summary>Publishes <see cref="GridInitialized"/>.</summary>
        /// <param name="grid">The freshly generated board.</param>
        public static void RaiseGridInitialized(IHexGrid grid)
        {
            GridInitialized?.Invoke(grid);
        }

        /// <summary>Publishes <see cref="GamePhaseChanged"/>.</summary>
        /// <param name="phase">The identifier of the phase just entered.</param>
        public static void RaiseGamePhaseChanged(int phase)
        {
            GamePhaseChanged?.Invoke(phase);
        }

        /// <summary>Publishes <see cref="EnergyChanged"/>.</summary>
        /// <param name="playerId">The player whose total changed.</param>
        /// <param name="newEnergy">The player's energy after the change.</param>
        public static void RaiseEnergyChanged(int playerId, float newEnergy)
        {
            EnergyChanged?.Invoke(playerId, newEnergy);
        }

        /// <summary>Publishes <see cref="EnergySpent"/>.</summary>
        /// <param name="playerId">The player who attempted the spend.</param>
        /// <param name="newEnergy">The player's energy after the attempt.</param>
        /// <param name="wasSuccessful">Whether the energy was actually deducted.</param>
        public static void RaiseEnergySpent(int playerId, float newEnergy, bool wasSuccessful)
        {
            EnergySpent?.Invoke(playerId, newEnergy, wasSuccessful);
        }

        /// <summary>Publishes <see cref="MoveExecuted"/>. Called only after the board has been mutated.</summary>
        /// <param name="command">The move that was applied.</param>
        /// <param name="affectedCoordinates">
        /// The coordinates whose contents changed. Owned by the caller and only valid for the duration of the
        /// dispatch, so subscribers must copy what they intend to keep.
        /// </param>
        public static void RaiseMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            MoveExecuted?.Invoke(command, affectedCoordinates);
        }

        /// <summary>
        /// Drops every subscriber. Runs automatically on subsystem registration because domain reload is
        /// disabled, and is called by tests to isolate fixtures from one another.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetEvents()
        {
            MatchStarted = null;
            GridInitialized = null;
            GamePhaseChanged = null;
            EnergyChanged = null;
            EnergySpent = null;
            MoveExecuted = null;
        }
    }
}
