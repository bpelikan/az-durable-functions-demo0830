using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading;

namespace durableFunc1
{
    public static class HttpWaiter
    {
        [FunctionName("HttpWaiter")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.Name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "No name in request"
                : $"Name in request: {name}";

            //Thread.Sleep(1000);
            await Task.Delay(1000);
            log.LogInformation("Name in HTTP request: {Name}", name);
            return new OkObjectResult(responseMessage);
        }
    }
}
