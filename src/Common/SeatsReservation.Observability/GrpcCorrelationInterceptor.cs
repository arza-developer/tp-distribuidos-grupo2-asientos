using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog.Context;

namespace SeatsReservation.Observability;

public class GrpcCorrelationInterceptor : Interceptor
{
    private const string CorrelationIdKey = "x-correlation-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        string correlationId = Guid.NewGuid().ToString();

        // Corrección: Usar RequestHeaders en lugar de EntryMetadata
        var headers = context.RequestHeaders;
        var entry = headers.FirstOrDefault(h => h.Key.Equals(CorrelationIdKey, StringComparison.OrdinalIgnoreCase));

        if (entry != null && !string.IsNullOrWhiteSpace(entry.Value))
        {
            correlationId = entry.Value;
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            return await continuation(request, context);
        }
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();

        // Corrección: Obtener el CorrelationId actual mediante OpenTelemetry Activity o generar uno nuevo
        var currentActivity = System.Diagnostics.Activity.Current;
        var correlationId = currentActivity?.TraceId.ToString() ?? Guid.NewGuid().ToString();

        if (!headers.Any(h => h.Key.Equals(CorrelationIdKey, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(CorrelationIdKey, correlationId);
        }

        var options = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

        return base.AsyncUnaryCall(request, newContext, continuation);
    }
}