namespace DC.Akka.Examples.Projections.Events;

public record OrderInitiated(string OrderId) : IOrderEvent;