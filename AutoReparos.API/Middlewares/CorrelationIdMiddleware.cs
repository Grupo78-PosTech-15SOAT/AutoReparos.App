using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoReparos.API.Middlewares
{
    /// <summary>
    /// Middleware responsável por interceptar ou gerar um Correlation ID para cada requisição HTTP,
    /// garantindo rastreabilidade distribuída de ponta a ponta sem interferir no Activity.Current
    /// (TraceId/SpanId) nativo do OpenTelemetry.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        public const string CorrelationIdHeaderName = "X-Correlation-Id";
        public const string CorrelationIdItemKey = "CorrelationId";
        private const int MaxCorrelationIdLength = 128;

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var rawCorrelationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
            var correlationId = SanitizeOrGenerateCorrelationId(rawCorrelationId);

            // Armazena no HttpContext.Items para acesso síncrono durante o ciclo de vida do request
            context.Items[CorrelationIdItemKey] = correlationId;

            // Define o cabeçalho imediatamente na resposta HTTP
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;

            // Registra garantia adicional caso downstream altere os cabeçalhos antes de iniciar envio
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
                {
                    context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                }
                return Task.CompletedTask;
            });

            // Para requisições OPTIONS (preflight CORS), propaga o cabeçalho e segue o fluxo
            // sem necessidade de alocar escopo de log estruturado desnecessário
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Enriquece o escopo de logs estruturados (capturado pelo ILogger com IncludeScopes = true)
            // sem sobrescrever ou desconfigurar os IDs de tracing do OpenTelemetry
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                [CorrelationIdItemKey] = correlationId
            }))
            {
                await _next(context);
            }
        }

        private static string SanitizeOrGenerateCorrelationId(string? rawCorrelationId)
        {
            if (string.IsNullOrWhiteSpace(rawCorrelationId))
            {
                return Guid.NewGuid().ToString("D");
            }

            var trimmed = rawCorrelationId.Trim();

            if (trimmed.Length > MaxCorrelationIdLength || !IsValidCorrelationId(trimmed))
            {
                return Guid.NewGuid().ToString("D");
            }

            return trimmed;
        }

        private static bool IsValidCorrelationId(string correlationId)
        {
            foreach (var c in correlationId)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != ':')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
