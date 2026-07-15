using System;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Shared.Events
{
    public static class StaticGameEvents
    {
        public static event Action<MatchConfiguration> MatchStarted;
        public static event Action<IHexGrid> GridInitialized;
        public static event Action<int> GamePhaseChanged;
        public static event Action<int, float> EnergyChanged;
        public static event Action<int, float, bool> EnergySpent;

        public static void OnMatchStarted(MatchConfiguration config)
        {
            MatchStarted?.Invoke(config);
        }

        public static void OnGridInitialized(IHexGrid grid)
        {
            GridInitialized?.Invoke(grid);
        }

        public static void OnGamePhaseChanged(int phase)
        {
            GamePhaseChanged?.Invoke(phase);
        }

        public static void OnEnergyChanged(int playerId, float newEnergy)
        {
            EnergyChanged?.Invoke(playerId, newEnergy);
        }

        public static void OnEnergySpent(int playerId, float newEnergy, bool wasSuccessful)
        {
            EnergySpent?.Invoke(playerId, newEnergy, wasSuccessful);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetEvents()
        {
            MatchStarted = null;
            GridInitialized = null;
            GamePhaseChanged = null;
            EnergyChanged = null;
            EnergySpent = null;
        }
    }
}
