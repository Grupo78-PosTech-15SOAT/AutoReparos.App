using System.Diagnostics.Metrics;

namespace AutoReparos.Application.Shared.Metrics
{
    /// <summary>
    /// Ponto central de definição das métricas de negócio do AutoReparos.
    /// Exposto via OpenTelemetry através do Meter <see cref="MeterName"/>.
    /// </summary>
    public static class AutoReparosMetrics
    {
        public const string MeterName = "AutoReparos.BusinessMetrics";
        public const string MeterVersion = "1.0.0";

        public static readonly Meter Meter = new(MeterName, MeterVersion);

        /// <summary>
        /// Contador de falhas no envio de notificações externas (ex: SendGrid).
        /// Requisito mandatório da Fase 3 para detecção e alerta de falhas de integração.
        /// </summary>
        public static readonly Counter<long> NotificacoesFalhas =
            Meter.CreateCounter<long>(
                name: "notificacoes.falhas",
                unit: "{falhas}",
                description: "Registra falhas no processamento e envio de notificações para clientes");

        /// <summary>
        /// Contador de ordens de serviço criadas, para o dashboard de volume diário.
        /// </summary>
        public static readonly Counter<long> OrdensServicoCriadas =
            Meter.CreateCounter<long>(
                name: "ordens_servico.criadas",
                unit: "{ordens}",
                description: "Volume de ordens de serviço criadas");
    }
}
