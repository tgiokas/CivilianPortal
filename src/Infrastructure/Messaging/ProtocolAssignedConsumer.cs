using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Infrastructure.Messaging;

/// Kafka consumer that listens for protocol assignment events from DMS.
/// When DMS finishes processing a citizen application, it publishes to
/// the protocol topic. This consumer picks it up and updates the CitizenPortal DB.

public class ProtocolAssignedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<ProtocolAssignedConsumer> _logger;
    private readonly string _topic;

    public ProtocolAssignedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<ProtocolAssignedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var settings = kafkaOptions.Value;
        _topic = settings.ProtocolTopic;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs,            
            
            GroupId = settings.GroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
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
                    var result = _consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is null) continue;

                    _logger.LogInformation("Received protocol assignment: {Key}", result.Message.Key);

                    var payload = JsonSerializer.Deserialize<ProtocolAssignedEvent>(
                        result.Message.Value,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    if (payload is not null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                        await appService.UpdateStatusFromDmsAsync(payload);
                    }

                    _consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in ProtocolAssignedConsumer");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }, stoppingToken);

        _consumer.Close();
        _logger.LogInformation("ProtocolAssignedConsumer stopped.");
    }
}