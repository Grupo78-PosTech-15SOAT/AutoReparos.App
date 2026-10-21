using AutoReparos.Application.OrdensServicos.Services.Interfaces;
using AutoReparos.Application.Servicos.DTOs.Response;
using AutoReparos.Application.Shared.Interfaces;
using AutoReparos.Application.Shared.Metrics;
using AutoReparos.Infra.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AutoReparos.Application.Tests.OrdensServicos
{
    public class NotificacaoServiceTests
    {
        private readonly ILogger<NotificacaoService> _logger = new NullLogger<NotificacaoService>();
        private readonly IConfiguration _configuration;
        private readonly IAppUrlProvider _appUrlProvider = Substitute.For<IAppUrlProvider>();
        private readonly ISendGridClient _sendGridClient = Substitute.For<ISendGridClient>();
        private readonly List<TimeSpan> _recordedDelays = [];

        public NotificacaoServiceTests()
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["SendGrid:ApiKey"] = "SG.test-api-key-12345",
                ["SendGrid:FromEmail"] = "noreply@autoreparos.com",
                ["SendGrid:FromName"] = "AutoReparos"
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryConfig)
                .Build();

            _appUrlProvider.GetBaseUrl().Returns("https://app.autoreparos.com");
        }

        private NotificacaoService CreateService(Func<TimeSpan, Task>? delayFunc = null)
        {
            return new NotificacaoService(
                _logger,
                _configuration,
                _appUrlProvider,
                _sendGridClient,
                delayFunc ?? (delay =>
                {
                    _recordedDelays.Add(delay);
                    return Task.CompletedTask;
                }));
        }

        private static Response CreateSendGridResponse(HttpStatusCode statusCode, HttpResponseHeaders? headers = null)
        {
            var content = new StringContent(statusCode.ToString());
            return new Response(statusCode, content, headers);
        }

        // =====================================================================
        // Cenário 1: Sucesso na primeira tentativa -> uma chamada, sem retry
        // =====================================================================
        [Fact(DisplayName = "1. Sucesso na primeira tentativa deve chamar SendGrid uma única vez sem retries")]
        public async Task EnviarOrcamento_ComSucessoNaPrimeiraTentativa_DeveChamarSendGridUmaVezENaoRetentar()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateSendGridResponse(HttpStatusCode.OK)));

            var sut = CreateService();
            var servicos = new List<ServicoDto>
            {
                new(Guid.NewGuid(), "Troca de Óleo", "Troca completa do óleo do motor", 250m, DateTime.UtcNow, null)
            };

            // Act
            await sut.EnviarOrcamento("cliente@teste.com", "Cliente Teste", "token-123", 250m, servicos);

            // Assert
            await _sendGridClient.Received(1).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().BeEmpty();
        }

        // =====================================================================
        // Cenário 2: HTTP 500 -> retry até sucesso
        // =====================================================================
        [Fact(DisplayName = "2. HTTP 500 deve acionar retry e ter sucesso na segunda tentativa")]
        public async Task EnviarComRetryAsync_QuandoRetornarHttp500_DeveRealizarRetryAteSucesso()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.InternalServerError)),
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.Accepted)));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(2).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().HaveCount(1);
        }

        // =====================================================================
        // Cenário 3: HTTP 502 / 503 / 504 -> retries transitórios
        // =====================================================================
        [Theory(DisplayName = "3. HTTP 502, 503 e 504 devem ser tratados como erros transitórios e retentados")]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task EnviarComRetryAsync_QuandoRetornar502_503_504_DeveRetentar(HttpStatusCode statusCode)
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(CreateSendGridResponse(statusCode)),
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.OK)));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "atualizacao_status");

            // Assert
            await _sendGridClient.Received(2).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().HaveCount(1);
        }

        // =====================================================================
        // Cenário 4: HTTP 429 -> retry com rate limit
        // =====================================================================
        [Fact(DisplayName = "4. HTTP 429 Too Many Requests deve acionar retry seguro")]
        public async Task EnviarComRetryAsync_QuandoRetornar429_DeveRealizarRetry()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.TooManyRequests)),
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.OK)));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(2).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().HaveCount(1);
        }

        // =====================================================================
        // Cenário 5: Respeito estrito ao cabeçalho Retry-After
        // =====================================================================
        [Fact(DisplayName = "5. Respeito ao cabeçalho Retry-After (Delta) em caso de HTTP 429")]
        public void CalcularDelay_QuandoRetornar429ComHeaderRetryAfterDelta_DeveRespeitarValorDoHeader()
        {
            // Arrange
            using var httpMsg = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            httpMsg.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(4));
            var response = CreateSendGridResponse(HttpStatusCode.TooManyRequests, httpMsg.Headers);

            // Act
            var delay = NotificacaoService.CalcularDelay(response, tentativa: 1, TimeSpan.FromSeconds(1));

            // Assert
            delay.Should().Be(TimeSpan.FromSeconds(4));
        }

        [Fact(DisplayName = "5b. Respeito ao cabeçalho Retry-After (Date) com teto máximo de 10 segundos")]
        public void CalcularDelay_QuandoRetornar429ComHeaderRetryAfterDateExcessivo_DeveLimitarA10Segundos()
        {
            // Arrange
            using var httpMsg = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            httpMsg.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(30));
            var response = CreateSendGridResponse(HttpStatusCode.TooManyRequests, httpMsg.Headers);

            // Act
            var delay = NotificacaoService.CalcularDelay(response, tentativa: 1, TimeSpan.FromSeconds(1));

            // Assert
            delay.Should().Be(TimeSpan.FromSeconds(10));
        }

        // =====================================================================
        // Cenário 6: Erros 4xx não transitórios (400, 401, 403, 404) -> sem retry
        // =====================================================================
        [Theory(DisplayName = "6. Erros de cliente HTTP 4xx (não transitórios) abortam imediatamente sem retry")]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task EnviarComRetryAsync_QuandoRetornar4xxNaoTransitorio_NaoDeveRetentar(HttpStatusCode statusCode)
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateSendGridResponse(statusCode)));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(1).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().BeEmpty();
        }

        // =====================================================================
        // Cenário 7: Falha em todas as tentativas -> incrementa métrica de falha
        // =====================================================================
        [Fact(DisplayName = "7. Falha persistente após 3 tentativas esgota retries e emite métrica NotificacoesFalhas")]
        public async Task EnviarComRetryAsync_QuandoFalharEmTodasAsTentativas_DeveEsgotarRetriesERegistrarMetricaFalha()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateSendGridResponse(HttpStatusCode.InternalServerError)));

            long totalFalhas = 0;
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AutoReparosMetrics.MeterName && instrument.Name == "notificacoes.falhas")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                Interlocked.Add(ref totalFalhas, measurement);
            });
            listener.Start();

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(3).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().HaveCount(2);
            totalFalhas.Should().Be(1);
        }

        // =====================================================================
        // Cenário 8: Sucesso após retry -> não incrementa métrica final de falha
        // =====================================================================
        [Fact(DisplayName = "8. Sucesso recuperado via retry não deve incrementar a métrica final de falha")]
        public async Task EnviarComRetryAsync_QuandoObterSucessoAposRetry_NaoDeveIncrementarMetricaFinalDeFalha()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.InternalServerError)),
                    Task.FromResult(CreateSendGridResponse(HttpStatusCode.OK)));

            long totalFalhas = 0;
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AutoReparosMetrics.MeterName && instrument.Name == "notificacoes.falhas")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                Interlocked.Add(ref totalFalhas, measurement);
            });
            listener.Start();

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(2).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            totalFalhas.Should().Be(0);
        }

        // =====================================================================
        // Cenário 9: Exceção transitória (HttpRequestException/Timeout) com retry
        // Validação de risco de duplicidade de e-mail documentada no método
        // =====================================================================
        [Fact(DisplayName = "9. Exceção transitória de transporte de rede aciona retry sem delay real")]
        public async Task EnviarComRetryAsync_QuandoLancarExcecaoTransitoria_DeveRetentarEAvisarRiscoDuplicidade()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => Task.FromException<Response>(new HttpRequestException("Conexão interrompida pelo host remoto")),
                    _ => Task.FromResult(CreateSendGridResponse(HttpStatusCode.OK)));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(2).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().HaveCount(1);
        }

        [Fact(DisplayName = "9b. Exceção não transitória aborta imediatamente sem retry")]
        public async Task EnviarComRetryAsync_QuandoLancarExcecaoNaoTransitoria_DeveAbortarSemRetentar()
        {
            // Arrange
            _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
                .Returns<Task<Response>>(_ => throw new InvalidOperationException("Erro de configuração interna"));

            var sut = CreateService();
            var msg = new SendGridMessage();

            // Act
            await sut.EnviarComRetryAsync(_sendGridClient, msg, "cliente@teste.com", "orcamento");

            // Assert
            await _sendGridClient.Received(1).SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>());
            _recordedDelays.Should().BeEmpty();
        }

        // =====================================================================
        // Cenário 10: Comportamento do Backoff Exponencial com Jitter
        // =====================================================================
        [Fact(DisplayName = "10. Cálculo de delay com jitter deve seguir fórmula de backoff exponencial")]
        public void CalcularDelayComJitter_DeveSeguirBackoffExponencialComJitter()
        {
            // Tentativa 1: 1s * 2^0 + jitter (100 a 400ms) => [1100ms, 1400ms]
            var delay1 = NotificacaoService.CalcularDelayComJitter(1, TimeSpan.FromSeconds(1));
            delay1.TotalMilliseconds.Should().BeInRange(1100, 1400);

            // Tentativa 2: 1s * 2^1 + jitter (100 a 400ms) => [2100ms, 2400ms]
            var delay2 = NotificacaoService.CalcularDelayComJitter(2, TimeSpan.FromSeconds(1));
            delay2.TotalMilliseconds.Should().BeInRange(2100, 2400);

            // Tentativa 3: 1s * 2^2 + jitter (100 a 400ms) => [4100ms, 4400ms]
            var delay3 = NotificacaoService.CalcularDelayComJitter(3, TimeSpan.FromSeconds(1));
            delay3.TotalMilliseconds.Should().BeInRange(4100, 4400);
        }
    }
}
