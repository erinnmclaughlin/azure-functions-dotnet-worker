// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Http.Converters;

namespace Microsoft.Azure.Functions.Worker.Http
{
    /// <summary>
    /// Specifies that a parameter should be bound using the HTTP request query string when using the <see cref="HttpTriggerAttribute"/>.
    /// </summary>
    public class FromQueryAttribute() : InputConverterAttribute(typeof(FromQueryConverter))
    {
    }
}
