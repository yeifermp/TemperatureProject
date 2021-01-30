using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using Proxy.Config;
using Proxy.Models;
using Proxy.Repositories;

namespace Proxy
{
    internal interface IScopedMqttClientProcessingService
    {
        Task DoWork(CancellationToken cancellation);
    }

    public class MqttClientHostedService : BackgroundService
    {
        private IMqttClient _mqttClient;
        private readonly ILogger<MqttClientHostedService> _logger;
        private readonly MqttCredentialsOptions _credentialsOptions;
        private readonly MqttServerOptions _serverOptions;
        private readonly MqttTopicsOptions _topicOptions;
        private readonly ITelemetricDataRepository _telemetricRepository;


        public MqttClientHostedService(ILogger<MqttClientHostedService> logger,
            ITelemetricDataRepository telemetricRepository,
            IOptions<MqttCredentialsOptions> credentialOptions,
            IOptions<MqttServerOptions> serverOptions,
            IOptions<MqttTopicsOptions> topicOptions)
        {
            _logger = logger;
            _credentialsOptions = credentialOptions.Value;
            _serverOptions = serverOptions.Value;
            _topicOptions = topicOptions.Value;
            _telemetricRepository = telemetricRepository;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await InitAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            var options = new MqttClientDisconnectOptions();
            await _mqttClient.DisconnectAsync(options, cancellationToken);
            await base.StopAsync(cancellationToken);
        }

        private async Task InitAsync(CancellationToken cancellationToken)
        {
            var factory = new MqttFactory();

            if (_mqttClient == null)
                _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("ProxyApp")
                .WithTcpServer(_serverOptions.Servername, _serverOptions.Port)
                .WithCredentials(_credentialsOptions.Username, _credentialsOptions.Password)
                .WithCleanSession()
                .Build();

            _logger.LogInformation($"Connecting with {_serverOptions.Servername}:{_serverOptions.Port} server...");
            var result = await _mqttClient.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation($"Connection has been stabished.");

                _logger.LogInformation($"Subscribing to {_topicOptions.SensorTemperatureTopic} topic...");
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(_topicOptions.SensorTemperatureTopic)
                    .WithExactlyOnceQoS()
                    .Build());
                _logger.LogInformation($"Subscription to {_topicOptions.SensorTemperatureTopic} topic has been made.");
            }
            else
            {
                _logger.LogError($"Error trying to connect with {_serverOptions.Servername}:{_serverOptions.Port}.");
            }

            _mqttClient.UseDisconnectedHandler(e =>
            {
                _logger.LogInformation("### DISCONNECTED FROM SERVER ###");
            });

            _mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                if (e.ApplicationMessage.Topic == _topicOptions.SensorTemperatureTopic)
                {
                    try
                    {
                        var data = Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                                                .Split(" ");
                        _logger.LogInformation("Saving telemetric data...");
                        await _telemetricRepository.Save(new TelemetricData
                        {
                            Value = float.Parse(data[0]),
                            Device = new Device { Id = Guid.Parse(data[1]) },
                            Location = new Location() { Id = Guid.Parse("5c30586c-43dd-4749-b746-7463b7711bba") },
                            Type = TelemetricDataType.Temperature
                        });
                        _logger.LogInformation("Telemetric data was saved.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                }
            });
        }
    }
}