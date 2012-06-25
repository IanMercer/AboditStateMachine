using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Abodit.StateMachine
{
    public abstract partial class StateMachine<TStateMachine, TEvent>
        where TStateMachine : StateMachine<TStateMachine, TEvent>
        where TEvent : IEquatable<TEvent>
    {
        /// <summary>
        /// The static State Definitions that are created for a StateMachine
        /// </summary>
        [DebuggerDisplay("StateDefinition = {Name}")]
        private class StateDefinition
        {
            /// <summary>
            /// A StateDef spawns State objects
            /// </summary>
            /// <param name="stateDef"></param>
            /// <returns></returns>
            public static implicit operator State (StateDefinition stateDef)
            {
                if (stateDef == null) return (State)null;
                else return new State(stateDef.Name);
            }

            /// <summary>
            /// The Name of this state definition
            /// </summary>
            /// <remarks>
            /// Used for serialization of the state machine, also handy for debugging
            /// </remarks>
            [XmlAttribute]
            public string Name { get; set; }

            internal StateDefinition Parent {get; private set;}

            /// <summary>
            /// Get the ancestor states in ascending order, top-most state last
            /// </summary>
            internal IEnumerable<StateDefinition> SelfAndAncestorsInAscendingOrder
            {
                get
                {
                    var p = this;
                    while (p != null)
                    {
                        yield return p;
                        p = p.Parent;
                    }
                }
            }

            /// <summary>
            /// Get the ancestor states in ascending order, top-most state last
            /// </summary>
            internal IEnumerable<StateDefinition> AncestorsInAscendingOrder
            {
                get
                {
                    var p = this.Parent;
                    while (p != null)
                    {
                        yield return p;
                        p = p.Parent;
                    }
                }
            }

            internal Action<TStateMachine, TEvent, State> EntryAction = null;

            internal Action<TStateMachine, State, TEvent> ExitAction = null;

            // Entry and exit actions are looked up by NAME so that you can serialize and deserialize a State
            // and all we care about is the name of the state as the entry and exit actions were created
            // statically when the state machine class was initialized

            internal readonly IDictionary<TEvent, Func<TStateMachine, State, TEvent, State>> transitions = new Dictionary<TEvent, Func<TStateMachine, State, TEvent, State>>();

            /// <summary>
            /// Create a new State with a name and an optional entry and exit action
            /// </summary>
            public StateDefinition (string name, Action<TStateMachine, TEvent, State> entryAction = null, Action<TStateMachine, State, TEvent> exitAction = null, State parent = null)
            {
                this.Name = name;
                if (parent != null)
                    this.Parent = definitions[parent.Name];         // Map the parent to the identity mapped StateDefinition by name
                this.EntryAction = entryAction;
                this.ExitAction = exitAction;
            }

            public StateDefinition When(TEvent @event, Func<TStateMachine, State, TEvent, State> action)
            {
                transitions.Add(@event, action);
                return this;
            }

            public override string ToString()
            {
                return "*" + this.Name + "*";
            }
        }
    }
}
