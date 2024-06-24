namespace DC.Akka.Examples.Projections;

public record EventWithPosition(object Event, long? Position);