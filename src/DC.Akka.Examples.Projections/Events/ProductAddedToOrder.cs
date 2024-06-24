namespace DC.Akka.Examples.Projections.Events;

public record ProductAddedToOrder(string OrderId, string ProductId) : IOrderEvent;