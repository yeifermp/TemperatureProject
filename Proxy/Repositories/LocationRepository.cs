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
    public interface ILocationRepository 
    {
        Task Save(Location data);
    }

    public class LocationRepository : ILocationRepository
    {
        private readonly CosmosDbOptions _options;
        private readonly GremlinServer _server;
        private readonly ILogger<LocationRepository> _logger;

        public LocationRepository(IOptions<CosmosDbOptions> options, 
            ILogger<LocationRepository> logger)
        {
            _options = options.Value;
            _logger = logger;

            _server = new GremlinServer(_options.Hostname, 
                _options.Port, 
                true, 
                $"/dbs/ {_options.Database}/colls/{_options.TelemetricDataCollectionName}",
                _options.AuthKey);

        }
        
        public async Task Save(Location location)
        {
            using (var gremlinClient = new GremlinClient(_server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            {
                string addQuery = $@"g.AddV('{_options.TelemetricDataCollectionName}')
                    .property('name', '{location.Id}')
                    .property('locationId', '{location.Name}')
                    .property('id', '{location.Id}')";

                try
                {
                    await gremlinClient.SubmitAsync<dynamic>(addQuery);
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