using System;
using GooGalaxy.Runtime.Input.Interfaces;
using GooGalaxy.Runtime.Input.Models;
using UnityEngine;

namespace GooGalaxy.Tests.PlayMode.Input
{
    // Hand-written double per the testing rules: raises the three PointerSample events a real touch device would,
    // with no Input System behind it, so a fixture drives a press, a move and a release directly.
    internal sealed class FakePointerSource : IPointerSource
    {
        public event Action<PointerSample> PointerPressed;
        public event Action<PointerSample> PointerMoved;
        public event Action<PointerSample> PointerReleased;

        public Vector2 CurrentScreenPosition { get; private set; }

        public bool IsPointerDown { get; private set; }

        public void RaisePressed(Vector2 screenPosition)
        {
            CurrentScreenPosition = screenPosition;
            IsPointerDown = true;
            PointerPressed?.Invoke(new PointerSample(screenPosition, PointerPhase.Pressed, 0f));
        }

        public void RaiseMoved(Vector2 screenPosition)
        {
            CurrentScreenPosition = screenPosition;
            PointerMoved?.Invoke(new PointerSample(screenPosition, PointerPhase.Moved, 0f));
        }

        public void RaiseReleased(Vector2 screenPosition)
        {
            CurrentScreenPosition = screenPosition;
            IsPointerDown = false;
            PointerReleased?.Invoke(new PointerSample(screenPosition, PointerPhase.Released, 0f));
        }
    }
}
