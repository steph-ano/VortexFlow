using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VortexFlow.Application.Cache;
using VortexFlow.Application.Events;
using VortexFlow.Application.Interfaces;
using VortexFlow.Infrastructure.Hubs;
using VortexFlow.Infrastructure.Messaging;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Tests;

public class MassTransitTests
{
    [Fact]
    public async Task TrendProcessedConsumer_ShouldConsumeEvent()
    {
        // Arrange
        var dbContextMock = new Mock<IApplicationDbContext>();
        
        // Mock DbSets
        var trendSnapshotsMock = new Mock<DbSet<TrendSnapshot>>();
        dbContextMock.Setup(d => d.TrendSnapshots).Returns(trendSnapshotsMock.Object);

        var cacheMock = new Mock<ITrendCache>();
        var hubContextMock = new Mock<IHubContext<TrendsHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        
        hubClientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

        var loggerMock = new Mock<ILogger<TrendProcessedConsumer>>();

        var services = new ServiceCollection();
        
        services.AddSingleton(dbContextMock.Object);
        services.AddSingleton(cacheMock.Object);
        services.AddSingleton(hubContextMock.Object);
        services.AddSingleton(loggerMock.Object);
        
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<TrendProcessedConsumer>();
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        
        await harness.Start();

        try
        {
            // Act
            var eventMessage = new TrendProcessedEvent
            {
                EventId = Guid.NewGuid(),
                Platform = "twitter",
                Hashtags = new[] { "test" },
                Source = "test-source",
                Timestamp = System.DateTime.UtcNow,
                Metrics = new TrendMetrics { Volume = 100, Sentiment = 0.5 }
            };

            await harness.Bus.Publish(eventMessage);

            // Assert
            Assert.True(await harness.Consumed.Any<TrendProcessedEvent>());
            Assert.True(await harness.GetConsumerHarness<TrendProcessedConsumer>().Consumed.Any<TrendProcessedEvent>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
