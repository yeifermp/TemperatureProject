using System;
using System.Collections.Generic;
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
    public interface ITelemetricDataRepository 
    {
        Task Save(TelemetricData data);
    }

    public class TelemetricDataRepository : ITelemetricDataRepository
    {
        private readonly CosmosDbOptions _options;
        private readonly GremlinServer _server;
        private ILogger<TelemetricDataRepository> _logger;

        public TelemetricDataRepository(IOptions<CosmosDbOptions> options,
            ILogger<TelemetricDataRepository> logger)
        {
            _options = options.Value;
            _logger = logger;
            _server = new GremlinServer(_options.Hostname, 
                _options.Port, 
                true, 
                $"/dbs/{_options.Database}/colls/{_options.TelemetricDataCollectionName}",
                _options.AuthKey);

        }

        public async Task Save(TelemetricData data)
        {            
            using (var gremlinClient = new GremlinClient(_server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            {
                if(data.Id == Guid.Empty)
                    data.Id = Guid.NewGuid();

                data.DateUtc = DateTime.UtcNow;

                string query = $@"g.addV('TelemetricData')
                    .property('id', '{data.Id}')
                    .property('value', '{data.Value}')
                    .property('dateUtc', '{data.DateUtc}')
                    .property('type', '{data.Type.Name}')
                    .property('locationId', '{data.Location.Id}')";

                string addEdgeLocationQuery = $@"g.V('{data.Device.Id}')
                    .addE('produces')
                    .to(g.V('{data.Id}'))";

                try
                {
                    _logger.LogInformation(query);
                    _logger.LogInformation(addEdgeLocationQuery);

                    await gremlinClient.SubmitAsync<dynamic>(query);
                    await gremlinClient.SubmitAsync<dynamic>(addEdgeLocationQuery);
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