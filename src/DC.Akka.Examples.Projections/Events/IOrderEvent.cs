namespace DC.Akka.Examples.Projections.Events;

public interface IOrderEvent
{
    string OrderId { get; }
}