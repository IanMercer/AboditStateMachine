using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Abodit.StateMachine
{
    /// <summary>
    /// An event that causes the state machine to transition to a new state - you can use any object
    /// that implements IEquatable of T
    /// </summary>
    /// <remarks>
    /// Using this class you can create simple named events that have all of the required properties to work in the State Machine
    /// </remarks>
    [DebuggerDisplay("Event = {Name}")]
    public class Event : IEquatable<Event>
    {
        /// <summary>
        /// Events with the same name (within a state machine) are considered to be the same event
        /// so you don't need the specific same Event in order to fire it
        /// Unlike States where we do extra work to get a consistent stateDefinition
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }

        private Event() { }

        public Event(string name)
        {
            this.Name = name;
        }
        public override string ToString()
        {
            return "~" + this.Name + "~";
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Event)) return false;
            return this.Name.Equals(((Event)obj).Name);
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public static bool operator ==(Event a, Event b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }
            return a.Equals(b);
        }

        public static bool operator !=(Event a, Event b)
        {
            return !(a == b);
        }

        public bool Equals(Event other)
        {
            if (other == null) return false;
            else return this.Name.Equals(other.Name);
        }
    }
}