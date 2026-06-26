namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Defines the drag-and-drop selection states for cards and targetable actions.
    /// Placed in the Shared assembly to prevent circular dependencies between HUD and Input.
    /// </summary>
    public enum DragState
    {
        Idle,
        CardSelected,
        Dragging,
        Previewing,
        SpellTargeting,
    }
}
