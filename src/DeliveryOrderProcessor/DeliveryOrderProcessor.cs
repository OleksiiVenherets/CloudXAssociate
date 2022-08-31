using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using System;

namespace DeliveryOrderProcessor
{
    public class DeliveryOrderProcessor
    {
        private static readonly string EndpointUri = "https://cloudx-cosmosdb.documents.azure.com:443/";

        private static readonly string PrimaryKey = "";

        private CosmosClient _cosmosClient;

        private Database _database;

        private Container _container;

        private string _databaseId = "OrdersDatabase";

        private string _containerId = "OrdersContainer";

        [FunctionName("DeliveryOrderProcessor")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<Order>(requestBody);
                data.Id = Guid.NewGuid().ToString();

                _cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
                _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
                _container = await _database.CreateContainerIfNotExistsAsync(_containerId, "/id");
                var result = await _container.CreateItemAsync(data);


                return new OkObjectResult(result.StatusCode);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }
    }
}
