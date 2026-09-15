using AutoReparos.Application.Shared.Metrics;
using AutoReparos.Domain.OrdensServicos.Entities;
using AutoReparos.Domain.OrdensServicos.Enums;
using AutoReparos.Infra.Data;
using AutoReparos.Infra.Data.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

namespace AutoReparos.Application.Tests.Shared;

/// <summary>
/// Os testes deste arquivo escutam o Meter estático <see cref="AutoReparosMetrics.Meter"/>.
/// Como o Meter é global ao processo, as medições de um teste vazariam para outro se
/// rodassem em paralelo — daí a coleção dedicada sem paralelismo.
/// </summary>
[CollectionDefinition("BusinessMetrics", DisableParallelization = true)]
public class BusinessMetricsCollection;

[Collection("BusinessMetrics")]
public class OrdemServicoMetricsInterceptorTests
{
    private const string Criadas = "ordens_servico.criadas";
    private const string Transicoes = "ordens_servico.transicoes_status";
    private const string TempoDiagnostico = "ordens_servico.tempo_diagnostico";
    private const string TempoExecucao = "ordens_servico.tempo_execucao";
    private const string TempoPermanencia = "ordens_servico.tempo_permanencia";

    /// <summary>
    /// Captura as medições emitidas pelo Meter de negócio durante a execução de uma ação.
    /// </summary>
    private sealed class MedicoesCapturadas : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<(string Instrumento, double Valor, string? Status)> _medicoes = [];
        private readonly Lock _sync = new();

