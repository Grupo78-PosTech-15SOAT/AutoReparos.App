using AutoReparos.Application.Shared.Metrics;
using FluentAssertions;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Xunit;

namespace AutoReparos.Application.Tests.Shared
{
    public class BusinessMetricsTests
    {
        [Fact]
        public void Meter_DeveExporNomeEVersaoEsperadosPeloOpenTelemetry()
        {
            AutoReparosMetrics.Meter.Name.Should().Be("AutoReparos.BusinessMetrics");
            AutoReparosMetrics.Meter.Version.Should().Be("1.0.0");
        }

        [Fact]
        public void NotificacoesFalhas_DeveEstarRegistradoComNomeEUnidadeCorretos()
        {
            AutoReparosMetrics.NotificacoesFalhas.Name.Should().Be("notificacoes.falhas");
            AutoReparosMetrics.NotificacoesFalhas.Unit.Should().Be("{falhas}");
        }

        [Fact]
        public void OrdensServicoCriadas_DeveEstarRegistradoComNomeEUnidadeCorretos()
        {
            AutoReparosMetrics.OrdensServicoCriadas.Name.Should().Be("ordens_servico.criadas");
            AutoReparosMetrics.OrdensServicoCriadas.Unit.Should().Be("{ordens}");
        }

        [Fact]
        public void NotificacoesFalhas_DeveEmitirMedicaoComTagsContextuais()
        {
            var medicoes = new List<(long Valor, Dictionary<string, object?> Tags)>();

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrumento, l) =>
            {
                if (instrumento.Meter.Name == AutoReparosMetrics.MeterName &&
                    instrumento.Name == "notificacoes.falhas")
                {
                    l.EnableMeasurementEvents(instrumento);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrumento, valor, tags, estado) =>
            {
                var mapa = new Dictionary<string, object?>();
                foreach (var tag in tags)
                {
                    mapa[tag.Key] = tag.Value;
                }
                medicoes.Add((valor, mapa));
            });
            listener.Start();

            AutoReparosMetrics.NotificacoesFalhas.Add(1,
                new KeyValuePair<string, object?>("canal", "email"),
                new KeyValuePair<string, object?>("tipo", "orcamento"),
                new KeyValuePair<string, object?>("motivo", "SendGridException"));

            medicoes.Should().ContainSingle();
            medicoes[0].Valor.Should().Be(1);
            medicoes[0].Tags.Should().Contain("canal", "email");
            medicoes[0].Tags.Should().Contain("tipo", "orcamento");
            medicoes[0].Tags.Should().Contain("motivo", "SendGridException");
        }

        [Fact]
        public void NotificacoesFalhas_DeveAcumularMultiplasFalhasDeCanaisDistintos()
        {
            var total = 0L;

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrumento, l) =>
            {
                if (instrumento.Meter.Name == AutoReparosMetrics.MeterName &&
                    instrumento.Name == "notificacoes.falhas")
                {
                    l.EnableMeasurementEvents(instrumento);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrumento, valor, tags, estado) => total += valor);
            listener.Start();

            AutoReparosMetrics.NotificacoesFalhas.Add(1,
                new KeyValuePair<string, object?>("canal", "email"),
                new KeyValuePair<string, object?>("tipo", "orcamento"),
                new KeyValuePair<string, object?>("status_code", 500));
            AutoReparosMetrics.NotificacoesFalhas.Add(1,
                new KeyValuePair<string, object?>("canal", "email"),
                new KeyValuePair<string, object?>("tipo", "atualizacao_status"),
                new KeyValuePair<string, object?>("motivo", "TaskCanceledException"));

            total.Should().Be(2);
        }
    }
}
