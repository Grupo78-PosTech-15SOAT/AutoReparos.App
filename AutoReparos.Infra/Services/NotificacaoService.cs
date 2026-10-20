using AutoReparos.Application.OrdensServicos.Services.Interfaces;
using AutoReparos.Application.Servicos.DTOs.Response;
using AutoReparos.Application.Shared.Interfaces;
using AutoReparos.Application.Shared.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AutoReparos.Infra.Services
{
    public class NotificacaoService : INotificacaoService
    {
        private readonly ILogger<NotificacaoService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAppUrlProvider _appUrlProvider;

        public NotificacaoService(ILogger<NotificacaoService> logger, IConfiguration configuration, IAppUrlProvider appUrlProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _appUrlProvider = appUrlProvider;
        }

        public async Task EnviarOrcamento(string emailDestinatario, string nomeDestinatario, string token, decimal valorTotal, IEnumerable<ServicoDto> servicos)
        {
            var baseUrl = _appUrlProvider.GetBaseUrl();
            var aprovarUrl = $"{baseUrl}/api/ordem-servico/aprovar?token={Uri.EscapeDataString(token)}";
            var recusarUrl = $"{baseUrl}/api/ordem-servico/recusar?token={Uri.EscapeDataString(token)}";

            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"];
            var fromName = _configuration["SendGrid:FromName"];

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Configurações do SendGrid incompletas ou ausentes. Envio de e-mail de orçamento ignorado.");
                RegistrarFalhaMetrica("orcamento", motivo: "SendGridNaoConfigurado");
                return;
            }

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(emailDestinatario, nomeDestinatario);
            var subject = "Orçamento da Ordem de Serviço";
            var plainTextContent = $"Olá, {nomeDestinatario}! O valor do orçamento da ordem de serviço ficou em {valorTotal.ToString("C2", new CultureInfo("pt-BR"))}.";

            var linhasServicos = string.Join("", servicos.Select(s => $@"
            <tr>
                <td style='border: 1px solid #e2e8f0; padding: 12px; color: #334155; font-size: 14px;'>{s.Nome}</td>
                <td style='border: 1px solid #e2e8f0; padding: 12px; color: #334155; font-size: 14px;'>{s.Descricao}</td>
                <td style='border: 1px solid #e2e8f0; padding: 12px; color: #334155; font-size: 14px; text-align: right;'>{s.ValorTabelado?.ToString("C2", new CultureInfo("pt-BR")) ?? "R$ 0,00"}</td>
            </tr>"));

            var htmlContent = $@"
<div style='font-family: ""Outfit"", ""Inter"", Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
    <div style='text-align: center; margin-bottom: 24px; padding-bottom: 20px; border-bottom: 2px solid #f1f5f9;'>
        <h2 style='color: #1e293b; margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.025em;'>AutoReparos</h2>
        <p style='color: #64748b; margin: 4px 0 0 0; font-size: 14px;'>Orçamento da Ordem de Serviço</p>
    </div>

    <div style='margin-bottom: 24px;'>
        <p style='color: #334155; font-size: 16px; line-height: 1.6; margin: 0;'>Olá, <strong>{nomeDestinatario}</strong>,</p>
        <p style='color: #334155; font-size: 16px; line-height: 1.6; margin: 8px 0 0 0;'>
            O diagnóstico do seu veículo foi concluído e o orçamento ficou em <strong style='font-size: 18px; color: #2563eb;'>{valorTotal.ToString("C2", new CultureInfo("pt-BR"))}</strong>.
        </p>
    </div>

    <div style='margin-bottom: 24px;'>
        <h3 style='color: #1e293b; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;'>Serviços diagnosticados:</h3>
        <table style='border-collapse: collapse; width: 100%; border: 1px solid #e2e8f0;'>
            <thead>
                <tr style='background-color: #f8fafc;'>
                    <th style='border: 1px solid #e2e8f0; padding: 12px; text-align: left; color: #475569; font-weight: 600; font-size: 14px;'>Nome</th>
                    <th style='border: 1px solid #e2e8f0; padding: 12px; text-align: left; color: #475569; font-weight: 600; font-size: 14px;'>Descrição</th>
                    <th style='border: 1px solid #e2e8f0; padding: 12px; text-align: right; color: #475569; font-weight: 600; font-size: 14px;'>Valor</th>
                </tr>
            </thead>
            <tbody>
                {linhasServicos}
            </tbody>
        </table>
    </div>

    <div style='background-color: #f8fafc; border-radius: 8px; padding: 20px; margin-bottom: 24px; border: 1px solid #f1f5f9; text-align: center;'>
        <p style='color: #334155; font-size: 16px; font-weight: 500; margin: 0 0 16px 0;'>Deseja aprovar o orçamento?</p>
        <div style='text-align: center;'>
            <a href='{aprovarUrl}' style='background-color:#22c55e;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;display:inline-block;margin-right:12px;font-weight:600;font-size:15px;box-shadow: 0 2px 4px rgba(34, 197, 94, 0.2);'>Aprovar Orçamento</a>
            <a href='{recusarUrl}' style='background-color:#ef4444;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;display:inline-block;font-weight:600;font-size:15px;box-shadow: 0 2px 4px rgba(239, 68, 68, 0.2);'>Recusar Orçamento</a>
        </div>
    </div>

    <div style='margin-top: 32px; padding-top: 20px; border-top: 1px solid #e2e8f0; text-align: center; color: #94a3b8; font-size: 12px;'>
        <p style='margin: 0;'>Este é um e-mail automático enviado pelo sistema AutoReparos.</p>
        <p style='margin: 4px 0 0 0;'>FIAP - Pós-Graduação em Arquitetura de Software</p>
    </div>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            await EnviarComRetryAsync(client, msg, emailDestinatario, "orcamento");
        }

        public async Task EnviarAtualizacaoStatus(string emailDestinatario, string nomeDestinatario, Guid ordemServicoId, string statusAnterior, string novoStatus)
        {
            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"];
            var fromName = _configuration["SendGrid:FromName"];

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Configurações do SendGrid incompletas ou ausentes. Envio de atualização de status ignorado.");
                RegistrarFalhaMetrica("atualizacao_status", motivo: "SendGridNaoConfigurado");
                return;
            }

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(emailDestinatario, nomeDestinatario);
            var subject = $"Atualização de Status da Ordem de Serviço #{ordemServicoId.ToString().Substring(0, 8).ToUpper()}";

            var labelAnterior = TraduzirStatus(statusAnterior);
            var labelNovo = TraduzirStatus(novoStatus);

            var plainTextContent = $"Olá, {nomeDestinatario}! O status da sua ordem de serviço foi atualizado de '{labelAnterior}' para '{labelNovo}'.";

            string mensagemAdicional = "";
            if (statusAnterior == "AguardandoAprovacao" && novoStatus == "EmDiagnostico")
            {
                mensagemAdicional = "<p style='color: #ef4444; font-weight: 500; font-size: 15px; margin: 16px 0 0 0; text-align: center;'>Identificamos que o orçamento foi recusado. Nossa equipe já foi notificada e está revisando os itens do diagnóstico para encontrar a melhor solução para você. Entraremos em contato em breve!</p>";
            }
            else if (novoStatus == "EmExecucao")
            {
                mensagemAdicional = "<p style='color: #22c55e; font-weight: 500; font-size: 15px; margin: 16px 0 0 0; text-align: center;'>Seu orçamento foi aprovado e nossos técnicos já iniciaram os trabalhos no seu veículo!</p>";
            }
            else if (novoStatus == "Finalizada")
            {
                mensagemAdicional = "<p style='color: #22c55e; font-weight: 500; font-size: 15px; margin: 16px 0 0 0; text-align: center;'>Todos os serviços foram concluídos com sucesso! Seu veículo está pronto e aguardando a entrega.</p>";
            }

            var htmlContent = $@"
<div style='font-family: ""Outfit"", ""Inter"", Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
    <div style='text-align: center; margin-bottom: 24px; padding-bottom: 20px; border-bottom: 2px solid #f1f5f9;'>
        <h2 style='color: #1e293b; margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.025em;'>AutoReparos</h2>
        <p style='color: #64748b; margin: 4px 0 0 0; font-size: 14px;'>Atualização da Ordem de Serviço</p>
    </div>

    <div style='margin-bottom: 24px;'>
        <p style='color: #334155; font-size: 16px; line-height: 1.6; margin: 0;'>Olá, <strong>{nomeDestinatario}</strong>,</p>
        <p style='color: #334155; font-size: 16px; line-height: 1.6; margin: 8px 0 0 0;'>
            Gostaríamos de informar que a sua Ordem de Serviço <strong>#{ordemServicoId.ToString().Substring(0, 8).ToUpper()}</strong> mudou de status:
        </p>
    </div>

    <div style='background-color: #f8fafc; border-radius: 8px; padding: 20px; margin-bottom: 24px; border: 1px solid #f1f5f9; text-align: center;'>
        <div style='display: inline-block; vertical-align: middle; padding: 8px 16px; background-color: #e2e8f0; border-radius: 20px; color: #475569; font-weight: 600; font-size: 14px;'>
            {labelAnterior}
        </div>
        <div style='display: inline-block; vertical-align: middle; margin: 0 12px; color: #94a3b8; font-size: 18px; font-weight: bold;'>
            ➔
        </div>
        <div style='display: inline-block; vertical-align: middle; padding: 8px 16px; background-color: #3b82f6; border-radius: 20px; color: #ffffff; font-weight: 600; font-size: 14px; box-shadow: 0 2px 4px rgba(59, 130, 246, 0.2);'>
            {labelNovo}
        </div>
        {mensagemAdicional}
    </div>

    <div style='margin-top: 32px; padding-top: 20px; border-top: 1px solid #e2e8f0; text-align: center; color: #94a3b8; font-size: 12px;'>
        <p style='margin: 0;'>Este é um e-mail automático enviado pelo sistema AutoReparos.</p>
        <p style='margin: 4px 0 0 0;'>FIAP - Pós-Graduação em Arquitetura de Software</p>
    </div>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            await EnviarComRetryAsync(client, msg, emailDestinatario, "atualizacao_status");
        }

        /// <summary>
        /// Executa o envio de e-mail via SendGrid com estratégia nativa de resiliência e retries.
        /// 
        /// AVISO ARQUITETURAL SOBRE IDEMPOTÊNCIA E RISCO DE DUPLICIDADE:
        /// A API v3 Mail Send do SendGrid NÃO suporta chaves de idempotência enviadas pelo cliente.
        /// Quando ocorre uma falha na camada de transporte de rede (TimeoutException, HttpRequestException),
        /// é impossível para o cliente determinar com 100% de certeza se a conexão foi interrompida antes
        /// ou depois de o servidor SendGrid ter aceito e enfileirado o disparo da mensagem.
        /// 
        /// Consequentemente, efetuar retry diante de quedas de rede acarreta um risco inerente de entrega
        /// "at-least-once" (possível duplicidade de recebimento pelo destinatário).
        /// Para mitigar esse impacto:
        /// 1. Limitamos os retries a no máximo 2 tentativas adicionais (total de 3 tentativas).
        /// 2. Respostas HTTP 4xx permanentes (400, 401, 403, 404) NÃO são retentadas.
        /// 3. Respostas HTTP 429 (Too Many Requests) respeitam estritamente o cabeçalho 'Retry-After'.
        ///    Neste cenário específico, o retry é 100% seguro contra duplicidade, pois o rate limiter do
        ///    SendGrid rejeita o processamento da mensagem antes de enfileirá-la.
        /// 4. Erros 5xx transitórios (502, 503, 504) e exceções de transporte utilizam backoff exponencial com jitter.
        /// </summary>
        private async Task EnviarComRetryAsync(
            SendGridClient client,
            SendGridMessage msg,
            string emailDestinatario,
            string tipoNotificacao)
        {
            const int maxTentativas = 3;
            var delayBase = TimeSpan.FromSeconds(1);

            for (var tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    var response = await client.SendEmailAsync(msg);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation(
                            "E-mail de {Tipo} enviado com sucesso para {Email} na tentativa {Tentativa}/{Max}. StatusCode: {StatusCode}",
                            tipoNotificacao, emailDestinatario, tentativa, maxTentativas, response.StatusCode);
                        return;
                    }

                    var statusCode = (int)response.StatusCode;

                    // Erros 4xx de cliente (exceto 429 Too Many Requests): falha permanente (ex: chave inválida, payload malformado)
                    if (statusCode >= 400 && statusCode < 500 && statusCode != (int)HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning(
                            "Falha permanente ao enviar e-mail de {Tipo} para {Email}. StatusCode: {StatusCode}. Abortando retries.",
                            tipoNotificacao, emailDestinatario, response.StatusCode);

                        RegistrarFalhaMetrica(tipoNotificacao, statusCode: statusCode);
                        return;
                    }

                    // Se atingiu a última tentativa permitida
                    if (tentativa == maxTentativas)
                    {
                        _logger.LogError(
                            "Limite de tentativas ({Max}) esgotado ao enviar e-mail de {Tipo} para {Email}. Último StatusCode: {StatusCode}",
                            maxTentativas, tipoNotificacao, emailDestinatario, response.StatusCode);

                        RegistrarFalhaMetrica(tipoNotificacao, statusCode: statusCode);
                        return;
                    }

                    // Calcula tempo de espera para erro transitório (429 ou 5xx)
                    var delay = CalcularDelay(response, tentativa, delayBase);

                    _logger.LogWarning(
                        "Falha transitória ao enviar e-mail de {Tipo} para {Email} (Tentativa {Tentativa}/{Max}). StatusCode: {StatusCode}. Aguardando {DelayMs}ms para retentar...",
                        tipoNotificacao, emailDestinatario, tentativa, maxTentativas, response.StatusCode, (int)delay.TotalMilliseconds);

                    await Task.Delay(delay);
                }
                catch (Exception ex) when (IsTransientException(ex) && tentativa < maxTentativas)
                {
                    var delay = CalcularDelayComJitter(tentativa, delayBase);

                    _logger.LogWarning(
                        ex,
                        "Exceção transitória de rede ao disparar e-mail de {Tipo} para {Email} (Tentativa {Tentativa}/{Max}). Aguardando {DelayMs}ms para retentar. [Aviso de risco: Possível entrega duplicada caso o gateway SendGrid tenha recebido a requisição antes da interrupção de transporte]",
                        tipoNotificacao, emailDestinatario, tentativa, maxTentativas, (int)delay.TotalMilliseconds);

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erro não recuperável ao disparar e-mail de {Tipo} para {Email} na tentativa {Tentativa}/{Max}",
                        tipoNotificacao, emailDestinatario, tentativa, maxTentativas);

                    RegistrarFalhaMetrica(tipoNotificacao, motivo: ex.GetType().Name);
                    return;
                }
            }
        }

        private static TimeSpan CalcularDelay(Response response, int tentativa, TimeSpan delayBase)
        {
            // 1. Respeita o cabeçalho Retry-After em caso de HTTP 429
            if (response.StatusCode == HttpStatusCode.TooManyRequests && response.Headers?.RetryAfter != null)
            {
                if (response.Headers.RetryAfter.Delta.HasValue)
                {
                    var delta = response.Headers.RetryAfter.Delta.Value;
                    return delta > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : delta;
                }

                if (response.Headers.RetryAfter.Date.HasValue)
                {
                    var delta = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero)
                    {
                        return delta > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : delta;
                    }
                }
            }

            // 2. Backoff exponencial com jitter para falhas 5xx e conexões
            return CalcularDelayComJitter(tentativa, delayBase);
        }

        private static TimeSpan CalcularDelayComJitter(int tentativa, TimeSpan delayBase)
        {
            var fator = Math.Pow(2, tentativa - 1);
            var baseMs = delayBase.TotalMilliseconds * fator;
            var jitterMs = Random.Shared.Next(100, 400);

            return TimeSpan.FromMilliseconds(baseMs + jitterMs);
        }

        private static bool IsTransientException(Exception ex)
        {
            return ex is HttpRequestException
                || ex is TimeoutException
                || ex is TaskCanceledException
                || ex is IOException
                || ex.InnerException is SocketException;
        }

        private static void RegistrarFalhaMetrica(string tipoNotificacao, int? statusCode = null, string? motivo = null)
        {
            var tags = new List<KeyValuePair<string, object?>>
            {
                new("canal", "email"),
                new("tipo", tipoNotificacao)
            };

            if (statusCode.HasValue)
            {
                tags.Add(new("status_code", statusCode.Value));
            }

            if (!string.IsNullOrEmpty(motivo))
            {
                tags.Add(new("motivo", motivo));
            }

            AutoReparosMetrics.NotificacoesFalhas.Add(1, [.. tags]);
        }

        private static string TraduzirStatus(string status)
        {
            return status switch
            {
                "Recebida" => "Recebida",
                "EmDiagnostico" => "Em Diagnóstico",
                "AguardandoAprovacao" => "Aguardando Aprovação",
                "EmExecucao" => "Em Execução",
                "Finalizada" => "Finalizada",
                "Entregue" => "Entregue",
                _ => status
            };
        }
    }
}
