// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.Azure.Functions.Worker.Invocation
{
    internal interface IMethodInvokerFactory
    {
        IMethodInvoker<TInstance, TReturn> Create<TInstance, TReturn>(MethodInfo method);
    }
}
