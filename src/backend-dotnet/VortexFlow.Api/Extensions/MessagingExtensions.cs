using MassTransit;
using VortexFlow.Infrastructure.Messaging;

namespace VortexFlow.Api.Extensions;

/// <summary>
/// Messaging-layer bootstrap: MassTransit with RabbitMQ as the transport,
/// plus the consumer registration and the dead-letter/retry topology.
///
/// The consumer is wired by type (<see cref="TrendProcessedConsumer"/>) so
/// the transport binding stays declarative; adding a new consumer is a
/// single <c>x.AddConsumer&lt;NewConsumer&gt;()</c> line away.
/// </summary>
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddVortexFlowMessaging(this WebApplicationBuilder builder)
    {
        var rabbitConnStr = builder.Configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is not configured.");

        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<TrendProcessedConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConnStr);
                cfg.ReceiveEndpoint("trends_processed_queue", e =>
                {
                    e.ConfigureConsumer<TrendProcessedConsumer>(context);
                    // Exponential retry on transient failure; final failure
                    // dead-letters the message so it can be inspected and
                    // re-driven by an operator.
                    e.UseMessageRetry(r =>
                        r.Exponential(
                            5,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(30),
                            TimeSpan.FromSeconds(1)));
                });
            });
        });
        return builder;
    }
}
