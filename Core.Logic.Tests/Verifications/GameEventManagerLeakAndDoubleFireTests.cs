using System;
using System.Reflection;
using Xunit;
using TaiwuGu.Core;

namespace TaiwuGu.Tests
{
    public class GameEventManagerLeakAndDoubleFireTests
    {
        [Fact]
        public void Test01_SingleSubscriptionNormalFire()
        {
            GameEventManager.ClearAllSubscriptions();
            int fireCount = 0;

            Action<Vector2Int> handler = pos => fireCount++;
            GameEventManager.OnMoveRequested += handler;

            GameEventManager.RaiseRequestMove(new Vector2Int(5, 5));
            GameEventManager.OnMoveRequested -= handler;

            Assert.Equal(1, fireCount);
        }

        [Fact]
        public void Test02_DoubleSubscriptionVulnerabilityReplication()
        {
            GameEventManager.ClearAllSubscriptions();
            int fireCount = 0;

            Action<Vector2Int> handler = pos => fireCount++;

            GameEventManager.OnMoveRequested += handler;
            GameEventManager.OnMoveRequested += handler;

            GameEventManager.RaiseRequestMove(new Vector2Int(2, 3));
            GameEventManager.ClearAllSubscriptions();

            Assert.Equal(2, fireCount);
        }

        [Fact]
        public void Test03_ClearAllSubscriptionsPreventsDoubleFire()
        {
            GameEventManager.ClearAllSubscriptions();
            int fireCount = 0;

            Action<Vector2Int> handler = pos => fireCount++;

            GameEventManager.OnMoveRequested += handler;
            GameEventManager.OnMoveRequested += handler;

            GameEventManager.ClearAllSubscriptions();

            GameEventManager.OnMoveRequested += handler;

            GameEventManager.RaiseRequestMove(new Vector2Int(7, 8));
            GameEventManager.ClearAllSubscriptions();

            Assert.Equal(1, fireCount);
        }

        [Fact]
        public void Test04_SimulatedSceneReloadDanglingListenerPurge()
        {
            GameEventManager.ClearAllSubscriptions();

            int scene1ListenerFireCount = 0;
            int scene2ListenerFireCount = 0;

            Action scene1Handler = () => scene1ListenerFireCount++;
            GameEventManager.OnEscapeRequested += scene1Handler;

            // Scene 1 unloads WITHOUT unsubscribing
            GameEventManager.ClearAllSubscriptions();

            // Scene 2 mounts
            Action scene2Handler = () => scene2ListenerFireCount++;
            GameEventManager.OnEscapeRequested += scene2Handler;

            GameEventManager.TriggerEscape();

            GameEventManager.ClearAllSubscriptions();

            Assert.Equal(0, scene1ListenerFireCount);
            Assert.Equal(1, scene2ListenerFireCount);
        }

        [Fact]
        public void Test05_All66EventsResetToNullSweep()
        {
            GameEventManager.OnMoveRequested += pos => { };
            GameEventManager.OnAdvanceSolarTermRequested += () => { };
            GameEventManager.OnFeedGuRequested += (id, diet, qty) => { };
            GameEventManager.OnSaveGameRequested += slot => { };
            GameEventManager.OnCombatStarted += (enc, name, hp) => { };
            GameEventManager.OnCultivatorCultivationChanged += (r, s, n) => { };
            GameEventManager.OnCauldronOpened += state => { };
            GameEventManager.OnBeginJourneyRequested += () => { };
            GameEventManager.OnReturnToTitleRequested += () => { };

            GameEventManager.ClearAllSubscriptions();

            Type type = typeof(GameEventManager);
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            int checkedEvents = 0;
            int nonNullCount = 0;

            foreach (var field in fields)
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    checkedEvents++;
                    var value = field.GetValue(null);
                    if (value != null)
                    {
                        nonNullCount++;
                    }
                }
            }

            Assert.True(checkedEvents >= 66, $"Expected >= 66 events, found {checkedEvents}");
            Assert.Equal(0, nonNullCount);
        }

        [Fact]
        public void Test06_MonobehaviourOnEnableOnDisablePairing()
        {
            GameEventManager.ClearAllSubscriptions();
            int fireCount = 0;

            Action<int, SolarTerm, string> handler = (y, t, n) => fireCount++;

            GameEventManager.OnSolarTermAdvanced += handler;
            GameEventManager.TriggerSolarTermAdvanced(1, SolarTerm.Lichun_BeginningOfSpring, "立春");

            GameEventManager.OnSolarTermAdvanced -= handler;
            GameEventManager.TriggerSolarTermAdvanced(1, SolarTerm.Yushui_RainWater, "雨水");

            Assert.Equal(1, fireCount);
        }
    }
}
