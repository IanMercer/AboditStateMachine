using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace Abodit.StateMachine
{
    /// <summary>
    /// A state machine allows you to track state and to take actions when states change
    /// This state machine provides a fluent interface for defining states and transitions
    /// When you inherit from this abstract base class you can add variables to your state machine
    /// which makes it an "Extended State Machine"
    /// </summary>
    /// <remarks>
    /// Also has timed events set to fire at some point in the future but provides no Timer internally because
    /// that will be implementation dependent.  For example the StateMachine may exist in a serialized form
    /// in a database with the NextTimedEventAt property exposed for indexing and some external timer fecthing
    /// records and calling the Tick() method.
    /// 
    /// Nasty generic of self so we can refer to the inheriting class internally.
    /// </remarks>
    [Serializable]
    [DebuggerDisplay("Current State = {CurrentState.Name}")]
    public abstract partial class StateMachine<TStateMachine, TEvent> : StateMachineBase//, IEquatable<TStateMachine>
        where TStateMachine : StateMachine<TStateMachine, TEvent>
        where TEvent : IEquatable<TEvent>
    {
        /// <summary>
        /// The current state of this State machine
        /// </summary>
        public State CurrentState { get; set; }

        // TODO: Use the Reactive framework instead of events??
        //public Subject<State> StateObservable = new Subject<State>();

        public delegate void ChangesStateDelegate (State @state);

        /// <summary>
        /// A public Event that advises of any changes in state that actually happen
        /// </summary>
        public event ChangesStateDelegate StateChanges;


        /// <summary>
        /// An optional method that you can use to handle every state change without hooking the event
        /// </summary>
        /// <param name="newState"></param>
        public virtual void OnStateChanging(State newState)
        {
        }

        /// <summary>
        /// The UTC time at which this state machine needs to receive a wake up from an external timing component
        /// </summary>
        public DateTime NextTimedEventAt { get; set; }

        public static implicit operator TStateMachine (StateMachine<TStateMachine, TEvent> stateMachine)
        {
            return stateMachine;
        }

        public static implicit operator StateMachine<TStateMachine,TEvent>(TStateMachine stateMachine)
        {
            return stateMachine;
        }

        /// <summary>
        /// Most State machines have a Start() method that moves them to their initial state
        /// </summary>
        public virtual void Start()
        {
        }

        /// <summary>
        /// A event that should fire at a particular time
        /// </summary>
        [DebuggerDisplay("Timed Event = {At} {Event}")]
        [Serializable]
        public class TimedEvent
        {
            /// <summary>
            /// The time at which the event should happen
            /// </summary>
            public DateTime AtUtc {get; set;}

            /// <summary>
            /// The event that happens
            /// </summary>
            public TEvent Event {get; set;}

            /// <summary>
            /// Autorepeat after this many ticks
            /// </summary>
            /// <remarks>
            /// Would have used a TimeSpan but they don't XML Serialize 
            /// </remarks>
            public long AutoRepeatTicks { get; set; }

            public TimedEvent() { }
            public DateTime NextOccurrence()
            {
                return this.AtUtc.AddTicks(this.AutoRepeatTicks);
            }
        }

        /// <summary>
        /// Future events set on this StateMachine
        /// </summary>
        /// <remarks>
        /// Each StateMachine has its own small set of future events.  Typically this list will be very small; when an event fires
        /// it might record a future event that needs to be executed
        /// </remarks>
        public List<TimedEvent> TimedEvents
        {
            get;
            set;
        }

        private object noConcurrentTicks = new object();

        /// <summary>
        /// Find any events that should have fired by now and execute up to a maximum set number of them
        /// (e.g. use the number to limit how long this method can run for in worst case before you persist the state
        ///  machine to disk)
        /// </summary>
        /// <param name="utcNow">The current utc time (passed as a parameter to make this method testable)</param>
        /// <param name="limitOnNumberExecuted">In order to prevent runaway execution of a misconfigured recurring event set a maximum number of executions</param>
        /// <remarks>
        /// Note: These events are executed synchronously on the calling thread
        /// Caller should persist this object (if necessary) after all timedEvents have been processed
        /// Timed events may themselves add new events to the event queue.  These new events will happen
        /// immediately in this method if they themselves are already past due
        /// </remarks>
        public void Tick(DateTime utcNow, int limitOnNumberExecuted = 10000)
        {
            lock (noConcurrentTicks)
            {
                while (limitOnNumberExecuted-- > 0)
                {
                    TEvent nextEvent = default(TEvent);
                    DateTime eventAtUtc = default(DateTime);
                    lock (this.TimedEvents)
                    {
                        TimedEvent current = null;
                        // Within the lock all we do is find the next event, remove it and update the next time
                        current = this.TimedEvents.Where(te => te.AtUtc <= utcNow).OrderBy(te => te.AtUtc).FirstOrDefault();
                        if (current != null)
                        {
                            nextEvent = current.Event;
                            eventAtUtc = current.AtUtc;

                            if (current.AutoRepeatTicks != 0)
                            {
                                current.AtUtc = current.NextOccurrence();
                            }
                            else
                            {
                                this.TimedEvents.Remove(current);
                            }
                            RecalculateNextTimedEvent();
                        }
                    }

                    // NOTE: Current.At is now pointing to the new time

                    if (nextEvent != null)
                    {
                        // DEBUG
                        Trace.WriteLine(string.Format("Was due at {0:HHmm} and it is now {1:HHmm}", eventAtUtc, utcNow));
                        this.EventHappens(nextEvent);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void RecalculateNextTimedEvent()
        {
            if (this.TimedEvents.Any())
                this.NextTimedEventAt = this.TimedEvents.Min(te => te.AtUtc);
            else
                this.NextTimedEventAt = DateTime.MaxValue;      // never!
        }


        /// <summary>
        /// Empty constructor for serialization only
        /// </summary>
        public StateMachine()
        {
            this.TimedEvents = new List<TimedEvent>();      // Will get deserialized events written in to it
            this.NextTimedEventAt = DateTime.MaxValue;
        }

        /// <summary>
        /// Construct a state machine with an initial state
        /// </summary>
        public StateMachine(State initial)
        {
            this.CurrentState = initial;
            this.TimedEvents = new List<TimedEvent>();
            this.NextTimedEventAt = DateTime.MaxValue;
        }

        /// <summary>
        /// An event has happened, transition to next state
        /// </summary>
        public void EventHappens(TEvent @event)
        {
            if (this.CurrentState == null)
            {
                var initialStateIfNotSet = definitions.First().Value;
                throw new NullReferenceException("You forgot to set the initial State, maybe you wanted to use " + initialStateIfNotSet);
            }
            var newState = this.CurrentState.OnEvent((TStateMachine)this, @event);
            if (newState != null && newState != this.CurrentState)
            {
                this.OnStateChanging(newState);
                this.CurrentState = newState;
                if (StateChanges != null)
                    StateChanges(this.CurrentState);
            }
        }

        private static IDictionary<string, StateDefinition> definitions = new Dictionary<string, StateDefinition>();

        // We remember the parents for States separately from the State object itself
        // to make it easier to deal with deserialized states (which lack the parent structure)

        /// <summary>
        /// Add a new state definition
        /// </summary>
        public static State AddState(string name, Action<TStateMachine, TEvent, State> entryAction = null, Action<TStateMachine, State, TEvent> exitAction = null, State parent = null)
        {
            var stateDefinition = new StateDefinition(name, entryAction, exitAction, parent);
            definitions.Add(name, stateDefinition);
            return stateDefinition;
        }

        /// <summary>
        /// At a certain time, cause a certain event to happen
        /// </summary>
        public void At(DateTimeOffset dateTime, TEvent @event)
        {
            lock (this.TimedEvents)
            {
                var utc = dateTime.ToUniversalTime().DateTime;
                this.TimedEvents.Add(new TimedEvent { AtUtc = utc, Event = @event, AutoRepeatTicks = 0});
                if (utc < this.NextTimedEventAt) this.NextTimedEventAt = utc;
            }
        }

        /// <summary>
        /// At a certain time Utc, cause a certain event to happen
        /// </summary>
        public void At(DateTime dateTimeUtc, TEvent @event)
        {
            lock (this.TimedEvents)
            {
                this.TimedEvents.Add(new TimedEvent { AtUtc = dateTimeUtc, Event = @event, AutoRepeatTicks = 0 });
                if (dateTimeUtc < this.NextTimedEventAt) this.NextTimedEventAt = dateTimeUtc.ToUniversalTime();
            }
        }


        /// <summary>
        /// After a certain period, cause a certain event to happen
        /// </summary>
        public void After(TimeSpan timeSpan, TEvent @event)
        {
            lock (this.TimedEvents)
            {
                var dateTimeAt = DateTime.UtcNow.Add(timeSpan);
                At(dateTimeAt, @event);
            }
        }

        /// <summary>
        /// Every time interval, cause a certain event to happen
        /// </summary>
        /// <remarks>
        /// Uses TimePeriod not timespan because TimePeriod is more flexible (e.g. weeks, months, ...)
        /// Use CancelScheduledEvent() to remove a repeated event
        /// </remarks>
        public void Every(TimeSpan timeSpan, TEvent @event)
        {
            lock (this.TimedEvents)
            {
                var firstOccurrence = DateTime.UtcNow.Add(timeSpan);
                this.TimedEvents.Add(new TimedEvent { AtUtc = firstOccurrence, Event = @event, AutoRepeatTicks = timeSpan.Ticks });
                if (firstOccurrence < this.NextTimedEventAt) this.NextTimedEventAt = firstOccurrence;
            }
        }

        /// <summary>
        /// Removes any scheduled or recurring events that would fire the given event
        /// </summary>
        public void CancelScheduledEvent(TEvent @event)
        {
            lock (this.TimedEvents)
            {
                this.TimedEvents.RemoveAll(te => te.Event.Equals(@event));
                RecalculateNextTimedEvent();
            }
        }
    }
}