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
                return Convert(context, requestData.Result, targetType, source);
            }

            return ConvertAsync(context, requestData, targetType, source);
        }

        private async ValueTask<object?> ConvertAsync(FunctionContext context, ValueTask<HttpRequestData?> requestDataResult, Type targetType, object? source)
        {
            var requestData = await requestDataResult;
            return Convert(context, requestData, targetType, source);
        }

        private ValueTask<object?> Convert(FunctionContext context, HttpRequestData? requestData, Type targetType, object? source)
        {
            if (requestData is null)
            {
                throw new InvalidOperationException($"The '{nameof(DefaultFromQueryConversionFeature)} expects an '{nameof(HttpRequestData)}' instance in the current context.");
            }

            return new ValueTask<object?>(ConvertQuery(context, requestData, targetType, source));
        }

        private static object? ConvertQuery(FunctionContext context, HttpRequestData requestData, Type targetType, object? source)
        {
            if (IsSimpleType(targetType))
            {
                return source is not null ? ParseSimpleType(targetType, source) : CreateDefaultInstanceOfType(targetType);
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

            foreach (var propertyName in query.AllKeys)
            {
                var value = query[propertyName];

                // skip if the target type does not contain a property with a matching name:
                if (targetType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) is not { } property)
                {
                    continue;
                }

                var propertyType = property.PropertyType;

                // Handle simple types:
                if (IsSimpleType(propertyType))
                {
                    dict.Add(propertyName, ParseSimpleType(propertyType, value));
                    continue;
                }
                
                // Handle collection types:
                if (typeof(IEnumerable).IsAssignableFrom(propertyType))
                {
                    if (TryGetArrayType(propertyType, out var arrayType))
                    {
                        var arrayValues = value.Split(',');

                        // Handle collections of simple types:
                        if (IsSimpleType(arrayType))
                        {
                            var parsedValues = arrayValues.Select(p => ParseSimpleType(arrayType, p)).ToArray();
                            dict.Add(propertyName, parsedValues);
                        }

                        // TODO: Handle collections of complex types
                        // ..

                        continue;
                    }
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

        private static object? ParseSimpleType(Type targetType, object? value)
        {
            if (value?.ToString() is not { Length: > 0 } stringValue)
            {
                return CreateDefaultInstanceOfType(targetType);
            }

            if (targetType == typeof(string))
            {
                return stringValue;
            }

            var typeConverter = TypeDescriptor.GetConverter(targetType);
            return typeConverter.ConvertFromString(null, CultureInfo.InvariantCulture, stringValue);
        }

        private static bool TryGetArrayType(Type propertyType, out Type arrayType)
        {
            arrayType = propertyType.GetElementType();

            if (arrayType is null && propertyType.ContainsGenericParameters)
            {
                arrayType = propertyType.GetGenericArguments().FirstOrDefault();
            }

            if (arrayType is null && propertyType.IsGenericType)
            {
                arrayType = propertyType.GenericTypeArguments.FirstOrDefault();
            }

            return arrayType is not null;
        }

        private static bool IsSimpleType(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } underlyingType)
            {
                type = underlyingType;
            }

            return type.IsPrimitive || type.IsEnum || type == typeof(string);
        }

        private static bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
    }
}
