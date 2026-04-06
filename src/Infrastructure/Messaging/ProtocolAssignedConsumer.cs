using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;

/// <summary>
/// Kafka consumer that listens for protocol assignment events from DMS.
/// When DMS finishes processing a citizen application, it publishes to
/// "citizen.application.protocol-assigned" topic. This consumer picks it
/// up and updates the CitizenPortal DB.
/// </summary>
public class ProtocolAssignedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<ProtocolAssignedConsumer> _logger;
    private readonly string _topic;

    public ProtocolAssignedConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ProtocolAssignedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _topic = config["PORTAL_KAFKA_PROTOCOL_TOPIC"] ?? "citizen.application.protocol-assigned";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(config), "KAFKA_BOOTSTRAP_SERVERS is not set."),
            GroupId = config["PORTAL_KAFKA_CONSUMER_GROUP"] ?? "citizen-portal-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProtocolAssignedConsumer started. Subscribing to {Topic}", _topic);
        _consumer.Subscribe(_topic);

        await Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null) continue;

                    _logger.LogDebug("Consumed message from {Topic}: {Key}", _topic, result.Message.Key);

                    var protocolEvent = JsonSerializer.Deserialize<ProtocolAssignedEvent>(result.Message.Value);
                    if (protocolEvent is null)
                    {
                        _logger.LogWarning("Failed to deserialize ProtocolAssignedEvent");
                        _consumer.Commit(result);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var applicationService = scope.ServiceProvider.GetRequiredService<IApplicationService>();
                    var updateResult = await applicationService.UpdateStatusFromDmsAsync(protocolEvent);

                    if (updateResult.Success)
                    {
                        _logger.LogInformation("Protocol {ProtocolNumber} assigned to application {PublicId}",
                            protocolEvent.ProtocolNumber, protocolEvent.ApplicationPublicId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update status: {Error}", updateResult.Message);
                    }

                    _consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing protocol-assigned event");
                }
            }
        }, stoppingToken);

        _consumer.Close();
        _logger.LogInformation("ProtocolAssignedConsumer stopped.");
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
