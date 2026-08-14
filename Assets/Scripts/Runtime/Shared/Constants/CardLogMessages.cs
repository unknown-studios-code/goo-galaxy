namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the card authoring and registry problems a designer has to act on.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="BoardLogMessages" /> because the audience is different: these are read while
    /// authoring an asset in the Inspector, not while debugging a match. Both live in Shared so the text is
    /// stated once and every message that takes arguments carries the <c>Format</c> suffix, so a caller can see
    /// from the name alone that it needs <c>string.Format</c>.
    /// </remarks>
    public static class CardLogMessages
    {
        public const string CardIdEmptyFormat = "{0}: CardId is empty. Assign a unique, stable id before referencing this card in a CardPresenter.";

        public const string DescriptionEmptyFormat = "{0}: Description is empty. The card face renders blank until it is authored.";

        public const string SpellClusterSizeMissingFormat =
            "{0}: landing effect {1} has a Cluster Size of 0 on a Spell. "
            + "On a Protocol that field is the number of hexes the player picks, so this card will be rejected as InvalidTargets. "
            + "Set it to the authored cluster size — 3 for Cryo-Stasis and Purge Pulse, 4 for Sterilization Beam.";

        public const string DurationUnitMismatchFormat =
            "{0}: landing effect {1} disagrees with its Duration Unit. "
            + "Arm Fuse is the only impact measured in Seconds; Apply Status and Spawn Hazard are measured in Action Windows. "
            + "That impact is skipped at runtime, so set the Duration Unit field to match the impact type.";

        public const string DuplicateCardIdFormat = "CardPresenter: duplicate CardId '{0}' on '{1}' was skipped.";
    }
}
