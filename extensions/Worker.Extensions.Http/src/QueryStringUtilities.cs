using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.Functions.Worker.Extensions.Http;

internal static class QueryStringUtilities
{
    public static bool TryConvertToType(object? source, Type targetType, out object? result)
    {
        if (IsSimpleType(targetType))
        {
            result = source is not null ? ConvertSimpleType(targetType, source) : CreateDefaultInstanceOfType(targetType);
            return true;
        }

        result = null;
        return false;
    }

    public static Dictionary<string, object?> ToObjectDictionary(this NameValueCollection query, Type targetType)
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

    private static object? CreateDefaultInstanceOfType(Type targetType)
    {
        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
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
}
