using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace durableFunc1
{
    public static class OrchFunc
    {
        private static readonly HttpClient client = new HttpClient();

        [FunctionName("OrchFunc")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var outputs = new List<string>();

            var itemsToProcess = new List<int>();
            for (int i = 0; i < 10; i++)   //900
            {
                itemsToProcess.Add(i);
            }



            int iteration = 0;
            foreach (var items in itemsToProcess.Chunk(2))
            {
                if (!context.IsReplaying) log.LogInformation("CallActivityAsync before iteration {ProcessItemsIteration}", iteration);
                outputs.Add(await context.CallActivityAsync<string>(nameof(ProcessItems), items));
                if (!context.IsReplaying) log.LogInformation("CallActivityAsync after iteration {ProcessItemsIteration}", iteration);
                iteration++;
            }

            
            // Replace "hello" with the name of your Durable Activity Function.
            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "Tokyo");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "Tokyo");

            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "Seattle");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "Seattle");

            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "London");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "London");


            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]

            log.LogInformation("OrchFunc finished with output: {OrchFuncOutput}", outputs);
            return outputs;
        }

        [FunctionName(nameof(ProcessItems))]
        public static async Task<string> ProcessItems([ActivityTrigger] List<int> items, ILogger log)
        {
            var results = new List<string>();
            results.Add("Start");
            foreach (var item in items)
            {
                var json = JsonConvert.SerializeObject(new { Name = item });
                //using var client = new HttpClient();
                var response = await client.PostAsync(
                    Environment.GetEnvironmentVariable("UrlHttpWaiter"),
                    new StringContent(json, Encoding.UTF8, "application/json"));

                log.LogInformation("Processed item: {ProcessedItem}", item);
                results.Add( $"({item} {response.StatusCode}: {(int)response.StatusCode})");
            }
            results.Add("End");

            log.LogInformation("Processed items: {ProcessedItems}", results);
            return $"Processed items: {JsonConvert.SerializeObject(results)}!";
        }


        [FunctionName(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation("Saying hello to {name}.", name);
            return $"Hello {name}!";
        }
        

        [FunctionName("OrchFunc_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("OrchFunc", null);

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}