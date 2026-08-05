namespace GooGalaxy.Runtime.Shared.Constants
{
    public static class BoardLogMessages
    {
        public const string CannotAddBlockedCoordinateFormat =
            "Cannot add another blocked coordinate with value (0,0) in {0}. Edit the existing (0,0) coordinate first before adding new ones.";

        public const string DuplicateBlockedCoordinatesFormat = "Duplicate blocked coordinates detected in {0}. Deduplicating in Inspector.";

        public const string MultipleGridPresenters = "Multiple GridPresenters detected in the scene! Destroying this duplicate instance to prevent conflicts.";

        public const string GridLayoutConfigurationMissing = "GridLayout configuration is missing!";

        public const string CellViewPrefabNotAssigned = "CellView prefab is not assigned!";

        public const string GridPresenterMissing = "GridPresenter reference is missing or its hex grid is not initialized! Movement cannot be resolved.";

        public const string MoveNotValidatedFormat =
            "MovementResolver received an unvalidated {0} command from {1} to {2}. Run MovementValidator and only resolve a Success result.";

        public const string UnitSpawnFailedFormat = "Unit spawner failed to produce a clone for player {0} at {1}. The board was left unchanged.";

        public const string MoveResolveReentered =
            "MovementResolver was re-entered from a MoveExecuted subscriber. Queue follow-up moves instead of resolving them during event dispatch.";

        public const string UnitRegistrationFailedFormat =
            "Cannot register unit {0}: the hex grid is unavailable or {1} is outside it. The unit was not added to the registry.";

        public const string UnitRegistrationCellOccupiedFormat =
            "Cannot register unit {0} at {1}: unit {2} already occupies that cell. The unit was not added to the registry.";

        public const string UnitUnregistrationFailedFormat =
            "Cannot unregister unit {0}: the hex grid is unavailable, so its cell cannot be released. The unit is still registered.";

        public const string MoveExecutedSubscriberFailed =
            "A MoveExecuted subscriber threw. The move itself was applied and the board is correct; the failing subscriber is the defect.";
    }
}
