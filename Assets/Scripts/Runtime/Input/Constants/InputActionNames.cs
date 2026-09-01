namespace GooGalaxy.Runtime.Input.Constants
{
    /// <summary>
    /// Every action map and action name the match's <c>InputActionAsset</c> is expected to declare.
    /// </summary>
    /// <remarks>
    /// <b>A renamed action is caught at wake, not by the compiler.</b> An <c>InputActionAsset</c> is looked up by
    /// string, so an action renamed in the asset cannot fail a build — it resolves to null and the half of the
    /// pointer it drove silently stops reporting, which reads as a dead board rather than as a wiring fault. The
    /// consts below are what let <c>PointerInputView</c> name the missing action in its own error, and what let a
    /// fixture assert the asset's contents against the same strings the runtime asks for. Both only hold while
    /// the value lives here rather than being inlined at its call site — the same discipline
    /// <c>HudSelectors</c> keeps for the HUD's markup, and for the same reason.
    /// </remarks>
    public static class InputActionNames
    {
        /// <summary>The action map enabled for the duration of a match, and disabled with the pointer view.</summary>
        public const string MatchMap = "Match";

        /// <summary>The action reporting where the pointer is, as a <c>Vector2</c> in screen pixels.</summary>
        public const string PointerPosition = "PointerPosition";

        /// <summary>The action reporting whether the pointer is down, as a button.</summary>
        public const string PointerPress = "PointerPress";
    }
}
