// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.Http.Converters;
using Microsoft.Azure.Functions.Worker.Http;

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
                return ConvertRequestAsync(requestData.Result, targetType, context.CancellationToken);
            }

            return ConvertAsync(requestData, targetType, context.CancellationToken);
        }

        private async ValueTask<object?> ConvertAsync(ValueTask<HttpRequestData?> requestDataResult, Type targetType, CancellationToken cancellationToken)
        {
            var requestData = await requestDataResult;
            return await ConvertRequestAsync(requestData, targetType, cancellationToken);
        }

        private ValueTask<object?> ConvertRequestAsync(HttpRequestData? requestData, Type targetType, CancellationToken cancellationToken)
        {
            if (requestData is null)
            {
                throw new InvalidOperationException($"The '{nameof(DefaultFromQueryConversionFeature)} expects an '{nameof(HttpRequestData)}' instance in the current context.");
            }

            return ConvertQueryAsync(requestData, targetType, cancellationToken);
        }

        private static ValueTask<object?> ConvertQueryAsync(HttpRequestData requestData, Type targetType, CancellationToken cancellationToken)
        {
            var query = requestData.Query;

            // TODO: Implement a better way of doing this
            var obj = Activator.CreateInstance(targetType);

            foreach (var property in obj.GetType().GetProperties())
            {
                var valueAsString = query[property.Name];
                var typeConverter = TypeDescriptor.GetConverter(property.PropertyType);
                var value = typeConverter.ConvertFromString(null, CultureInfo.InvariantCulture, valueAsString);

                property.SetValue(obj, value, null);
            }

            return new ValueTask<object?>(obj);
        }
    }
}