        public MedicoesCapturadas()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrumento, listener) =>
                {
                    if (instrumento.Meter.Name == AutoReparosMetrics.MeterName)
                        listener.EnableMeasurementEvents(instrumento);
                }
            };

            _listener.SetMeasurementEventCallback<long>((inst, valor, tags, _) => Registrar(inst, valor, tags));
            _listener.SetMeasurementEventCallback<double>((inst, valor, tags, _) => Registrar(inst, valor, tags));
            _listener.Start();
        }

        private void Registrar(Instrument instrumento, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            string? status = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "status")
                    status = tag.Value?.ToString();
            }

            lock (_sync)
            {
                _medicoes.Add((instrumento.Name, valor, status));
            }
        }

        public IReadOnlyList<(string Instrumento, double Valor, string? Status)> Todas
        {
            get { lock (_sync) { return [.. _medicoes]; } }
        }

        public IEnumerable<(string Instrumento, double Valor, string? Status)> De(string instrumento)
            => Todas.Where(m => m.Instrumento == instrumento);

        public void Dispose() => _listener.Dispose();
    }

    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"metrics-{Guid.NewGuid()}")
            .AddInterceptors(new OrdemServicoMetricsInterceptor())
            .Options;

        return new AppDbContext(options);
    }

    private static OrdemServico CriarOrdemPersistida(AppDbContext contexto)
    {
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid(), "teste");
        contexto.OrdensServico.Add(ordem);
        contexto.SaveChanges();
        return ordem;
    }

    [Fact]
    public void Deve_ContabilizarOrdemCriada_QuandoUmaNovaOrdemEPersistida()
    {
        // Arrange
        using var medicoes = new MedicoesCapturadas();
        using var contexto = CriarContexto();

        // Act
        contexto.OrdensServico.Add(new OrdemServico(Guid.NewGuid(), Guid.NewGuid(), "nova"));
        contexto.SaveChanges();

        // Assert
        medicoes.De(Criadas).Should().ContainSingle()
            .Which.Should().Match<(string, double, string?)>(m =>
                m.Item2 == 1 && m.Item3 == nameof(EStatusOrdemServico.Recebida));

        medicoes.De(Transicoes).Should().ContainSingle()
            .Which.Item3.Should().Be(nameof(EStatusOrdemServico.Recebida));
    }

    [Fact]
    public void Deve_ContabilizarTransicao_SemContarComoCriada_QuandoOStatusMuda()
    {
        // Arrange
        using var contexto = CriarContexto();
        var ordem = CriarOrdemPersistida(contexto);

        using var medicoes = new MedicoesCapturadas();

        // Act
        ordem.IniciarDiagnostico("mecanico-1");
        contexto.SaveChanges();

        // Assert
        medicoes.De(Criadas).Should().BeEmpty("a ordem já existia, apenas mudou de status");

        medicoes.De(Transicoes).Should().ContainSingle()
            .Which.Item3.Should().Be(nameof(EStatusOrdemServico.EmDiagnostico));
    }

    [Fact]
    public void NaoDeve_EmitirMetrica_QuandoASalvarSemMudancaDeStatus()
    {
        // Arrange
        using var contexto = CriarContexto();
        var ordem = CriarOrdemPersistida(contexto);

        using var medicoes = new MedicoesCapturadas();

        // Act — altera a OS sem tocar no status
        contexto.Entry(ordem).Property(os => os.Observacao).CurrentValue = "observação revisada";
        contexto.SaveChanges();

        // Assert
        medicoes.Todas.Should().BeEmpty();
    }

    [Fact]
    public void Deve_RegistrarTempoDeDiagnostico_AoEnviarParaAprovacao()
    {
        // Arrange
        using var contexto = CriarContexto();
        var ordem = CriarOrdemPersistida(contexto);

        ordem.AdicionarServico(new OrdemServicoServico(ordem.Id, Guid.NewGuid(), 150m));
        ordem.IniciarDiagnostico("mecanico-1");
        contexto.SaveChanges();

        using var medicoes = new MedicoesCapturadas();

        // Act
        ordem.AguardarAprovacao("mecanico-1");
        contexto.SaveChanges();

        // Assert
        medicoes.De(Transicoes).Should().ContainSingle()
            .Which.Item3.Should().Be(nameof(EStatusOrdemServico.AguardandoAprovacao));

        medicoes.De(TempoDiagnostico).Should().ContainSingle()
            .Which.Item2.Should().BeGreaterThanOrEqualTo(0,
                "a duração vem de EnvioAprovacaoEm - DiagnosticoIniciadoEm");
    }

    [Fact]
    public void Deve_RegistrarTempoDeExecucaoEPermanencia_AoLongoDoCicloCompleto()
    {
        // Arrange
        using var contexto = CriarContexto();
        var ordem = CriarOrdemPersistida(contexto);

        var servico = new OrdemServicoServico(ordem.Id, Guid.NewGuid(), 200m);
        ordem.AdicionarServico(servico);
        ordem.IniciarDiagnostico("mecanico-1");
        ordem.AguardarAprovacao("mecanico-1");
        ordem.Aprovar();
        contexto.SaveChanges();

        using var medicoes = new MedicoesCapturadas();

        // Act — finaliza e entrega
        ordem.IniciarServico(servico.Id);
        ordem.ConcluirServico(servico.Id);
        contexto.SaveChanges();

        ordem.Entregar();
        contexto.SaveChanges();

        // Assert
        medicoes.De(Transicoes).Select(m => m.Status).Should().BeEquivalentTo(
            [nameof(EStatusOrdemServico.Finalizada), nameof(EStatusOrdemServico.Entregue)]);

        medicoes.De(TempoExecucao).Should().ContainSingle(
            "o tempo de execução é registrado exatamente uma vez, ao finalizar");

        medicoes.De(TempoPermanencia).Should().ContainSingle(
            "o tempo de permanência é registrado apenas na entrega");
    }

    [Fact]
    public void NaoDeve_EmitirMetricas_QuandoAsPendenciasSaoDescartadas()
    {
        // Arrange — captura pendências no ChangeTracker sem nunca concluir o SaveChanges
        using var contexto = CriarContexto();
        var ordem = CriarOrdemPersistida(contexto);

        using var medicoes = new MedicoesCapturadas();

        // Act — muda o status mas descarta as alterações antes de persistir
        ordem.IniciarDiagnostico("mecanico-1");
        contexto.ChangeTracker.Entries<OrdemServico>().ToList()
            .ForEach(e => e.State = EntityState.Unchanged);
        contexto.SaveChanges();

        // Assert
        medicoes.Todas.Should().BeEmpty("nada foi efetivamente persistido");
    }
}
