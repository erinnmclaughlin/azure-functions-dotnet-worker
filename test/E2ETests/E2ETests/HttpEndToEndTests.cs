// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Functions.Tests.E2ETests
{
    [Collection(Constants.FunctionAppCollectionName)]
    public class HttpEndToEndTests
    {
        private readonly FunctionAppFixture _fixture;

        public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _fixture.TestLogs.UseTestLogger(testOutputHelper);
        }

        [Theory]
        [InlineData("HelloPascal", "", HttpStatusCode.OK, "Hello!")]
        [InlineData("HelloAllCaps", "", HttpStatusCode.OK, "HELLO!")]
        [InlineData("HelloFromQuery", "?name=Test", HttpStatusCode.OK, "Hello Test")]
        [InlineData("HelloFromQuery", "?name=John&lastName=Doe", HttpStatusCode.OK, "Hello John")]
        [InlineData("HelloFromQuery", "?emptyProperty=&name=Jane", HttpStatusCode.OK, "Hello Jane")]
        [InlineData("HelloFromQuery", "?name=John&name=Jane", HttpStatusCode.OK, "Hello John,Jane")]
        [InlineData("HelloWithNoResponse", "", HttpStatusCode.NoContent, "")]
        [InlineData("ExceptionFunction", "", HttpStatusCode.InternalServerError, "")]
        [InlineData("HelloFromQuery", "", HttpStatusCode.BadRequest, "")]
        public async Task HttpTriggerTests(string functionName, string queryString, HttpStatusCode expectedStatusCode, string expectedMessage)
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, queryString);
            string actualMessage = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedStatusCode, response.StatusCode);

            if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.False(string.IsNullOrEmpty(actualMessage));
                Assert.Contains(expectedMessage, actualMessage);
            }
        }

        [Theory(Skip = "TODO: https://github.com/Azure/azure-functions-dotnet-worker/issues/1910")]
        [InlineData("HelloFromJsonBody", "{\"Name\": \"Whitney\"}", "application/json", HttpStatusCode.OK, "Hello Whitney")]
        [InlineData("HelloFromJsonBody", "{\"Name\": \"麵🍜\"}", "application/json", HttpStatusCode.OK, "Hello 麵🍜")]
        [InlineData("HelloFromJsonBody", "{\"Name\": \"Bob\"}", "application/octet-stream", HttpStatusCode.OK, "Hello Bob")]
        public async Task HttpTriggerTestsMediaTypeDoNotMatter(string functionName, string body, string mediaType, HttpStatusCode expectedStatusCode, string expectedBody)
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody(functionName, body, mediaType);
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedStatusCode, response.StatusCode);
            Assert.Equal(expectedBody, responseBody);
        }

        [Theory]
        [InlineData("PocoFromBody", "", "{ \"Name\": \"John\" }", "application/json", HttpStatusCode.OK, "Greetings John")]
        [InlineData("PocoBeforeRouteParameters", "eu/caller/", "{ \"Name\": \"b\" }", "application/json", HttpStatusCode.NoContent, "")]
        [InlineData("PocoAfterRouteParameters", "eu/caller/", "{ \"Name\": \"c\" }", "application/json", HttpStatusCode.OK, "eu caller c")]
        public async Task HttpTriggerTests_PocoFromBody(string functionName, string route, string body, string mediaType, HttpStatusCode expectedStatusCode, string expectedBody)
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody($"{route}{functionName}", body, mediaType);
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedStatusCode, response.StatusCode);
            Assert.Equal(expectedBody, responseBody);
        }

        [Theory]
        [InlineData("?someString=MyString&someInteger=42&SomeNullableInteger=24", "MyString", 42, 24)]
        [InlineData("?someString=MyString&someInteger=42", "MyString", 42, null)]
        public async Task HttpTriggerTests_PocoFromQuery(string queryString, string expectedStringValue, int expectedIntValue, int? expectedNullableIntValue)
        {
            var response = await HttpHelpers.InvokeHttpTrigger("PocoFromQuery", queryString);
            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedStringValue, responseBody.GetProperty("SomeString").GetString());
            Assert.Equal(expectedIntValue, responseBody.GetProperty("SomeInteger").GetInt32());

            if (expectedNullableIntValue.HasValue)
                Assert.Equal(expectedNullableIntValue, responseBody.GetProperty("SomeNullableInteger").GetInt32());
        }

        [Theory]
        [InlineData("")]
        [InlineData("?numbers=4", 4)]
        [InlineData("?numbers=4&numbers=2", 4, 2)]
        public async Task HttpTriggerTests_PocoFromQuery_Collections(string queryString, params int[] expectedNumbers)
        {
            var response = await HttpHelpers.InvokeHttpTrigger("PocoFromQuery", queryString);
            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedNumbers, responseBody.GetProperty("Numbers").EnumerateArray().Select(e => e.GetInt32()).ToArray());
        }

        [Fact]
        public async Task HttpTriggerTests_HelloFromQueryUsingAttribute()
        {
            var response = await HttpHelpers.InvokeHttpTrigger("HelloFromQueryUsingAttribute", "?name=John");
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello John", responseBody);
        }

        [Theory]
        [InlineData("", "0 + 0 = 0")]
        [InlineData("?number1=12", "12 + 0 = 12")]
        [InlineData("?number1=12&number2=30", "12 + 30 = 42")]
        public async Task HttpTriggerTests_SumFromQuery(string queryString, string expectedBody)
        {
            var response = await HttpHelpers.InvokeHttpTrigger("SumFromQuery", queryString);
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedBody, responseBody);
        }


        [Fact]
        public async Task HttpTriggerTests_PocoWithoutBindingSource()
        {
            const HttpStatusCode expectedStatusCode = HttpStatusCode.InternalServerError;

            HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("PocoWithoutBindingSource", "{ \"Name\": \"John\" }", "application/json");

            Assert.Equal(expectedStatusCode, response.StatusCode);
        }

        [Fact]
        public async Task HttpTriggerTestsPocoResult()
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("HelloUsingPoco", string.Empty, "application/json");
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"{{{Environment.NewLine}  \"Name\": \"Test\"{Environment.NewLine}}}", responseBody);
        }

        [Fact(Skip = "Proxies not currently supported in V4 but will be coming back.")]
        public async Task HttpProxy()
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("proxytest", string.Empty, "application/json");
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Proxy response", responseBody);
        }

        [Fact(Skip = "TODO: https://github.com/Azure/azure-functions-dotnet-worker/issues/133")]
        public async Task HttpTriggerWithCookieTests()
        {
            HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HttpTriggerSetsCookie");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<string> cookies = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.ToList();
        }

        [Fact(Skip = "TODO: https://github.com/Azure/azure-functions-dotnet-worker/issues/134")]
        public Task HttpTriggerBindingDataTests()
        {
            return Task.CompletedTask;
        }
    }
}
