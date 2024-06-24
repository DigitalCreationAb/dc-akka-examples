// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Tools.Singleton;
using Akka.Hosting;
using Akka.Persistence.EventStore.Hosting;
using Akka.Remote.Hosting;
using DC.Akka.Examples.Projections;
using Microsoft.Extensions.Hosting;

await Host.CreateDefaultBuilder(args)
    .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
    .ConfigureServices((_, services) =>
    {
        services.AddAkka("projections-example", (builder, _) =>
        {
            builder
                .WithEventStorePersistence("tcp://admin:changeit@localhost:1113")
                .WithRemoting("localhost", 1660)
                .WithClustering(new ClusterOptions
                {
                    SeedNodes = ["localhost:1660"]
                })
                .WithActors((system, registry) =>
                {
                    var projectionCoordinator = system.ActorOf(ClusterSingletonManager.Props(
                            singletonProps: Props.Create(() => new OrderInformationProjectionCoordinator()),
                            terminationMessage: PoisonPill.Instance,
                            settings: ClusterSingletonManagerSettings.Create(system)),
                        name: "order-projection-coordinator");
                    
                    registry.Register<OrderInformationProjectionCoordinator>(projectionCoordinator);
                });
        });
    })
    .Build()
    .RunAsync();