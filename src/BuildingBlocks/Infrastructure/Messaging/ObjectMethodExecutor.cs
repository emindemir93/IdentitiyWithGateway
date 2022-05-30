// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Internal
{
    internal class ObjectMethodExecutor
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ConstructorInfo _objectMethodExecutorAwaitableConstructor =
            typeof(ObjectMethodExecutorAwaitable).GetConstructor(new[]
            {
                typeof(object), // customAwaitable
                typeof(Func<object, object>), // getAwaiterMethod
                typeof(Func<object, bool>), // isCompletedMethod
                typeof(Func<object, object>), // getResultMethod
                typeof(Action<object, Action>), // onCompletedMethod
                typeof(Action<object, Action>) // unsafeOnCompletedMethod
            });

        private readonly MethodExecutor _executor;
        private readonly MethodExecutorAsync _executorAsync;
        private readonly object[] _parameterDefaultValues;

        private ObjectMethodExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo, object[] parameterDefaultValues)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            MethodInfo = methodInfo;
            MethodParameters = methodInfo.GetParameters();
            TargetTypeInfo = targetTypeInfo;
            MethodReturnType = methodInfo.ReturnType;

            var isAwaitable = CoercedAwaitableInfo.IsTypeAwaitable(MethodReturnType, out var coercedAwaitableInfo);

            IsMethodAsync = isAwaitable;
            AsyncResultType = isAwaitable ? coercedAwaitableInfo.AwaitableInfo.ResultType : null;

            // Upstream code may prefer to use the sync-executor even for async methods, because if it knows
            // that the result is a specific Task<T> where T is known, then it can directly cast to that type
            // and await it without the extra heap allocations involved in the _executorAsync code path.
            _executor = GetExecutor(methodInfo, targetTypeInfo);

            if (IsMethodAsync)
            {
                _executorAsync = GetExecutorAsync(methodInfo, targetTypeInfo, coercedAwaitableInfo);
            }

            _parameterDefaultValues = parameterDefaultValues;
        }

        public MethodInfo MethodInfo { get; }

        public ParameterInfo[] MethodParameters { get; }

        public TypeInfo TargetTypeInfo { get; }

        public Type AsyncResultType { get; }

        // This field is made internal set because it is set in unit tests.
        public Type MethodReturnType { get; internal set; }

        public bool IsMethodAsync { get; }

        public static ObjectMethodExecutor Create(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            return new ObjectMethodExecutor(methodInfo, targetTypeInfo, null);
        }

        public static ObjectMethodExecutor Create(MethodInfo methodInfo, TypeInfo targetTypeInfo,
            object[] parameterDefaultValues)
        {
            if (parameterDefaultValues == null)
            {
                throw new ArgumentNullException(nameof(parameterDefaultValues));
            }

            return new ObjectMethodExecutor(methodInfo, targetTypeInfo, parameterDefaultValues);
        }

        /// <summary>
        /// Executes the configured method on <paramref name="target" />. This can be used whether or not
        /// the configured method is asynchronous.
        /// </summary>
        /// <remarks>
        /// Even if the target method is asynchronous, it's desirable to invoke it using Execute rather than
        /// ExecuteAsync if you know at compile time what the return type is, because then you can directly
        /// "await" that value (via a cast), and then the generated code will be able to reference the
        /// resulting awaitable as a value-typed variable. If you use ExecuteAsync instead, the generated
        /// code will have to treat the resulting awaitable as a boxed object, because it doesn't know at
        /// compile time what type it would be.
        /// </remarks>
        /// <param name="target">The object whose method is to be executed.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>The method return value.</returns>
        public object Execute(object target, params object[] parameters)
        {
            return _executor(target, parameters);
        }

        /// <summary>
        /// Executes the configured method on <paramref name="target" />. This can only be used if the configured
        /// method is asynchronous.
        /// </summary>
        /// <remarks>
        /// If you don't know at compile time the type of the method's returned awaitable, you can use ExecuteAsync,
        /// which supplies an awaitable-of-object. This always works, but can incur several extra heap allocations
        /// as compared with using Execute and then using "await" on the result value typecasted to the known
        /// awaitable type. The possible extra heap allocations are for:
        /// 1. The custom awaitable (though usually there's a heap allocation for this anyway, since normally
        /// it's a reference type, and you normally create a new instance per call).
        /// 2. The custom awaiter (whether or not it's a value type, since if it's not, you need a new instance
        /// of it, and if it is, it will have to be boxed so the calling code can reference it as an object).
        /// 3. The async result value, if it's a value type (it has to be boxed as an object, since the calling
        /// code doesn't know what type it's going to be).
        /// </remarks>
        /// <param name="target">The object whose method is to be executed.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>An object that you can "await" to get the method return value.</returns>
        public ObjectMethodExecutorAwaitable ExecuteAsync(object target, params object[] parameters)
        {
            return _executorAsync(target, parameters);
        }

        public object GetDefaultValueForParameter(int index)
        {
            if (_parameterDefaultValues == null)
            {
                throw new InvalidOperationException(
                    $"Cannot call {nameof(GetDefaultValueForParameter)}, because no parameter default values were supplied.");
            }

            if (index < 0 || index > MethodParameters.Length - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _parameterDefaultValues[index];
        }

        private static MethodExecutor GetExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            // Parameters to executor
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            // Build parameter list
            var parameters = new List<Expression>();
            var paramInfos = methodInfo.GetParameters();
            for (var i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);

                // valueCast is "(Ti) parameters[i]"
                parameters.Add(valueCast);
            }

            // Call method
            var instanceCast = Expression.Convert(targetParameter, targetTypeInfo.AsType());
            var methodCall = Expression.Call(instanceCast, methodInfo, parameters);

            // methodCall is "((Ttarget) target) method((T0) parameters[0], (T1) parameters[1], ...)"
            // Create function
            if (methodCall.Type == typeof(void))
            {
                var lambda = Expression.Lambda<VoidMethodExecutor>(methodCall, targetParameter, parametersParameter);
                var voidExecutor = lambda.Compile();
                return WrapVoidMethod(voidExecutor);
            }
            else
            {
                // must coerce methodCall to match ActionExecutor signature
                var castMethodCall = Expression.Convert(methodCall, typeof(object));
                var lambda = Expression.Lambda<MethodExecutor>(castMethodCall, targetParameter, parametersParameter);
                return lambda.Compile();
            }
        }

        private static MethodExecutor WrapVoidMethod(VoidMethodExecutor executor)
        {
            return delegate (object target, object[] parameters)
            {
                executor(target, parameters);
                return null;
            };
        }

        private static MethodExecutorAsync GetExecutorAsync(
            MethodInfo methodInfo,
            TypeInfo targetTypeInfo,
            CoercedAwaitableInfo coercedAwaitableInfo)
        {
            // Parameters to executor
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            // Build parameter list
            var parameters = new List<Expression>();
            var paramInfos = methodInfo.GetParameters();
            for (var i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);

                // valueCast is "(Ti) parameters[i]"
                parameters.Add(valueCast);
            }

            // Call method
            var instanceCast = Expression.Convert(targetParameter, targetTypeInfo.AsType());
            var methodCall = Expression.Call(instanceCast, methodInfo, parameters);

            // Using the method return value, construct an ObjectMethodExecutorAwaitable based on
            // the info we have about its implementation of the awaitable pattern. Note that all
            // the funcs/actions we construct here are precompiled, so that only one instance of
            // each is preserved throughout the lifetime of the ObjectMethodExecutor.

            // var getAwaiterFunc = (object awaitable) =>
            //     (object)((CustomAwaitableType)awaitable).GetAwaiter();
            var customAwaitableParam = Expression.Parameter(typeof(object), "awaitable");
            var awaitableInfo = coercedAwaitableInfo.AwaitableInfo;
            var postCoercionMethodReturnType = coercedAwaitableInfo.CoercerResultType ?? methodInfo.ReturnType;
            var getAwaiterFunc = Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    Expression.Call(
                        Expression.Convert(customAwaitableParam, postCoercionMethodReturnType),
                        awaitableInfo.GetAwaiterMethod),
                    typeof(object)),
                customAwaitableParam).Compile();

            // var isCompletedFunc = (object awaiter) =>
            //     ((CustomAwaiterType)awaiter).IsCompleted;
            var isCompletedParam = Expression.Parameter(typeof(object), "awaiter");
            var isCompletedFunc = Expression.Lambda<Func<object, bool>>(
                Expression.MakeMemberAccess(
                    Expression.Convert(isCompletedParam, awaitableInfo.AwaiterType),
                    awaitableInfo.AwaiterIsCompletedProperty),
                isCompletedParam).Compile();

            var getResultParam = Expression.Parameter(typeof(object), "awaiter");
            Func<object, object> getResultFunc;
            if (awaitableInfo.ResultType == typeof(void))
            {
                getResultFunc = Expression.Lambda<Func<object, object>>(
                    Expression.Block(
                        Expression.Call(
                            Expression.Convert(getResultParam, awaitableInfo.AwaiterType),
                            awaitableInfo.AwaiterGetResultMethod),
                        Expression.Constant(null)
                    ),
                    getResultParam).Compile();
            }
            else
            {
                getResultFunc = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(
                        Expression.Call(
                            Expression.Convert(getResultParam, awaitableInfo.AwaiterType),
                            awaitableInfo.AwaiterGetResultMethod),
                        typeof(object)),
                    getResultParam).Compile();
            }

            // var onCompletedFunc = (object awaiter, Action continuation) => {
            //     ((CustomAwaiterType)awaiter).OnCompleted(continuation);
            // };
            var onCompletedParam1 = Expression.Parameter(typeof(object), "awaiter");
            var onCompletedParam2 = Expression.Parameter(typeof(Action), "continuation");
            var onCompletedFunc = Expression.Lambda<Action<object, Action>>(
                Expression.Call(
                    Expression.Convert(onCompletedParam1, awaitableInfo.AwaiterType),
                    awaitableInfo.AwaiterOnCompletedMethod,
                    onCompletedParam2),
                onCompletedParam1,
                onCompletedParam2).Compile();

            Action<object, Action> unsafeOnCompletedFunc = null;
            if (awaitableInfo.AwaiterUnsafeOnCompletedMethod != null)
            {
                // var unsafeOnCompletedFunc = (object awaiter, Action continuation) => {
                //     ((CustomAwaiterType)awaiter).UnsafeOnCompleted(continuation);
                // };
                var unsafeOnCompletedParam1 = Expression.Parameter(typeof(object), "awaiter");
                var unsafeOnCompletedParam2 = Expression.Parameter(typeof(Action), "continuation");
                unsafeOnCompletedFunc = Expression.Lambda<Action<object, Action>>(
                    Expression.Call(
                        Expression.Convert(unsafeOnCompletedParam1, awaitableInfo.AwaiterType),
                        awaitableInfo.AwaiterUnsafeOnCompletedMethod,
                        unsafeOnCompletedParam2),
                    unsafeOnCompletedParam1,
                    unsafeOnCompletedParam2).Compile();
            }

            // If we need to pass the method call result through a coercer function to get an
            // awaitable, then do so.
            var coercedMethodCall = coercedAwaitableInfo.RequiresCoercion
                ? Expression.Invoke(coercedAwaitableInfo.CoercerExpression, methodCall)
                : (Expression)methodCall;

            // return new ObjectMethodExecutorAwaitable(
            //     (object)coercedMethodCall,
            //     getAwaiterFunc,
            //     isCompletedFunc,
            //     getResultFunc,
            //     onCompletedFunc,
            //     unsafeOnCompletedFunc);
            var returnValueExpression = Expression.New(
                _objectMethodExecutorAwaitableConstructor,
                Expression.Convert(coercedMethodCall, typeof(object)),
                Expression.Constant(getAwaiterFunc),
                Expression.Constant(isCompletedFunc),
                Expression.Constant(getResultFunc),
                Expression.Constant(onCompletedFunc),
                Expression.Constant(unsafeOnCompletedFunc, typeof(Action<object, Action>)));

            var lambda =
                Expression.Lambda<MethodExecutorAsync>(returnValueExpression, targetParameter, parametersParameter);
            return lambda.Compile();
        }

        private delegate ObjectMethodExecutorAwaitable MethodExecutorAsync(object target, params object[] parameters);

        private delegate object MethodExecutor(object target, params object[] parameters);

        private delegate void VoidMethodExecutor(object target, object[] parameters);
    }

    /// <summary>
    /// Provides a common awaitable structure that <see cref="ObjectMethodExecutor.ExecuteAsync" /> can
    /// return, regardless of whether the underlying value is a System.Task, an FSharpAsync, or an
    /// application-defined custom awaitable.
    /// </summary>
    internal struct ObjectMethodExecutorAwaitable
    {
        private readonly object _customAwaitable;
        private readonly Func<object, object> _getAwaiterMethod;
        private readonly Func<object, bool> _isCompletedMethod;
        private readonly Func<object, object> _getResultMethod;
        private readonly Action<object, Action> _onCompletedMethod;
        private readonly Action<object, Action> _unsafeOnCompletedMethod;

        // Perf note: since we're requiring the customAwaitable to be supplied here as an object,
        // this will trigger a further allocation if it was a value type (i.e., to box it). We can't
        // fix this by making the customAwaitable type generic, because the calling code typically
        // does not know the type of the awaitable/awaiter at compile-time anyway.
        //
        // However, we could fix it by not passing the customAwaitable here at all, and instead
        // passing a func that maps directly from the target object (e.g., controller instance),
        // target method (e.g., action method info), and params array to the custom awaiter in the
        // GetAwaiter() method below. In effect, by delaying the actual method call until the
        // upstream code calls GetAwaiter on this ObjectMethodExecutorAwaitable instance.
        // This optimization is not currently implemented because:
        // [1] It would make no difference when the awaitable was an object type, which is
        //     by far the most common scenario (e.g., System.Task<T>).
        // [2] It would be complex - we'd need some kind of object pool to track all the parameter
        //     arrays until we needed to use them in GetAwaiter().
        // We can reconsider this in the future if there's a need to optimize for ValueTask<T>
        // or other value-typed awaitables.

        public ObjectMethodExecutorAwaitable(
            object customAwaitable,
            Func<object, object> getAwaiterMethod,
            Func<object, bool> isCompletedMethod,
            Func<object, object> getResultMethod,
            Action<object, Action> onCompletedMethod,
            Action<object, Action> unsafeOnCompletedMethod)
        {
            _customAwaitable = customAwaitable;
            _getAwaiterMethod = getAwaiterMethod;
            _isCompletedMethod = isCompletedMethod;
            _getResultMethod = getResultMethod;
            _onCompletedMethod = onCompletedMethod;
            _unsafeOnCompletedMethod = unsafeOnCompletedMethod;
        }

        public Awaiter GetAwaiter()
        {
            var customAwaiter = _getAwaiterMethod(_customAwaitable);
            return new Awaiter(customAwaiter, _isCompletedMethod, _getResultMethod, _onCompletedMethod,
                _unsafeOnCompletedMethod);
        }

        public struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly object _customAwaiter;
            private readonly Func<object, bool> _isCompletedMethod;
            private readonly Func<object, object> _getResultMethod;
            private readonly Action<object, Action> _onCompletedMethod;
            private readonly Action<object, Action> _unsafeOnCompletedMethod;

            public Awaiter(
                object customAwaiter,
                Func<object, bool> isCompletedMethod,
                Func<object, object> getResultMethod,
                Action<object, Action> onCompletedMethod,
                Action<object, Action> unsafeOnCompletedMethod)
            {
                _customAwaiter = customAwaiter;
                _isCompletedMethod = isCompletedMethod;
                _getResultMethod = getResultMethod;
                _onCompletedMethod = onCompletedMethod;
                _unsafeOnCompletedMethod = unsafeOnCompletedMethod;
            }

            public bool IsCompleted => _isCompletedMethod(_customAwaiter);

            public object GetResult()
            {
                return _getResultMethod(_customAwaiter);
            }

            public void OnCompleted(Action continuation)
            {
                _onCompletedMethod(_customAwaiter, continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                // If the underlying awaitable implements ICriticalNotifyCompletion, use its UnsafeOnCompleted.
                // If not, fall back on using its OnCompleted.
                //
                // Why this is safe:
                // - Implementing ICriticalNotifyCompletion is a way of saying the caller can choose whether it
                //   needs the execution context to be preserved (which it signals by calling OnCompleted), or
                //   that it doesn't (which it signals by calling UnsafeOnCompleted). Obviously it's faster *not*
                //   to preserve and restore the context, so we prefer that where possible.
                // - If a caller doesn't need the execution context to be preserved and hence calls UnsafeOnCompleted,
                //   there's no harm in preserving it anyway - it's just a bit of wasted cost. That's what will happen
                //   if a caller sees that the proxy implements ICriticalNotifyCompletion but the proxy chooses to
                //   pass the call on to the underlying awaitable's OnCompleted method.

                var underlyingMethodToUse = _unsafeOnCompletedMethod ?? _onCompletedMethod;
                underlyingMethodToUse(_customAwaiter, continuation);
            }
        }
    }

    internal struct CoercedAwaitableInfo
    {
        public AwaitableInfo AwaitableInfo { get; }
        public Expression CoercerExpression { get; }
        public Type CoercerResultType { get; }
        public bool RequiresCoercion => CoercerExpression != null;

        public CoercedAwaitableInfo(AwaitableInfo awaitableInfo)
        {
            AwaitableInfo = awaitableInfo;
            CoercerExpression = null;
            CoercerResultType = null;
        }

        public CoercedAwaitableInfo(Expression coercerExpression, Type coercerResultType,
            AwaitableInfo coercedAwaitableInfo)
        {
            CoercerExpression = coercerExpression;
            CoercerResultType = coercerResultType;
            AwaitableInfo = coercedAwaitableInfo;
        }

        public static bool IsTypeAwaitable(Type type, out CoercedAwaitableInfo info)
        {
            if (AwaitableInfo.IsTypeAwaitable(type, out var directlyAwaitableInfo))
            {
                info = new CoercedAwaitableInfo(directlyAwaitableInfo);
                return true;
            }

            // It's not directly awaitable, but maybe we can coerce it.
            // Currently we support coercing FSharpAsync<T>.
            if (ObjectMethodExecutorFSharpSupport.TryBuildCoercerFromFSharpAsyncToAwaitable(type,
                out var coercerExpression,
                out var coercerResultType))
            {
                if (AwaitableInfo.IsTypeAwaitable(coercerResultType, out var coercedAwaitableInfo))
                {
                    info = new CoercedAwaitableInfo(coercerExpression, coercerResultType, coercedAwaitableInfo);
                    return true;
                }
            }

            info = default(CoercedAwaitableInfo);
            return false;
        }
    }

    internal struct AwaitableInfo
    {
        public Type AwaiterType { get; }
        public PropertyInfo AwaiterIsCompletedProperty { get; }
        public MethodInfo AwaiterGetResultMethod { get; }
        public MethodInfo AwaiterOnCompletedMethod { get; }
        public MethodInfo AwaiterUnsafeOnCompletedMethod { get; }
        public Type ResultType { get; }
        public MethodInfo GetAwaiterMethod { get; }

        public AwaitableInfo(
            Type awaiterType,
            PropertyInfo awaiterIsCompletedProperty,
            MethodInfo awaiterGetResultMethod,
            MethodInfo awaiterOnCompletedMethod,
            MethodInfo awaiterUnsafeOnCompletedMethod,
            Type resultType,
            MethodInfo getAwaiterMethod)
        {
            AwaiterType = awaiterType;
            AwaiterIsCompletedProperty = awaiterIsCompletedProperty;
            AwaiterGetResultMethod = awaiterGetResultMethod;
            AwaiterOnCompletedMethod = awaiterOnCompletedMethod;
            AwaiterUnsafeOnCompletedMethod = awaiterUnsafeOnCompletedMethod;
            ResultType = resultType;
            GetAwaiterMethod = getAwaiterMethod;
        }

        public static bool IsTypeAwaitable(Type type, out AwaitableInfo awaitableInfo)
        {
            // Based on Roslyn code: http://source.roslyn.io/#Microsoft.CodeAnalysis.Workspaces/Shared/Extensions/ISymbolExtensions.cs,db4d48ba694b9347

            // Awaitable must have method matching "object GetAwaiter()"
            var getAwaiterMethod = type.GetRuntimeMethods().FirstOrDefault(m =>
                m.Name.Equals("GetAwaiter", StringComparison.OrdinalIgnoreCase)
                && m.GetParameters().Length == 0
                && m.ReturnType != null);
            if (getAwaiterMethod == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            var awaiterType = getAwaiterMethod.ReturnType;

            // Awaiter must have property matching "bool IsCompleted { get; }"
            var isCompletedProperty = awaiterType.GetRuntimeProperties().FirstOrDefault(p =>
                p.Name.Equals("IsCompleted", StringComparison.OrdinalIgnoreCase)
                && p.PropertyType == typeof(bool)
                && p.GetMethod != null);
            if (isCompletedProperty == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            // Awaiter must implement INotifyCompletion
            var awaiterInterfaces = awaiterType.GetInterfaces();
            var implementsINotifyCompletion = awaiterInterfaces.Any(t => t == typeof(INotifyCompletion));
            if (!implementsINotifyCompletion)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            // INotifyCompletion supplies a method matching "void OnCompleted(Action action)"
            var iNotifyCompletionMap = awaiterType
                .GetTypeInfo()
                .GetRuntimeInterfaceMap(typeof(INotifyCompletion));
            var onCompletedMethod = iNotifyCompletionMap.InterfaceMethods.Single(m =>
                m.Name.Equals("OnCompleted", StringComparison.OrdinalIgnoreCase)
                && m.ReturnType == typeof(void)
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(Action));

            // Awaiter optionally implements ICriticalNotifyCompletion
            var implementsICriticalNotifyCompletion =
                awaiterInterfaces.Any(t => t == typeof(ICriticalNotifyCompletion));
            MethodInfo unsafeOnCompletedMethod;
            if (implementsICriticalNotifyCompletion)
            {
                // ICriticalNotifyCompletion supplies a method matching "void UnsafeOnCompleted(Action action)"
                var iCriticalNotifyCompletionMap = awaiterType
                    .GetTypeInfo()
                    .GetRuntimeInterfaceMap(typeof(ICriticalNotifyCompletion));
                unsafeOnCompletedMethod = iCriticalNotifyCompletionMap.InterfaceMethods.Single(m =>
                    m.Name.Equals("UnsafeOnCompleted", StringComparison.OrdinalIgnoreCase)
                    && m.ReturnType == typeof(void)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(Action));
            }
            else
            {
                unsafeOnCompletedMethod = null;
            }

            // Awaiter must have method matching "void GetResult" or "T GetResult()"
            var getResultMethod = awaiterType.GetRuntimeMethods().FirstOrDefault(m =>
                m.Name.Equals("GetResult")
                && m.GetParameters().Length == 0);
            if (getResultMethod == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            awaitableInfo = new AwaitableInfo(
                awaiterType,
                isCompletedProperty,
                getResultMethod,
                onCompletedMethod,
                unsafeOnCompletedMethod,
                getResultMethod.ReturnType,
                getAwaiterMethod);
            return true;
        }
    }

    /// <summary>
    /// Helper for detecting whether a given type is FSharpAsync`1, and if so, supplying
    /// an <see cref="Expression" /> for mapping instances of that type to a C# awaitable.
    /// </summary>
    /// <remarks>
    /// The main design goal here is to avoid taking a compile-time dependency on
    /// FSharp.Core.dll, because non-F# applications wouldn't use it. So all the references
    /// to FSharp types have to be constructed dynamically at runtime.
    /// </remarks>
    internal static class ObjectMethodExecutorFSharpSupport
    {
        private static readonly object _fsharpValuesCacheLock = new object();
        private static Assembly _fsharpCoreAssembly;
        private static MethodInfo _fsharpAsyncStartAsTaskGenericMethod;
        private static PropertyInfo _fsharpOptionOfTaskCreationOptionsNoneProperty;
        private static PropertyInfo _fsharpOptionOfCancellationTokenNoneProperty;

        public static bool TryBuildCoercerFromFSharpAsyncToAwaitable(
            Type possibleFSharpAsyncType,
            out Expression coerceToAwaitableExpression,
            out Type awaitableType)
        {
            var methodReturnGenericType = possibleFSharpAsyncType.IsGenericType
                ? possibleFSharpAsyncType.GetGenericTypeDefinition()
                : null;

            if (!IsFSharpAsyncOpenGenericType(methodReturnGenericType))
            {
                coerceToAwaitableExpression = null;
                awaitableType = null;
                return false;
            }

            var awaiterResultType = possibleFSharpAsyncType.GetGenericArguments().Single();
            awaitableType = typeof(Task<>).MakeGenericType(awaiterResultType);

            // coerceToAwaitableExpression = (object fsharpAsync) =>
            // {
            //     return (object)FSharpAsync.StartAsTask<TResult>(
            //         (Microsoft.FSharp.Control.FSharpAsync<TResult>)fsharpAsync,
            //         FSharpOption<TaskCreationOptions>.None,
            //         FSharpOption<CancellationToken>.None);
            // };
            var startAsTaskClosedMethod = _fsharpAsyncStartAsTaskGenericMethod
                .MakeGenericMethod(awaiterResultType);
            var coerceToAwaitableParam = Expression.Parameter(typeof(object));
            coerceToAwaitableExpression = Expression.Lambda(
                Expression.Convert(
                    Expression.Call(
                        startAsTaskClosedMethod,
                        Expression.Convert(coerceToAwaitableParam, possibleFSharpAsyncType),
                        Expression.MakeMemberAccess(null, _fsharpOptionOfTaskCreationOptionsNoneProperty),
                        Expression.MakeMemberAccess(null, _fsharpOptionOfCancellationTokenNoneProperty)),
                    typeof(object)),
                coerceToAwaitableParam);

            return true;
        }

        private static bool IsFSharpAsyncOpenGenericType(Type possibleFSharpAsyncGenericType)
        {
            var typeFullName = possibleFSharpAsyncGenericType?.FullName;
            if (!string.Equals(typeFullName, "Microsoft.FSharp.Control.FSharpAsync`1", StringComparison.Ordinal))
            {
                return false;
            }

            lock (_fsharpValuesCacheLock)
            {
                if (_fsharpCoreAssembly != null)
                {
                    return possibleFSharpAsyncGenericType?.Assembly == _fsharpCoreAssembly;
                }

                return TryPopulateFSharpValueCaches(possibleFSharpAsyncGenericType);
            }
        }

        private static bool TryPopulateFSharpValueCaches(Type possibleFSharpAsyncGenericType)
        {
            var assembly = possibleFSharpAsyncGenericType.Assembly;
            var fsharpOptionType = assembly.GetType("Microsoft.FSharp.Core.FSharpOption`1");
            var fsharpAsyncType = assembly.GetType("Microsoft.FSharp.Control.FSharpAsync");

            if (fsharpOptionType == null || fsharpAsyncType == null)
            {
                return false;
            }

            // Get a reference to FSharpOption<TaskCreationOptions>.None
            var fsharpOptionOfTaskCreationOptionsType = fsharpOptionType
                .MakeGenericType(typeof(TaskCreationOptions));
            _fsharpOptionOfTaskCreationOptionsNoneProperty = fsharpOptionOfTaskCreationOptionsType
                .GetTypeInfo()
                .GetRuntimeProperty("None");

            // Get a reference to FSharpOption<CancellationToken>.None
            var fsharpOptionOfCancellationTokenType = fsharpOptionType
                .MakeGenericType(typeof(CancellationToken));
            _fsharpOptionOfCancellationTokenNoneProperty = fsharpOptionOfCancellationTokenType
                .GetTypeInfo()
                .GetRuntimeProperty("None");

            // Get a reference to FSharpAsync.StartAsTask<>
            var fsharpAsyncMethods = fsharpAsyncType
                .GetRuntimeMethods()
                .Where(m => m.Name.Equals("StartAsTask", StringComparison.Ordinal));
            foreach (var candidateMethodInfo in fsharpAsyncMethods)
            {
                var parameters = candidateMethodInfo.GetParameters();
                if (parameters.Length == 3
                    && TypesHaveSameIdentity(parameters[0].ParameterType, possibleFSharpAsyncGenericType)
                    && parameters[1].ParameterType == fsharpOptionOfTaskCreationOptionsType
                    && parameters[2].ParameterType == fsharpOptionOfCancellationTokenType)
                {
                    // This really does look like the correct method (and hence assembly).
                    _fsharpAsyncStartAsTaskGenericMethod = candidateMethodInfo;
                    _fsharpCoreAssembly = assembly;
                    break;
                }
            }

            return _fsharpCoreAssembly != null;
        }

        private static bool TypesHaveSameIdentity(Type type1, Type type2)
        {
            return type1.Assembly == type2.Assembly
                   && string.Equals(type1.Namespace, type2.Namespace, StringComparison.Ordinal)
                   && string.Equals(type1.Name, type2.Name, StringComparison.Ordinal);
        }
    }
}