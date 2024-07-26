// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
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

        public ValueTask<object?> ConvertAsync(FunctionContext context, Type targetType)
        {
            var requestData = context.GetHttpRequestDataAsync();

            if (requestData.IsCompletedSuccessfully)
            {
                return ConvertRequestAsync(context, requestData.Result, targetType);
            }

            return ConvertAsync(context, requestData, targetType);
        }

        private async ValueTask<object?> ConvertAsync(FunctionContext context, ValueTask<HttpRequestData?> requestDataResult, Type targetType)
        {
            var requestData = await requestDataResult;
            return await ConvertRequestAsync(context, requestData, targetType);
        }

        private ValueTask<object?> ConvertRequestAsync(FunctionContext context, HttpRequestData? requestData, Type targetType)
        {
            if (requestData is null)
            {
                throw new InvalidOperationException($"The '{nameof(DefaultFromQueryConversionFeature)} expects an '{nameof(HttpRequestData)}' instance in the current context.");
            }

            return ConvertQueryAsync(context, requestData, targetType);
        }

        private static ValueTask<object?> ConvertQueryAsync(FunctionContext context, HttpRequestData requestData, Type targetType)
        {
            var serializer = requestData.FunctionContext.InstanceServices.GetService<IOptions<WorkerOptions>>()?.Value?.Serializer
                ?? throw new InvalidOperationException("A serializer is not configured for the worker.");

            var queryParamsAsDictionary = GetObjectDictionary(targetType, requestData.Query);
            var stream = serializer.Serialize(queryParamsAsDictionary).ToStream();
            var obj = serializer.Deserialize(stream, targetType, context.CancellationToken);
            return new ValueTask<object?>(obj);
        }

        private static Dictionary<string, object?> GetObjectDictionary(Type targetType, NameValueCollection collection)
        {
            const BindingFlags propertyFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

            var dict = new Dictionary<string, object?>();

            foreach (var key in collection.AllKeys)
            {
                var value = collection[key];

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (targetType.GetProperty(key, propertyFlags) is { } property)
                {
                    var typeConverter = TypeDescriptor.GetConverter(property.PropertyType);
                    var convertedValue = typeConverter.ConvertFromString(null, CultureInfo.InvariantCulture, value);

                    dict.Add(key, convertedValue);
                }
            }

            return dict;
        }
    }
}
