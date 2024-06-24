using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence.EventStore.Query;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using DC.Akka.Examples.Projections.Events;

namespace DC.Akka.Examples.Projections;

public class OrderInformationProjectionCoordinator : ReceiveActor
{
    public static class Commands
    {
        public record Start;

        public record Complete;

        public record Fail(Exception Error);
    }
    
    private IKillSwitch? _killSwitch;
    private readonly IActorRef _orderProjectionShard;
    
    public OrderInformationProjectionCoordinator()
    {
        Become(Stopped);
        
        _orderProjectionShard = ClusterSharding.Get(Context.System).Start(
            typeName: "projection-order",
            entityPropsFactory: id => Props.Create(() => new OrderInformationProjection(id)),
            settings: ClusterShardingSettings.Create(Context.System),
            messageExtractor: new OrderProjectionMessageExtractor(100));
    }

    private void Stopped()
    {
        ReceiveAsync<Commands.Start>(async _ =>
        {
            var latestPosition = await LoadLatestPosition();

            _killSwitch = RestartSource
                .OnFailuresWithBackoff(() =>
                {
                    var source = PersistenceQuery.Get(Context.System)
                        .ReadJournalFor<EventStoreReadJournal>(
                            Context.System.Settings.Config.GetString("akka.persistence.query.plugin"))
                        .EventsByTag("order", latestPosition != null ? Offset.Sequence(latestPosition.Value) : Offset.NoOffset());
                    
                    return source
                        .GroupedWithin(1000, TimeSpan.FromSeconds(5))
                        .SelectMany(data =>
                        {
                            return data
                                .Select(x => new
                                {
                                    Position = (x.Offset as Sequence)?.Value ?? 0,
                                    OrderEvent = x.Event as IOrderEvent
                                })
                                .Where(x => x.OrderEvent != null)
                                .GroupBy(x => x.OrderEvent!.OrderId)
                                .Select(x => (
                                    Events: x.Select(y => new EventWithPosition(y.OrderEvent!, y.Position)).ToImmutableList(),
                                    Id: x.Key));
                        })
                        .SelectAsyncUnordered(
                            100, 
                            async data =>
                            {
                                if (data.Events.IsEmpty)
                                    return null;
                                
                                var tries = 0;
                                
                                var maxPosition = data.Events.Select(x => x.Position).Max();

                                while (tries <= 5)
                                {
                                    try
                                    {
                                        var response =
                                            await _orderProjectionShard
                                                .Ask<OrderInformationProjection.Responses.IApplyEvents>(
                                                    new OrderInformationProjection.Commands.ApplyEvents(
                                                        data.Id,
                                                        data
                                                        .Events
                                                        .Select(x => x.Event)
                                                        .ToImmutableList()));

                                        return response switch
                                        {
                                            OrderInformationProjection.Responses.Acknowledge => maxPosition,
                                            OrderInformationProjection.Responses.Reject nack => throw new Exception(
                                                "Rejected projection", nack.Cause),
                                            _ => throw new Exception("Unknown projection response")
                                        };
                                    }
                                    catch (AskTimeoutException)
                                    {
                                        tries++;
                                    }
                                }

                                throw new Exception("Max retries reached");
                        })
                        .GroupedWithin(1000, TimeSpan.FromSeconds(5))
                        .SelectAsync(1, async positions =>
                        {
                            var highestPosition = positions.MaxBy(y => y);

                            if (highestPosition.HasValue)
                                latestPosition = await StoreLatestPosition(highestPosition.Value);

                            return NotUsed.Instance;
                        });
                }, RestartSettings.Create(
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromMinutes(1),
                    1.2).WithMaxRestarts(5, TimeSpan.FromMinutes(10)))
                .ViaMaterialized(KillSwitches.Single<NotUsed>(), Keep.Right)
                .ToMaterialized(Sink.ActorRef<NotUsed>(
                    Self,
                    new Commands.Complete(),
                    ex => new Commands.Fail(ex)), Keep.Left)
                .Run(Context.System.Materializer());

            Become(Started);
        });
    }

    private void Started()
    {
        Receive<Commands.Fail>(_ =>
        {
            _killSwitch?.Shutdown();

            Become(Stopped);
        });
        
        Receive<Commands.Complete>(_ =>
        {
            Become(Completed);
        });
    }
    
    private void Completed()
    {

    }

    protected override void PreStart()
    {
        Self.Tell(new Commands.Start());
    }

    private async Task<long?> LoadLatestPosition()
    {
        //TODO: Load latest position from database

        return null;
    }

    private async Task<long> StoreLatestPosition(long position)
    {
        //TODO: Store position in database
        
        return position;
    }
    
    private class OrderProjectionMessageExtractor(int maxNumberOfShards) 
        : HashCodeMessageExtractor(maxNumberOfShards)
    {
        public override string EntityId(object message)
        {
            return (message as OrderInformationProjection.Commands.ApplyEvents)?.OrderId ?? "";
        }
    }
}