using System;
using System.Threading.Tasks;
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proxy.Config;
using Proxy.Models;

namespace Proxy.Repositories 
{
    public interface IDeviceRepository 
    {
        Task Save(Device data);
    }

    public class DeviceRepository : IDeviceRepository
    {
        private readonly CosmosDbOptions _options;
        private readonly GremlinServer _server;
        private readonly ILogger<DeviceRepository> _logger;

        public DeviceRepository(IOptions<CosmosDbOptions> options,
            ILogger<DeviceRepository> logger)
        {
            _options = options.Value;
            _logger = logger;
            _server = new GremlinServer(_options.Hostname, 
                _options.Port, 
                true, 
                $"/dbs/ {_options.Database}/colls/{_options.TelemetricDataCollectionName}",
                _options.AuthKey);

        }

        public async Task Save(Device device)
        {
            using (var gremlinClient = new GremlinClient(_server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            {
                string addQuery = $@"g.AddV('Device')
                    .property('id', '{device.Id}')
                    .property('name', '{device.Name}')
                    .property('locationId', '{device.Location.Id}')";

                string addEdgeQuery = $@"g.V('{device.Location.Id}')
                    .addE('has')
                    .to(g.V('{device.Id}'))";

                try
                {
                    await gremlinClient.SubmitAsync<dynamic>(addQuery);
                    await gremlinClient.SubmitAsync<dynamic>(addEdgeQuery);
                }
                catch (ResponseException e)
                {
                    _logger.LogError("Request Error!");
                    _logger.LogError($"StatusCode: {e.StatusCode}");
                }
            }
        }
    }
}