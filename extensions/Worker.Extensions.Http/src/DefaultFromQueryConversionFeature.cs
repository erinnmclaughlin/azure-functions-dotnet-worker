// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.Http.Converters;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.Http
{
    internal class DefaultFromQueryConversionFeature : IFromQueryConversionFeature
    {
        internal static IFromQueryConversionFeature Instance { get; } = new DefaultFromQueryConversionFeature();

        public ValueTask<object?> ConvertAsync(FunctionContext context, Type targetType, object? source)
        {
            var requestData = context.GetHttpRequestDataAsync();

            if (requestData.IsCompletedSuccessfully)
            {
                return new ValueTask<object?>(Convert(context, requestData.Result, targetType, source));
            }

            return ConvertAsync(context, requestData, targetType, source);
        }

        private async ValueTask<object?> ConvertAsync(FunctionContext context, ValueTask<HttpRequestData?> requestDataResult, Type targetType, object? source)
        {
            var requestData = await requestDataResult;
            return Convert(context, requestData, targetType, source);
        }

        private object? Convert(FunctionContext context, HttpRequestData? requestData, Type targetType, object? source)
        {
            if (requestData is null)
            {
                throw new InvalidOperationException($"The '{nameof(DefaultFromQueryConversionFeature)} expects an '{nameof(HttpRequestData)}' instance in the current context.");
            }

            return ConvertQuery(context, requestData, targetType, source);
        }

        private static object? ConvertQuery(FunctionContext context, HttpRequestData requestData, Type targetType, object? source)
        {
            if (QueryStringUtilities.TryConvertToType(source, targetType, out var result))
            {
                return result;
            }

            var serializer = context.InstanceServices.GetService<IOptions<WorkerOptions>>()?.Value?.Serializer
                ?? throw new InvalidOperationException("A serializer is not configured for the worker.");

            var queryParamsAsDictionary = requestData.Query.ToObjectDictionary(targetType);

            var stream = serializer.Serialize(queryParamsAsDictionary).ToStream();
            return serializer.Deserialize(stream, targetType, context.CancellationToken);
        }
    }
}
