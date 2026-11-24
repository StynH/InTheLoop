namespace InTheLoop.Core.Messaging;

/// <summary>A request that is answered by exactly one handler.</summary>
public interface IRequest<TResult>;

public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Routes a request to its handler. View models and MCP tools only ever talk to this.</summary>
public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}

/// <summary>Thrown by handlers for problems the caller can fix (bad input, unknown id).</summary>
public sealed class RequestException(string message) : Exception(message);
