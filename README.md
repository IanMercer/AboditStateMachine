AboditStateMachine
==================

A state machine for .NET that implements easily serializable, hierarchical state machines with timing.

There are three components to the state machine:

1) Events: You can use any class that implements `IEquatable<T>` to implement these (or the provided `Event` class). Events can be shared across state machines. Alternatively you can keep the Events private to the state machine implementation and expose them only as methods on the StateMachine itself. e.g.

    private static Event eUserVerifiedEmail = new Event("User verified email");

    public void VerifiesEmail()
    {
       this.EventHappens(eUserVerifiedEmail);
    }


2) States: These belong to a given state machine, they can be hierarchical. They must be created by calling `AddState`. e.g.

    public static readonly State UnVerified = AddState("UnVerified");

Hierarchical states make it much easier to model complex systems, for example, an oven can be `Off` or `On` and within `On` it can be `On.Heating` or `On.Ready`. Transitioning to `Off` from any `On` state when the user turns it off can now be expressed as a single rule.

3) StateMachine: The static definition of the finite state machine specifying how to transition between states when events happen. e.g. 

    VerifiedRecently
       .When(eBeenHereAWhile, (m, s, e) =>
       {
          Trace.WriteLine("User has now been a member for over 24 hours - give them additional priviledges for example");
          return VerifiedAWhileAgo;
       });

The `When` method takes the state machine instance, the current state and the event and executes a method with can operate on any of these objects and then returns the new state (for a transition) or `s` the current state if no transition happens.

A state machine class is defined using a self-referencing generic and an Event type:

    [Serializable]
    public class DemoStatemachine : StateMachine<DemoStatemachine, Event>
    {
     ...
    }

Compared to the Wikipedia definition of a hierarchical state machine there is one further addition which is a set of methods and properties to handle time-based events using an efficient 'next-occurrence' approach. The exposed property `NextTimedEventAt` can be used to decide which state machine to load from a database and execute a `Tick` on next. This temporal capability is useful for state machines that implement a user messaging flow for a website - e.g. an email after one week provided they haven't cancelled, and then another each month after that.


See [the included example](blob/master/AboditStateMachine/Abodit/StateMachine/DemoStateMachine.cs) for details on how to specify and use the state machine.




