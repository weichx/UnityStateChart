using System;
using System.Collections.Generic;
using NUnit.Framework;
using Util;
using Callback = System.Action<string>;
using BehaviorMap = System.Collections.Generic.Dictionary<string, System.Action<string>>;

public class StateChart_Spec {

    class SomeEvt : StateChartEvent { }

    class Evt_DoorLock : StateChartEvent { }

    class Evt_DoorUnlock : StateChartEvent {

        public bool hasKey;

        public Evt_DoorUnlock(bool hasKey = true) {
            this.hasKey = hasKey;
        }

    }

    class Evt_DoorClose : StateChartEvent { }

    class Evt_DoorOpen : StateChartEvent { }

    struct EvtPair {

        public Type type;
        public string stateName;
        public Action<string, StateChartEvent> callback;

        public EvtPair(string stateName, Type type, Action<string, StateChartEvent> callback) {
            this.type = type;
            this.stateName = stateName;
            this.callback = callback;
        }

    }

    private static void SetCallbacks(StateChart.StateChartBuilder builder, string stateName, BehaviorMap behaviors) {
        builder.Init(() => CallBehavior(stateName + ":init", behaviors));

        builder.Enter(() => {
            CallBehavior(stateName + ":enter", behaviors);
        });

        builder.Exit(() => {
            CallBehavior(stateName + ":exit", behaviors);
        });

        builder.Update(() => {
            CallBehavior(stateName + ":update", behaviors);
        });
    }

    private static void SetEvents(StateChart.StateChartBuilder builder, string stateName, EvtPair[] eventPairs) {
        if (eventPairs == null) return;

        for (int i = 0; i < eventPairs.Length; i++) {
            EvtPair pair = eventPairs[i];

            if (pair.stateName == stateName) {
                builder.Event(pair.type, (evt) => {
                    pair.callback(stateName + ":" + evt.GetType().Name, evt);
                });
            }
        }
    }

    private static void CallBehavior(string key, BehaviorMap behaviors) {
        if (behaviors != null && behaviors.ContainsKey(key)) {
            behaviors[key](key);
        }
    }

    private StateChart GetChart(BehaviorMap behaviors = null, EvtPair[] events = null) {
        return new StateChart((builder) => {
            Action<string, Action> State = builder.State;

            SetCallbacks(builder, "Root", behaviors);
            SetEvents(builder, "Root", events);

            State("closed", () => {
                SetCallbacks(builder, "closed", behaviors);
                SetEvents(builder, "closed", events);

                State("locked", () => {
                    SetCallbacks(builder, "locked", behaviors);
                    SetEvents(builder, "locked", events);
                    builder.Transition<Evt_DoorUnlock>("unlocked", (evt) => evt.hasKey);
                });

                State("unlocked", () => {
                    SetCallbacks(builder, "unlocked", behaviors);
                    SetEvents(builder, "unlocked", events);
                    builder.Transition<Evt_DoorOpen>("opened");
                    builder.Transition<Evt_DoorLock>("locked");
                });
            });

            State("opened", () => {
                SetCallbacks(builder, "opened", behaviors);
                SetEvents(builder, "opened", events);

                builder.Transition<Evt_DoorClose>("unlocked");
            });
        });
    }

    [Test]
    public void StateChart_SpecSimplePasses() {
        StateChart chart = new StateChart((builder) => {
            Action<string, Action> State = builder.State;

            State("State-1", () => {
                State("State-1-1", () => {
                    State("State-1-1-1", () => { });
                    State("State-1-1-2", () => { });
                });
                State("State-1-2", () => { });
            });
        });

        Assert.IsTrue(chart.IsInState("State-1"));
        Assert.IsTrue(chart.IsInState("State-1-1"));
        Assert.IsTrue(chart.IsInState("State-1-1-1"));
        Assert.IsFalse(chart.IsInState("State-1-1-2"));
        Assert.IsFalse(chart.IsInState("State-1-2"));
    }

