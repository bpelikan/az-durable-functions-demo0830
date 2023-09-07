using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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

            log.LogInformation("Task.Delay before");
            //await Task.Delay(4000);
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(4), CancellationToken.None);
            log.LogInformation("Task.Delay after");


            int iteration = 0;
            foreach (var items in itemsToProcess.Chunk(2))
            {
                context.SetCustomStatus($"Iteration {iteration}");

                if (!context.IsReplaying) log.LogInformation("CallActivityAsync before iteration {ProcessItemsIteration}", iteration);
                outputs.Add(await context.CallActivityAsync<string>(nameof(ProcessItems), items));
                if (!context.IsReplaying) log.LogInformation("CallActivityAsync after iteration {ProcessItemsIteration}", iteration);
                iteration++;
            }

            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), $"1: {DateTime.Now}"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), $"2: {DateTime.Now}"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), $"3: {DateTime.Now}"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), $"4: {DateTime.Now}"));



            // Replace "hello" with the name of your Durable Activity Function.
            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "Tokyo");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            context.SetCustomStatus("Tokyo");
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "Tokyo");

            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "Seattle");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            context.SetCustomStatus("Seattle");
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "Seattle");

            log.LogInformation("CallActivityAsync before iteration {SayHelloInput}", "London");
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));
            context.SetCustomStatus("London");
            log.LogInformation("CallActivityAsync after iteration {SayHelloInput}", "London");


            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]


            TimeSpan timeout = TimeSpan.FromSeconds(30);
            DateTime deadline = context.CurrentUtcDateTime.Add(timeout);
            using (var cts = new CancellationTokenSource())
            {
                Task activityTask = context.CallActivityAsync(nameof(SayHello), "GetQuote");
                Task timeoutTask = context.CreateTimer(deadline, cts.Token);

                Task winner = await Task.WhenAny(activityTask, timeoutTask);
                if (winner == activityTask)
                {
                    // success case
                    //cts.Cancel();     //runtimeStatus will be not Completed until all outstanding tasks, including durable timer tasks, are either completed or canceled.
                    log.LogWarning("timer TRUE");
                }
                else
                {
                    // timeout case
                    log.LogWarning("Timer FALSE");
                }
            }

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