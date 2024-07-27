// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
            if (IsSimpleType(targetType))
            {
                return source is not null ? ConvertSimpleType(targetType, source) : CreateDefaultInstanceOfType(targetType);
            }

            return DeserializeQuery(context, targetType, requestData.Query);
        }

        private static object? DeserializeQuery(FunctionContext context, Type targetType, NameValueCollection query)
        {
            var serializer = context.InstanceServices.GetService<IOptions<WorkerOptions>>()?.Value?.Serializer
                ?? throw new InvalidOperationException("A serializer is not configured for the worker.");

            // Convert query string to dictionary type:
            var queryParamsAsDictionary = GetObjectDictionary(targetType, query);

            // Use object serializer to convert from dictionary to target type:
            var stream = serializer.Serialize(queryParamsAsDictionary).ToStream();
            return serializer.Deserialize(stream, targetType, context.CancellationToken);
        }

        private static Dictionary<string, object?> GetObjectDictionary(Type targetType, NameValueCollection query)
        {
            var dict = new Dictionary<string, object?>();
            const BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

            foreach (var propertyName in query.AllKeys)
            {
                // Skip if the target type does not contain a property with a matching name:
                if (string.IsNullOrEmpty(propertyName) || targetType.GetProperty(propertyName, bindingFlags)?.PropertyType is not { } propertyType)
                {
                    continue;
                }

                // Handle simple types:
                if (IsSimpleType(propertyType))
                {
                    dict.Add(propertyName, ConvertSimpleType(propertyType, query[propertyName]));
                    continue;
                }
                
                // Handle collection types:
                if (typeof(IEnumerable).IsAssignableFrom(propertyType))
                {
                    var arrayType = GetArrayType(propertyType);
                    var arrayValues = query.GetValues(propertyName);

                    // Handle collections of simple types:
                    if (IsSimpleType(arrayType))
                    {
                        var parsedValues = arrayValues.Select(p => ConvertSimpleType(arrayType, p)).ToArray();
                        dict.Add(propertyName, parsedValues);
                    }

                    // TODO: Handle collections of complex types
                    // ..
                    
                    continue;
                }

                // TODO: Handle complex types
                // ..
            }

            return dict;
        }

        private static object? CreateDefaultInstanceOfType(Type targetType)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        private static object? ConvertSimpleType(Type targetType, object? valueToConvert)
        {
            var stringValue = valueToConvert?.ToString();

            if (targetType == typeof(string))
            {
                return stringValue;
            }

            if (string.IsNullOrEmpty(stringValue))
            {
                return CreateDefaultInstanceOfType(targetType);
            }

            var typeConverter = TypeDescriptor.GetConverter(targetType);

            if (typeConverter.IsValid(stringValue))
            {
                return typeConverter.ConvertFromString(null, CultureInfo.InvariantCulture, stringValue);
            }

            return CreateDefaultInstanceOfType(targetType);
        }

        private static Type GetArrayType(Type propertyType)
        {
            return propertyType.GetElementType() ?? propertyType.GenericTypeArguments[0];
        }

        private static bool IsSimpleType(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } underlyingType)
            {
                type = underlyingType;
            }

            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan)
                || type == typeof(Guid);
        }

        private static bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
    }
}
