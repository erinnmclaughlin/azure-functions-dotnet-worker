// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.E2EApp
{
    public static class BasicHttpFunctions
    {
        private const string LogMessage = ".NET Worker HTTP trigger function processed a request";

        [Function("HelloPascal")]
        public static HttpResponseData Hello(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(Hello));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("Hello!");
            return response;
        }

        [Function("HelloAllCaps")]
        public static HttpResponseData HELLO(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(HELLO));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("HELLO!");
            return response;
        }

        [Function(nameof(HelloFromQuery))]
        public static HttpResponseData HelloFromQuery(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(HelloFromQuery));
            logger.LogInformation(LogMessage);

            var queryName = req.Query["name"];

            if (!string.IsNullOrEmpty(queryName))
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.WriteString("Hello " + queryName);
                return response;
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        [Function(nameof(HelloFromQueryUsingAttribute))]
        public static HttpResponseData HelloFromQueryUsingAttribute(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context,
            [FromQuery] string name)
        {
            var logger = context.GetLogger(nameof(HelloFromQueryUsingAttribute));
            logger.LogInformation(LogMessage);

            if (!string.IsNullOrEmpty(name))
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.WriteString("Hello " + name);
                return response;
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        [Function(nameof(HelloFromJsonBody))]
        public static HttpResponseData HelloFromJsonBody(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(HelloFromJsonBody));
            logger.LogInformation(LogMessage);
            var body = req.ReadAsString();

            if (!string.IsNullOrEmpty(body))
            {
                var serializedBody = (CallerName)JsonSerializer.Deserialize(body, typeof(CallerName));
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.WriteString("Hello " + serializedBody.Name);
                return response;
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        [Function(nameof(SumFromQuery))]
        public static HttpResponseData SumFromQuery(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context,
            [FromQuery] int number1,
            [FromQuery] int number2)
        {
            var logger = context.GetLogger(nameof(SumFromQuery));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString($"{number1} + {number2} = {number1 + number2}");
            return response;
        }

        public class CallerName
        {
            public string Name { get; set; }
        }

        public class MyResponse
        {
            public string Name { get; set; }
        }

        [Function(nameof(HelloUsingPoco))]
        public static MyResponse HelloUsingPoco(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(HelloUsingPoco));
            logger.LogInformation(LogMessage);

            return new MyResponse { Name = "Test" };
        }

        [Function(nameof(HelloWithNoResponse))]
        public static Task HelloWithNoResponse(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(HelloWithNoResponse));
            logger.LogInformation(LogMessage);

            return Task.CompletedTask;
        }

        [Function(nameof(PocoFromBody))]
        public static HttpResponseData PocoFromBody(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req,
            [FromBody] CallerName caller,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(PocoFromBody));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString($"Greetings {caller.Name}");
            return response;
        }

        [Function(nameof(PocoBeforeRouteParameters))]
        public static Task PocoBeforeRouteParameters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{region}/{category}/" + nameof(PocoBeforeRouteParameters))] [FromBody] CallerName caller,
            string region,
            string category,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(PocoBeforeRouteParameters));
            logger.LogInformation(LogMessage);
            return Task.CompletedTask;
        }

        [Function(nameof(PocoAfterRouteParameters))]
        public static HttpResponseData PocoAfterRouteParameters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{region}/{category}/" + nameof(PocoAfterRouteParameters))] HttpRequestData req,
            string region,
            string category,
            [FromBody] CallerName caller,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(PocoAfterRouteParameters));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString($"{region} {category} {caller.Name}");
            return response;
        }

        public class PocoFromQueryModel
        {
            public string SomeString { get; set; }
            public int SomeInteger { get; set; }
            public int? SomeNullableInteger { get; set; }
            public List<int> Numbers { get; set; } = [];
        }

        [Function(nameof(PocoFromQuery))]
        public static async Task<HttpResponseData> PocoFromQuery(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestData req,
            [FromQuery] PocoFromQueryModel model,
            FunctionContext context)
        {
            var logger = context.GetLogger(nameof(PocoFromQuery));
            logger.LogInformation(LogMessage);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(model);
            return response;
        }
    }
}
