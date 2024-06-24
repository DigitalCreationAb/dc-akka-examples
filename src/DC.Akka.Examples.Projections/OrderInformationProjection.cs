using System.Collections.Immutable;
using Akka.Actor;
using DC.Akka.Examples.Projections.Events;

namespace DC.Akka.Examples.Projections;

public class OrderInformationProjection : ReceiveActor
{
    public static class Commands
    {
        public record ApplyEvents(string OrderId, IImmutableList<object> Events);
    }
    
    public static class Responses
    {
        public record Acknowledge : IApplyEvents;

        public record Reject(Exception? Cause) : IApplyEvents;

        public interface IApplyEvents;
    }

    private readonly string _orderId;
    
    public OrderInformationProjection(string orderId)
    {
        _orderId = orderId;
        
        Become(NotLoaded);
    }
    
    private void NotLoaded()
    {
        ReceiveAsync<Commands.ApplyEvents>(async cmd =>
        {
            try
            {
                var order = await LoadFromDatabase();

                order = ApplyEvents(order, cmd.Events);

                await SaveToDatabase(order);

                Sender.Tell(new Responses.Acknowledge());

                Become(() => Loaded(order));
            }
            catch (Exception e)
            {
                Sender.Tell(new Responses.Reject(e));
                
                throw;
            }
        });
    }

    private void Loaded(OrderInformation? order)
    {
        ReceiveAsync<Commands.ApplyEvents>(async cmd =>
        {
            try
            {
                order = ApplyEvents(order, cmd.Events);

                await SaveToDatabase(order);

                Sender.Tell(new Responses.Acknowledge());

                Become(() => Loaded(order));
            }
            catch (Exception e)
            {
                Sender.Tell(new Responses.Reject(e));
                
                throw;
            }
        });
    }

    private OrderInformation? ApplyEvents(OrderInformation? order, IImmutableList<object> events)
    {
        return events.Aggregate(order, (current, evnt) => evnt switch
        {
            OrderInitiated initiated => new OrderInformation(initiated.OrderId, ImmutableList<string>.Empty),
            ProductAddedToOrder productAdded when current != null => current with
            {
                Products = current.Products.Add(productAdded.ProductId)
            },
            OrderCancelled => null,
            _ => current
        });
    }

    private async Task<OrderInformation?> LoadFromDatabase()
    {
        //TODO: Load order from database

        return null;
    }

    private async Task SaveToDatabase(OrderInformation? order)
    {
        if (order != null)
        {
            //TODO: Save order to database
        }
        else
        {
            //TODO: Delete order from database
        }
    }
}