    [Test]
    public void TransitionsToAnotherState() {
        StateChart chart = new StateChart((builder) => {
            Action<string, Action> State = builder.State;

            State("State-1", () => {
                builder.Transition<SomeEvt>("State-1-2");

                State("State-1-1", () => {
                    State("State-1-1-1", () => { });
                    State("State-1-1-2", () => { });
                });

                State("State-1-2", () => { });
            });
        });

        chart.Trigger(new SomeEvt());
        chart.Tick();
        Assert.IsTrue(chart.IsInState("State-1"));
        Assert.IsFalse(chart.IsInState("State-1-1"));
        Assert.IsFalse(chart.IsInState("State-1-1-1"));
        Assert.IsFalse(chart.IsInState("State-1-1-2"));
        Assert.IsTrue(chart.IsInState("State-1-2"));
    }

    [Test]
    public void StartInClosedAndLocked() {
        StateChart chart = GetChart();
        Assert.IsTrue(chart.IsInState("closed"));
        Assert.IsTrue(chart.IsInState("locked"));
    }

    [Test]
    public void StartNotInOpenedOrUnlocked() {
        StateChart chart = GetChart();
        Assert.IsFalse(chart.IsInState("opened"));
        Assert.IsFalse(chart.IsInState("unlocked"));
    }

    [Test]
    public void MovesToUnlockedFromClosedWithUnlockEvent() {
        StateChart chart = GetChart();
        chart.Trigger(new Evt_DoorUnlock());
        chart.Tick();
        Assert.IsTrue(chart.IsInState("unlocked"));
        Assert.IsTrue(chart.IsInState("closed"));
        Assert.IsFalse(chart.IsInState("opened"));
        Assert.IsFalse(chart.IsInState("locked"));
    }

    [Test]
    public void MovesFromClosedAndUnlockedToOpenedAndBackToClosedLocked() {
        StateChart chart = GetChart();
        chart.Trigger(new Evt_DoorUnlock());
        chart.Trigger(new Evt_DoorOpen());
        chart.Tick();
        Assert.IsTrue(chart.IsInState("opened"));
        Assert.IsFalse(chart.IsInState("closed"));
        Assert.IsFalse(chart.IsInState("locked"));
        Assert.IsFalse(chart.IsInState("unlocked"));
        chart.Trigger(new Evt_DoorClose());
        chart.Tick();
        Assert.IsFalse(chart.IsInState("opened"));
        Assert.IsTrue(chart.IsInState("closed"));
        Assert.IsFalse(chart.IsInState("locked"));
        Assert.IsTrue(chart.IsInState("unlocked"));
    }

    [Test]
    public void DoesNotTransitionIfNoTransitionHandlerExistsForCongiuration() {
        StateChart chart = GetChart();
        chart.Trigger(new Evt_DoorOpen());
        chart.Tick();
        Assert.IsFalse(chart.IsInState("opened"));
        Assert.IsTrue(chart.IsInState("closed"));
        Assert.IsTrue(chart.IsInState("locked"));
        Assert.IsFalse(chart.IsInState("unlocked"));
    }

    private static void MakeCallbacks(BehaviorMap behaviors, string[] keys, string[] events, Callback callback) {
        for (int i = 0; i < keys.Length; i++) {
            for (int j = 0; j < events.Length; j++) {
                behaviors[keys[i] + ":" + events[j]] = callback;
            }
        }
    }

