using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
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
        private readonly IotHubOptions _iotHubOptions;
        private DeviceClient _deviceClient;
 
        public MqttClientHostedService(ILogger<MqttClientHostedService> logger,
            ITelemetricDataRepository telemetricRepository,
            IOptions<MqttCredentialsOptions> credentialOptions,
            IOptions<MqttServerOptions> serverOptions,
            IOptions<MqttTopicsOptions> topicOptions,
            IOptions<IotHubOptions> iotHubOptions)
        {
            _logger = logger;
            _credentialsOptions = credentialOptions.Value;
            _serverOptions = serverOptions.Value;
            _topicOptions = topicOptions.Value;
            _telemetricRepository = telemetricRepository;
            _iotHubOptions = iotHubOptions.Value;
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

            await ConnectAsync(cancellationToken);

            _mqttClient.UseDisconnectedHandler(e =>
            {
                if(_deviceClient != null)
                    _deviceClient.Dispose();

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
                        var telemetricData = new TelemetricData
                        {
                            Value = float.Parse(data[0]),
                            Device = new Device { Id = Guid.Parse(data[1]) },
                            Location = new Location() { Id = Guid.Parse("8bd76e47-6b20-4107-ba43-34088f085343") },
                            Type = TelemetricDataType.Temperature
                        };
                        _logger.LogInformation(telemetricData.Value.ToString());
                        await SendToCloudHub(telemetricData);
                        _logger.LogInformation("Telemetric data was saved.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                }
            });
        }

        private async Task ConnectAsync(CancellationToken cancellationToken) 
        {
            var options = new MqttClientOptionsBuilder()
                .WithClientId("ProxyApp")
                .WithTcpServer(_serverOptions.Servername, _serverOptions.Port)
                .WithCredentials(_credentialsOptions.Username, _credentialsOptions.Password)
                .WithCleanSession()
                .Build();

            try
            {
                _logger.LogInformation($"Connecting with {_serverOptions.Servername}:{_serverOptions.Port} server...");
                var result = await _mqttClient.ConnectAsync(options, cancellationToken);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _logger.LogInformation($"Creating IoT Hub client...");
                    _deviceClient = DeviceClient.CreateFromConnectionString(_iotHubOptions.ESP01ConnectionString);
                    _logger.LogInformation($"IoT Hub client has been created.");

                    _logger.LogInformation($"Connection has been stabished.");

                    _logger.LogInformation($"Subscribing to {_topicOptions.SensorTemperatureTopic} topic...");
                    await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                        .WithTopic(_topicOptions.SensorTemperatureTopic)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                        //.WithAtMostOnceQoS()
                        .Build());
                    _logger.LogInformation($"Subscription to {_topicOptions.SensorTemperatureTopic} topic has been made.");
                }
                else
                {
                    _logger.LogError($"Error trying to connect with {_serverOptions.Servername}:{_serverOptions.Port}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task SendToCloudHub(TelemetricData data) 
        {
            var messageBody = JsonSerializer.Serialize(data);

            using var message = new Message(Encoding.ASCII.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _deviceClient.SendEventAsync(message);
        }
    }
}