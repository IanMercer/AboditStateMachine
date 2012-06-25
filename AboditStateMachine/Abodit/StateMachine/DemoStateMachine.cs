using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abodit.StateMachine;
using System.Diagnostics;

namespace AboditUnits.StateMachine
{
    //[TestClass]
    public class StateMachineDemoTests
    {
        //[TestMethod]
        public void TestMethod1()
        {
            DemoStatemachine demoStateMachine = new DemoStatemachine(DemoStatemachine.UnVerified);

            demoStateMachine.StateChanges += new Abodit.StateMachine.StateMachine<DemoStatemachine, Abodit.StateMachine.Event>.ChangesStateDelegate(demoStateMachine_StateChanges);

            // Normally you would be looking at the NextTimeEventAt field, and when it's after UtcNow you would call Tick()
            // For this test, simulate several days going by ...

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(1));
            Trace.WriteLine("Next time to tick = " + demoStateMachine.NextTimedEventAt);

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(2));
            Trace.WriteLine("Next time to tick = " + demoStateMachine.NextTimedEventAt);

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(3));
            Trace.WriteLine("Next time to tick = " + demoStateMachine.NextTimedEventAt);

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(4));
            // Finally the user verifies their email
            demoStateMachine.VerifiesEmail();

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(5));

            demoStateMachine.Tick(DateTime.UtcNow.AddDays(6));
        }

        void demoStateMachine_StateChanges(Abodit.StateMachine.StateMachine<DemoStatemachine, Abodit.StateMachine.Event>.State state)
        {
            Console.WriteLine("New state = " + state);
        }
    }


    // --------------------------------- BELOW HERE IS THE ACTUAL STATE MACHINE ----------------------------------------


    [Serializable]
    public class DemoStatemachine : StateMachine<DemoStatemachine, Event>
    {
        /// <summary>
        /// Example private state variable relating to this state machine
        /// </summary>
        DateTime dateTimeLastCommunicated { get; set; }

        /// <summary>
        /// Example of an extra property that you want to be serialized with the state machine
        /// </summary>
        public int MessageCounterForTesting { get; set; }

        public override void OnStateChanging(StateMachine<DemoStatemachine, Event>.State newState)
        {
            Trace.WriteLine("Entered state " + newState);
        }

        public static void SendReminderEmail(DemoStatemachine m, State state, Event e)
        {
            Trace.WriteLine("You need to verify your email " + state + " via " + e);
            m.MessageCounterForTesting++;
            // Send email to ask them to verify their account
            m.dateTimeLastCommunicated = DateTime.UtcNow;
        }

        public static void CompletedEntered(DemoStatemachine m, Event e, State state)
        {
            // No longer need to be on a scheduled check routine
            m.CancelScheduledEvent(eScheduledCheck);
            Trace.WriteLine("Use has completed email verification " + state + " via " + e);
        }

        // Unlike events, states belong to a particular state machine and should all be defined as static readonly properties like this:

        public static readonly State UnVerified = AddState("UnVerified");

        public static readonly State Verified = AddState("Verified");

        // States are hierarchical.  If you are in state VerifiedRecently you are also in is parent state Verified.

        public static readonly State VerifiedRecently = AddState("Verified recently", parent: Verified);
        public static readonly State VerifiedAWhileAgo = AddState("Verified a while ago", parent: Verified);

        // You can use any class that implements IEquatableofT as an Event, there is also an Event class provided which you
        // can use instead of defining one each time.

        private static Event eUserVerifiedEmail = new Event("User verified email");
        private static Event eScheduledCheck = new Event("Scheduled Check");
        private static Event eBeenHereAWhile = new Event("Been here a while");

        /// <summary>
        /// A static constructor in your state machine is where you define it.
        /// That way it is only ever defined once per program activation.
        /// 
        /// Each transition you define takes as an argument the state machine instance (m),
        /// the state (s) and the event (e).
        /// 
        /// </summary>
        static DemoStatemachine()
        {
            UnVerified
                    .OnEnter((m, s, e) =>
                        {
                            Trace.WriteLine("States can execute code when they are entered or when they are left");
                            Trace.WriteLine("In this case we start a timer to bug the user until they confirm their email");
                            m.Every(new TimeSpan(hours: 10, minutes:0, seconds:0), eScheduledCheck);

                            Trace.WriteLine("You can also set a reminder to happen at a specific time, or after a given interval just once");
                            m.At(new DateTime(DateTime.Now.Year+1, 1, 1), eScheduledCheck);
                            m.After(new TimeSpan(hours: 24, minutes: 0, seconds: 0), eScheduledCheck);

                            Trace.WriteLine("All necessary timing information is serialized with the state machine.");
                            Trace.WriteLine("The serialized state machine also exposes a property showing when it next needs to be woken up.");
                            Trace.WriteLine("External code will need to call the Tick(utc) method at that time to trigger the next temporal event");
                        })
                    .When(eScheduledCheck, (m, s, e) =>
                    {
                        Trace.WriteLine("Here is where we would send a message to the user asking them to verify their email");
                        // We return the current state 's' rather than 'UnVerified' in case we are in a child state of 'Unverified'
                        // This makes it easy to handle hierarchical states and to either change to a different state or stay in the same state
                        return s;
                    })
                    .When(eUserVerifiedEmail, (m, s, e) =>
                    {
                        Trace.WriteLine("The user has verified their email address, we are done (almost)");
                        // Kill the scheduled check event, we no longer need it
                        m.CancelScheduledEvent(eScheduledCheck);
                        // Start a timer for one last transition
                        m.After(new TimeSpan(hours:24, minutes:0, seconds:0), eBeenHereAWhile);
                        return VerifiedRecently;
                    });

            VerifiedRecently
                    .When(eBeenHereAWhile, (m, s, e) =>
                    {
                        Trace.WriteLine("User has now been a member for over 24 hours - give them additional priviledges for example");
                        // No need to cancel the eBeenHereAWhile event because it wasn't auto-repeating
                        //m.CancelScheduledEvent(eBeenHereAWhile);
                        return VerifiedAWhileAgo;
                    });

            Verified.OnEnter((m, s, e) => 
                {
                    Trace.WriteLine("The user is now fully verified");
                });

            VerifiedAWhileAgo.OnEnter((m, s, e) =>
                {
                    Trace.WriteLine("The user has been verified for over 24 hours");
                });

        }

        /// <summary>
        /// Default constructor - used only when deserializing (does not start the clock)
        /// </summary>
        public DemoStatemachine()
            : base(UnVerified)
        {
        }

        /// <summary>
        /// An alternate constructor with an initial state
        /// </summary>
        public DemoStatemachine(State initial)
            : base(initial)
        {
            // Manually start the clock since we are not being deserialized
            this.Every(new TimeSpan(hours: 10, minutes: 0, seconds: 0), eScheduledCheck);
        }

        // Instead of exposing our Events we might expose methods on the state machine that fire the events
        // I think this is preferable, some of the events might be purely internal, like the timer tick ones

        public void VerifiesEmail()
        {
            this.EventHappens(eUserVerifiedEmail);
        }
    }
}