    [Test]
    public void RunsEnterInitExitFunctionsInProperOrder() {
        BehaviorMap behaviors = new BehaviorMap();
        List<string> list = new List<string>();
        Callback callback = (stateName) => {
            list.Add(stateName);
        };
        MakeCallbacks(
            behaviors,
            new[] {"Root", "opened", "closed", "locked", "unlocked"},
            new[] {"init", "enter", "exit"},
            callback
        );

        StateChart chart = GetChart(behaviors);
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:init", "Root:enter",
            "closed:init", "closed:enter",
            "locked:init", "locked:enter"
        }));
        list.Clear();
        chart.Trigger(new Evt_DoorUnlock());
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "locked:exit", "unlocked:init", "unlocked:enter"
        }));
        list.Clear();
        chart.Trigger(new Evt_DoorLock());
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "unlocked:exit", "locked:enter"
        }));
    }

    [Test]
    public void RunsUpdateFunctionsInProperOrder() {
        BehaviorMap behaviors = new BehaviorMap();
        List<string> list = new List<string>();
        Callback callback = (stateName) => {
            list.Add(stateName);
        };
        MakeCallbacks(
            behaviors,
            new[] {"Root", "opened", "closed", "locked", "unlocked"},
            new[] {"update"},
            callback
        );
        StateChart chart = GetChart(behaviors);
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:update", "closed:update", "locked:update"
        }));
        list.Clear();
        chart.Trigger(new Evt_DoorUnlock());
        chart.Trigger(new Evt_DoorOpen());
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:update", "opened:update"
        }));
        list.Clear();
        chart.Trigger(new Evt_DoorClose());
        chart.Tick();
        chart.Tick();
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:update", "closed:update", "unlocked:update",
            "Root:update", "closed:update", "unlocked:update",
            "Root:update", "closed:update", "unlocked:update"
        }));
    }

    [Test]
    public void DoesNotTransitionIfGuardFnIsFalse() {
        StateChart chart = GetChart();
        chart.Trigger(new Evt_DoorUnlock(false));
        chart.Tick();
        Assert.IsFalse(chart.IsInState("unlocked"));
        Assert.IsTrue(chart.IsInState("closed"));
        Assert.IsFalse(chart.IsInState("opened"));
        Assert.IsTrue(chart.IsInState("locked"));
    }

    [Test]
    public void CallsEventHandlers() {
        List<string> list = new List<string>();
        Action<string, StateChartEvent> callback = (key, evt) => {
            list.Add(key);
        };

        EvtPair[] pairs = {
            new EvtPair("Root", typeof(Evt_DoorUnlock), callback),
            new EvtPair("closed", typeof(Evt_DoorUnlock), callback),
            new EvtPair("locked", typeof(Evt_DoorUnlock), callback),
            new EvtPair("unlocked", typeof(Evt_DoorUnlock), callback),
            new EvtPair("opened", typeof(Evt_DoorUnlock), callback),
            new EvtPair("Root", typeof(Evt_DoorLock), callback),
            new EvtPair("closed", typeof(Evt_DoorLock), callback),
            new EvtPair("locked", typeof(Evt_DoorLock), callback),
            new EvtPair("unlocked", typeof(Evt_DoorLock), callback)
        };

        StateChart chart = GetChart(null, pairs);
        chart.Trigger(new Evt_DoorUnlock());
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:Evt_DoorUnlock",
            "closed:Evt_DoorUnlock",
            "locked:Evt_DoorUnlock"
        }));
        list.Clear();
        chart.Trigger(new Evt_DoorUnlock());
        chart.Trigger(new Evt_DoorLock());
        chart.Tick();
        Assert.That(list, Is.EquivalentTo(new[] {
            "Root:Evt_DoorUnlock",
            "closed:Evt_DoorUnlock",
            "unlocked:Evt_DoorUnlock",
            "Root:Evt_DoorLock",
            "closed:Evt_DoorLock",
            "unlocked:Evt_DoorLock"
        }));
    }

    private static StateChart GetChart2() {
        return new StateChart((builder) => {
            builder.State("closed", () => {
                builder.Enter(() => {
                    builder.Trigger(new Evt_DoorOpen());
                });
            });

            builder.State("opened");

            builder.Transition<Evt_DoorClose>("closed");
            builder.Transition<Evt_DoorOpen>("opened");
        });
    }

    [Test]
    public void ProperlyQueuesEvents() {

        StateChart chart = GetChart2();
        Assert.IsTrue(chart.IsInState("closed"));
        chart.Tick();
        Assert.IsTrue(chart.IsInState("opened"));
    }

}