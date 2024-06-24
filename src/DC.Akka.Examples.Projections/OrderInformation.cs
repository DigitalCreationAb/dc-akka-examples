using System.Collections.Immutable;

namespace DC.Akka.Examples.Projections;

public record OrderInformation(string OrderId, IImmutableList<string> Products);