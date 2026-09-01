using System;
using GooGalaxy.Runtime.UI.Views;
using UnityEngine;

namespace GooGalaxy.Tests.PlayMode.Input
{
    // Hand-written double per the testing rules: reports a hand-slot press on demand and answers the discard-zone
    // question against a caller-configured rectangle, mirroring MatchHudView's own contract without a panel
    // behind it.
    internal sealed class FakeHandGestureSource : IHandGestureSource
    {
        public event Action<int> HandSlotPressed;

        public bool IsDiscardZoneArmed { get; private set; }

        public Rect DiscardZoneScreenRect { get; set; }

        public void SetDiscardZoneArmed(bool isArmed)
        {
            IsDiscardZoneArmed = isArmed;
        }

        public bool IsScreenPointInDiscardZone(Vector2 screenPosition)
        {
            return IsDiscardZoneArmed && DiscardZoneScreenRect.Contains(screenPosition);
        }

        public void RaiseHandSlotPressed(int slotIndex)
        {
            HandSlotPressed?.Invoke(slotIndex);
        }
    }
}
