using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace OrderItemsReserver
{
    public class OrderItemsReserver
    {
        private readonly ILogger<OrderItemsReserver> _logger;

        public OrderItemsReserver(ILogger<OrderItemsReserver> log)
        {
            _logger = log;
        }

        [FunctionName("OrderItemsReserver")]
        public async Task Run([ServiceBusTrigger("orderitemsrecerver", "OrderItemsReserver", Connection = "ServiceBusConnectionString")]string mySbMsg)
        {
            try
            {
                var connectionString = "";
                string containerName = "orders";
                var serviceClient = new BlobServiceClient(connectionString);

                var containerClient = serviceClient.GetBlobContainerClient(containerName);
                var fileName = $"Order-{DateTime.UtcNow:yyyy-MM-ddTHH-mm-ss}.json";

                var blockBlobClient = containerClient.GetBlockBlobClient(fileName);
                var options = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    }
                };

                using var ms = new MemoryStream();
                var writer = new StreamWriter(ms);
                writer.Write(mySbMsg);
                writer.Flush();
                ms.Position = 0;
                await blockBlobClient.UploadAsync(ms, options);
            }
            catch (Exception ex)
            {
                var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient();

                var logicAppUrl = "https://prod-232.westeurope.logic.azure.com:443/workflows/9a5118982b19404b90b786fc93bd1109/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=hiUVZ6qaUuYAxXHBTpPv9HmuoNy4H8eQvB_7XWMyDiQ";

                var requestData = new
                {
                    Reason = ex.Message,
                    ErrorMessage = ex.StackTrace,
                    OrderDetails = mySbMsg
                };

                var json = JsonConvert.SerializeObject(requestData);
                var data = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync(logicAppUrl, data);
            }
        }
    }
}
