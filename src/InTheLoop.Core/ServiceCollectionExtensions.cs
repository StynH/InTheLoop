using System.Reflection;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Services;
using InTheLoop.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InTheLoop.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInTheLoopCore(this IServiceCollection services, Action<StoreOptions>? configure = null)
    {
        var options = new StoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IReviewStore, FileReviewStore>();
        services.AddSingleton<IProjectStore, FileProjectStore>();
        services.AddSingleton<IAppPresence, AppPresence>();
        services.AddSingleton<IRequestDispatcher, RequestDispatcher>();
        services.AddRequestHandlers(typeof(ServiceCollectionExtensions).Assembly);
        return services;
    }

    /// <summary>Registers every <see cref="IRequestHandler{TRequest,TResult}"/> found in the assembly.</summary>
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            {
                services.AddTransient(contract, type);
            }
        }

        return services;
    }
}
