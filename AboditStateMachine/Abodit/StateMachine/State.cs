using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Abodit.StateMachine
{
    public abstract partial class StateMachine<TStateMachine, TEvent> where TStateMachine : StateMachine<TStateMachine, TEvent>
        where TEvent : IEquatable<TEvent>
    {
        /// <summary>
        /// A state that the state machine can be in
        /// </summary>
        /// <remarks>
        /// This is defined as a nested class to ensure each state machine has only the states that were defined for it
        /// The State class delegates almost everything to the StateDefinition that is stored statically for each StateMachine
        /// Defined as a nested class so that this state machine's states can only be used with it and not with some other state machine
        /// </remarks>
        [DebuggerDisplay("State = {Name}")]
        public class State : ISerializable, IEquatable<State>
        {
            /// <summary>
            /// The Name of this state (all states with the same Name are considered equal even if they are different objects)
            /// </summary>
            /// <remarks>
            /// This makes serialization and deserialization easier
            /// </remarks>
            [XmlAttribute]
            public string Name
            {
                get
                {
                    return this.stateDefinition.Name;
                }
                set
                {
                    this.stateDefinition = definitions[value];
                }
            }

            /// <summary>
            /// Our state definition stores everything we need to know - our entry and exit actions, our parentage, ...
            /// Every state with the same name within a StateMachine has the same definition
            /// </summary>
            private StateDefinition stateDefinition;

            /// <summary>
            /// States can optionally be organized hierarchically
            /// The hierarchy is set by the initial static creation of States
            /// not by some later states that are loaded from serialized versions
            /// </summary>
            public State ParentState { get { return (State)stateDefinition.Parent; } }

            public State()
            {
            }

            /// <summary>
            /// Create a new State with a name
            /// </summary>
            internal State(string name)
            {
                this.Name = name;
                this.stateDefinition = definitions[name];
            }

            /// <summary>
            /// Get a state by name, if it exists (suitable for use during deserialization from a string)
            /// </summary>
            public static bool TryGet(string name, out State state)
            {
                StateDefinition sd = null;
                bool found = definitions.TryGetValue(name, out sd);
                state = sd;
                return found;
            }

            /// <summary>
            /// Act on an event, return the new state or null if there are no transitions possible (even inherited)
            /// </summary>
            public State OnEvent(TStateMachine stateMachine, TEvent @event)
            {
                StateDefinition selfOrAncestor = this.stateDefinition;
                int safety = 1000;              // just cautious code to ensure a bad data structure can't crash app
                while (selfOrAncestor != null && --safety > 0)
                {
                    Func<TStateMachine, State, TEvent, State> transition = null;
                    if (selfOrAncestor.transitions.TryGetValue(@event, out transition))
                    {
                        // Execute the transition to get the new state
                        State newState = transition(stateMachine, this, @event);
                        if (newState != this)
                        {
                            // Entry and exit actions only fire when CHANGING state

                            // Exit actions happen from the innermost state to the outermost state
                            var oStates = this.stateDefinition.SelfAndAncestorsInAscendingOrder;
                            foreach (var n in oStates)
                            {
                                if (newState.Is(n)) break;      // Stop if we reach a common ancestor - we are NOT exiting that state
                                if (n.ExitAction != null) n.ExitAction(stateMachine, newState, @event);
                            }

                            // Entry actions happen from the base-most state to the innermost state (like Constructors)
                            var nStates = newState.stateDefinition.SelfAndAncestorsInAscendingOrder.Reverse();
                            foreach (var n in nStates)
                            {
                                if (this.Is(n)) continue;       // If the old state is already a descendant of that state we don't enter it
                                if (n.EntryAction != null) n.EntryAction(stateMachine, @event, newState);
                            }
                        }
                        return newState;
                    }
                    // otherwise, try parent, see if they have a transition we can use [inheritance]
                    selfOrAncestor = selfOrAncestor.Parent;
                }
                return null;
            }

            public override string ToString()
            {
                return "*" + this.Name + "*";
            }

            /// <summary>
            /// Define what happens 'When' an event happens while in this state.  The action will fire provided
            /// some substate doesn't act on it first.
            /// </summary>
            public State When(TEvent @event, Func<TStateMachine, State, TEvent, State> action)
            {
                // Pass the When clause on to our underlying definition
                this.stateDefinition.When(@event, action);
                return this;
            }

// Because entry actions are executed automatically whenever an associated state is entered, they often determine the conditions of operation or the identity of the state, very much as a class constructor determines the identity of the object being constructed. For example, the identity of the "heating" state is determined by the fact that the heater is turned on. This condition must be established before entering any substate of "heating" because entry actions to a substate of "heating," like "toasting," rely on proper initialization of the "heating" superstate and perform only the differences from this initialization. Consequently, the order of execution of entry actions must always proceed from the outermost state to the innermost state (top-down).
// Not surprisingly, this order is analogous to the order in which class constructors are invoked. Construction of a class always starts at the very root of the class hierarchy and follows through all inheritance levels down to the class being instantiated. The execution of exit actions, which corresponds to destructor invocation, proceeds in the exact reverse order (bottom-up).

            /// <summary>
            /// Add an action that happens when this state is entered
            /// </summary>
            /// <remarks>
            /// This is an alternative to setting it in the state constructor
            /// </remarks>
            public State OnEnter(Action<TStateMachine, TEvent, State> action)
            {
                this.stateDefinition.EntryAction = action;
                return this;
            }

            /// <summary>
            /// Add an action that happens when this state exits
            /// </summary>
            /// <remarks>
            /// This is an alternative to setting it in the state constructor
            /// </remarks>
            public State OnExit(Action<TStateMachine, State, TEvent> action)
            {
                this.stateDefinition.ExitAction = action;
                return this;
            }

            /// <summary>
            /// Test this state to see if it 'is' the other state, i.e. if it is the same or inherits from it
            /// </summary>
            public bool Is(State other)
            {
                return (this.Equals(other) || (this.ParentState != null && this.ParentState.Is(other)));
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("Name", this.Name);
            }

            // States are the same if ther underlying definition is the same

            public bool Equals(State other)
            {
                if (other == null) return false;
                return this.stateDefinition == other.stateDefinition;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as State);
            }

            public static bool operator == (State a, State b)
            {
                // If both are null, or both are same instance, return true.
                if (System.Object.ReferenceEquals(a, b))
                {
                    return true;
                }
                return a.Equals(b);
            }

            public static bool operator != (State a, State b)
            {
                return !(a == b);
            }

            public override int GetHashCode()
            {
                return this.stateDefinition.GetHashCode();
            } 

        }
    }
}
