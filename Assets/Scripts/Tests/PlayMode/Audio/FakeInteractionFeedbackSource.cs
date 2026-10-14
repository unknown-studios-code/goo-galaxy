using System;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Tests.PlayMode.Audio
{
    internal sealed class FakeInteractionFeedbackSource : IInteractionFeedbackSource
    {
        private Action<InteractionFeedback> _feedbackRaised;

        public event Action<InteractionFeedback> FeedbackRaised
        {
            add
            {
                _feedbackRaised += value;
                SubscriberCount++;
            }
            remove
            {
                _feedbackRaised -= value;
                SubscriberCount--;
            }
        }

        public int SubscriberCount { get; private set; }

        public void Raise(InteractionFeedback feedback)
        {
            _feedbackRaised?.Invoke(feedback);
        }
    }
}
