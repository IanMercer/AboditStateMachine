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
          Trace.WriteLine("User has now been a member for over 24 hours");
          // give them additional priviledges perhaps
          // and transition to a new state
          return VerifiedAWhileAgo;
       });

The `When` method takes the state machine instance, the current state and the event and executes a method with can operate on any of these objects and then returns the new state (for a transition) or `s` the current state if no transition happens.

A state machine class is defined using a self-referencing generic and an Event type:

    [Serializable]
    public class DemoStatemachine : StateMachine<DemoStatemachine, Event>
    {
     ...
    }

The state machine instance may contain additional properties (e.g. the identity of the user). It would not normally contain complex objects as it should be easily serializable to a database unless it is used only for the duration of the application's execution.

You can execute code when a state is entered or when it is left. Hierarchical states are entered and left in the appropriate order (reversed on leaving). Whilst you can include complex business logic in the state machine itself for such transitions, I find it better to instead keep that outside and expose additional C# events that fire when the business logic needs to be executed. This keeps the state machine implementation 'pure', dealing only with the definition of states and transitions between states and not the logic that happens when a state transitions.

Compared to the Wikipedia definition of a hierarchical state machine there is one further addition which is a set of methods and properties to handle time-based events and recurring events using an efficient 'next-occurrence' approach. The exposed property `NextTimedEventAt` can be mapped to a database column with an index allowing your program to easily find any state machines that need to be re-loaded and to have their `Tick` method called.

An example of the use of this temporal capability would be the implementation of user messaging flow for a website. The user creates an account, verifies their email, uses the system for a while, then deletes their account. After creating their account a reminder Event is added to send them a welcome email, a week later a follow up 'how is it going' email and so on. When the state machine loads from the database it can check that the user has not deleted their account, can fire an event to send the appropriate email and can schedule the next email.

See [the included example](blob/master/AboditStateMachine/Abodit/StateMachine/DemoStateMachine.cs) for details on how to specify and use the state machine.

