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
        /// <summary>
        /// Contador de transições de status de ordens de serviço, dimensionado pela tag
        /// <c>status</c>. Alimenta o painel de volume diário discriminado por status.
        /// </summary>
        public static readonly Counter<long> OrdensServicoTransicoesStatus =
            Meter.CreateCounter<long>(
                name: "ordens_servico.transicoes_status",
                unit: "{transicoes}",
                description: "Transições de status de ordens de serviço, por status de destino");

        /// <summary>
        /// Duração da etapa de diagnóstico (do início do diagnóstico ao envio para aprovação).
        /// </summary>
        public static readonly Histogram<double> TempoDiagnostico =
            Meter.CreateHistogram<double>(
                name: "ordens_servico.tempo_diagnostico",
                unit: "s",
                description: "Tempo entre o início do diagnóstico e o envio do orçamento para aprovação");

        /// <summary>
        /// Duração da etapa de execução (do início dos serviços à finalização da OS).
        /// </summary>
        public static readonly Histogram<double> TempoExecucao =
            Meter.CreateHistogram<double>(
                name: "ordens_servico.tempo_execucao",
                unit: "s",
                description: "Tempo entre o início da execução e a finalização da ordem de serviço");

        /// <summary>
        /// Tempo total de permanência da OS na oficina (da criação à entrega ao cliente).
        /// </summary>
        public static readonly Histogram<double> TempoTotalPermanencia =
            Meter.CreateHistogram<double>(
                name: "ordens_servico.tempo_permanencia",
                unit: "s",
                description: "Tempo total entre a criação da ordem de serviço e a entrega do veículo");

        public static readonly Counter<long> OrdensServicoCriadas =
            Meter.CreateCounter<long>(
                name: "ordens_servico.criadas",
                unit: "{ordens}",
                description: "Volume de ordens de serviço criadas");
    }
}
