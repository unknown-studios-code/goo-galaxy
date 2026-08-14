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

        public const string UnitPresenterMissing =
            "UnitPresenter was not injected. Register this component in GameLifetimeScope, or pass a presenter to Construct when building it by hand.";

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

        public const string ConversionBoardUnavailable =
            "ConversionController is missing its GridPresenter or UnitPresenter, or the hex grid is not initialized! Conversions were skipped.";

        public const string ConversionResolveReentered =
            "ConversionController was re-entered mid-dispatch. Queue follow-up landings instead of raising MoveExecuted during event dispatch.";

        public const string ConversionResolvedSubscriberFailed =
            "A ConversionResolved subscriber threw. The conversions themselves were applied and the board is correct; the failing subscriber is the defect.";

        public const string LandingResolvedSubscriberFailed =
            "A LandingResolved subscriber threw. The move and its conversions were applied and the board is correct; the failing subscriber is the defect.";

        public const string AbilityBoardUnavailable =
            "AbilityController is missing its GridPresenter, UnitPresenter or FuseController, or the hex grid is not initialized! "
            + "Landing impacts were skipped.";

        public const string AbilityResolveReentered =
            "AbilityController was re-entered mid-dispatch. Queue follow-up landings instead of raising LandingResolved during event dispatch.";

        public const string AbilityResolvedSubscriberFailed =
            "An AbilityResolved subscriber threw. The impacts themselves were applied and the board is correct; the failing subscriber is the defect.";

        public const string HazardOverwritten =
            "A landing spawned a hazard on a hex that already carried one. The previous duration was discarded and the new one starts fresh.";

        public const string SelfDestructOnDeadUnit =
            "A SelfDestruct impact resolved on a unit that is already dead or unregistered. The impact was skipped; the board is unchanged.";

        public const string HazardWithoutVacatedHex =
            "A SpawnHazard impact resolved on a deployment that vacates no hex. Only a Jump leaves a trail; author it off Clone-only troops and Protocols.";

        public const string SelfDestructWithoutActingUnit =
            "A SelfDestruct impact resolved on a Protocol, which puts no unit on the board. The impact was skipped; re-author the card.";

        public const string FuseWithoutActingUnit =
            "An ArmFuse impact resolved on a deployment with no unit on the board, which is every Protocol. "
            + "There was nothing to arm, so the impact was skipped; author the fuse on a troop instead.";

        public const string DurationUnitMismatch =
            "A card authored an impact whose Duration Unit does not match its type. "
            + "Arm Fuse is measured in Seconds; Apply Status and Spawn Hazard are measured in Action Windows. "
            + "That impact was skipped and the card's remaining impacts still resolved. "
            + "The diagnostic is a bitmask, so the card is not identifiable from here — re-import the card assets to have "
            + "CardDataSO.ValidateAuthoredData name the card and impact index, then fix the Duration Unit field on that asset.";

        public const string FuseControllerMissing =
            "FuseController was not injected. It owns the match's single fuse system, so without it no landing impact resolves at all. "
            + "Register this component in GameLifetimeScope, or pass a controller to Construct when building it by hand.";

        public const string FuseControllerPresenterMissing =
            "FuseController was not injected with a UnitPresenter, so no fuse can tick and an armed unit will sit on the board forever. "
            + "Register FuseController and UnitPresenter in GameLifetimeScope and keep both in the scene.";

        public const string FuseArmedSubscriberFailedFormat =
            "A FuseArmed subscriber threw while arming unit {0} (player {1}). "
            + "The fuse itself is running and the board is correct; the failing subscriber is the defect.";

        public const string FuseExpiredSubscriberFailed =
            "A FuseExpired subscriber threw. The unit was already removed and the board is correct; the failing subscriber is the defect.";

        public const string SpellResolveReentered =
            "AbilityController.ResolveSpell was re-entered mid-dispatch. Queue follow-up deployments instead of resolving them during event dispatch.";

        public const string UnknownImpactEffectType =
            "A card authored an ImpactEffectType the ability resolver does not handle. That impact was skipped; re-author the card or extend AbilityResolver.";

        public const string UnitViewPrefabNotAssigned = "Unit prefab is not assigned on UnitView! Units will not be rendered.";

        public const string CameraFitGridMissing =
            "BoardCameraController received a null grid and cannot frame the board. The board stays at its authored framing.";

        public const string CameraFitRequiresOrthographic =
            "BoardCameraController needs an orthographic camera to frame the board. Set Projection to Orthographic on the camera.";

        public const string EnergyLedgerMissing =
            "UnitPresenter was not injected with an IEnergyLedger, so no move can be paid for and every move is rejected. "
            + "Register EnergyPresenter as IEnergyLedger in GameLifetimeScope and keep an EnergyPresenter in the scene.";

        public const string UnitViewBoardUnavailable =
            "UnitView is missing its GridPresenter, its UnitPresenter, or its unit prefab, or the hex grid is not initialized! Unit visuals cannot be placed.";
    }
}
