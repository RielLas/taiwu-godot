using System;
using Xunit;
using TaiwuGu.Entities;

namespace TaiwuGu.Tests
{
    public class WorldSignalsTests : IDisposable
    {
        public WorldSignalsTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void DirtyTopics_FilterBitmasksCleanly()
        {
            DirtyTopic captured = DirtyTopic.None;
            Action<DirtyTopic> handler = t => captured |= t;
            WorldSignals.OnTopicsChanged += handler;

            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Calendar);
            Assert.True((captured & DirtyTopic.Aperture) != 0);
            Assert.True((captured & DirtyTopic.Calendar) != 0);
            Assert.False((captured & DirtyTopic.NPCs) != 0);

            WorldSignals.OnTopicsChanged -= handler;
            captured = DirtyTopic.None;
            WorldSignals.EmitTopics(DirtyTopic.All);
            Assert.Equal(DirtyTopic.None, captured);
        }

        [Fact]
        public void EmitEscape_ResetsEscapeHandled_AndRespectsCascade()
        {
            bool secondHandlerRan = false;
            int firstCount = 0;

            Action first = () =>
            {
                if (WorldSignals.EscapeHandled) return;
                firstCount++;
                WorldSignals.EscapeHandled = true;
            };
            Action second = () =>
            {
                if (WorldSignals.EscapeHandled) return;
                secondHandlerRan = true;
                WorldSignals.EscapeHandled = true;
            };

            WorldSignals.OnEscapeRequested += first;
            WorldSignals.OnEscapeRequested += second;

            WorldSignals.EscapeHandled = true; // Stale flag must be reset by EmitEscape
            WorldSignals.EmitEscape();

            Assert.Equal(1, firstCount);
            Assert.False(secondHandlerRan);
        }
    }
}
