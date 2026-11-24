using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace InTheLoop.Core.Messaging;

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (IInvoker<TResult>)Invokers.GetOrAdd(request.GetType(), static (type, result) =>
            Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(type, result))!, typeof(TResult));

        return invoker.InvokeAsync(services, request, cancellationToken);
    }

    private interface IInvoker<TResult>
    {
        Task<TResult> InvokeAsync(IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken);
    }

    private sealed class Invoker<TRequest, TResult> : IInvoker<TResult> where TRequest : IRequest<TResult>
    {
        public Task<TResult> InvokeAsync(IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken) =>
            services.GetRequiredService<IRequestHandler<TRequest, TResult>>().HandleAsync((TRequest)request, cancellationToken);
    }
}
