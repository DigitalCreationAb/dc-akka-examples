namespace DC.Akka.Examples.Projections.Events;

public record OrderCancelled(string OrderId) : IOrderEvent;