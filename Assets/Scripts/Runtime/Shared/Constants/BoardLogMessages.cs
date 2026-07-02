namespace GooGalaxy.Runtime.Shared.Constants
{
    public static class BoardLogMessages
    {
        public const string CannotAddBlockedCoordinateFormat =
            "Cannot add another blocked coordinate with value (0,0) in {0}. Edit the existing (0,0) coordinate first before adding new ones.";

        public const string DuplicateBlockedCoordinatesFormat = "Duplicate blocked coordinates detected in {0}. Deduplicating in Inspector.";

        public const string MultipleGridPresenters = "Multiple GridPresenters detected in the scene! Destroying this duplicate instance to prevent conflicts.";

        public const string UnitMovementControllerMissing = "UnitMovementController reference is missing! Returning empty unit registry.";

        public const string GridLayoutConfigurationMissing = "GridLayout configuration is missing!";

        public const string CellViewPrefabNotAssigned = "CellView prefab is not assigned!";
    }
}
