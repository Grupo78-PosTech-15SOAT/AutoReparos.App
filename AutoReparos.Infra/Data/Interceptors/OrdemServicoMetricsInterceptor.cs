using AutoReparos.Application.Shared.Metrics;
using AutoReparos.Domain.OrdensServicos.Entities;
using AutoReparos.Domain.OrdensServicos.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutoReparos.Infra.Data.Interceptors
{
    /// <summary>
    /// Emite as métricas de negócio de ordens de serviço a partir do ChangeTracker.
    ///
    /// Concentrar a instrumentação aqui garante que qualquer use case que crie uma OS ou
    /// altere seu status seja contabilizado, sem depender de cada fluxo lembrar de chamar
    /// o contador. As métricas só são incrementadas após o commit bem-sucedido, para que
    /// uma transação revertida não gere volume fantasma nos dashboards.
    /// </summary>
    public class OrdemServicoMetricsInterceptor : SaveChangesInterceptor
    {
        private readonly record struct Pendente(
            bool Criada,
            string? StatusDestino,
            double? TempoDiagnosticoSegundos,
            double? TempoExecucaoSegundos,
            double? TempoPermanenciaSegundos);

        private readonly List<Pendente> _pendentes = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Capturar(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Capturar(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Emitir();
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Emitir();
            return base.SavedChanges(eventData, result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            _pendentes.Clear();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _pendentes.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        private void Capturar(DbContext? context)
        {
            if (context is null)
                return;

            foreach (var entry in context.ChangeTracker.Entries<OrdemServico>())
            {
                if (entry.State == EntityState.Added)
                {
                    _pendentes.Add(new Pendente(true, entry.Entity.Status.ToString(), null, null, null));
                    continue;
                }

                if (entry.State != EntityState.Modified)
                    continue;

                var status = entry.Property(os => os.Status);
                if (!status.IsModified || Equals(status.OriginalValue, status.CurrentValue))
                    continue;

                var os = entry.Entity;

                // As durações do ciclo de vida são derivadas dos carimbos de tempo da própria OS
                // no exato momento em que ela entra no status correspondente, de forma que cada
                // ordem contribua com uma única amostra para o histograma.
                _pendentes.Add(new Pendente(
                    false,
                    status.CurrentValue.ToString(),
                    Duracao(os.DiagnosticoIniciadoEm, os.EnvioAprovacaoEm),
                    Duracao(os.IniciadoEm, os.FinalizadoEm),
                    status.CurrentValue == EStatusOrdemServico.Entregue
                        ? Duracao(os.CriadoEm, os.EntregueEm)
                        : null));
            }
        }

        private static double? Duracao(DateTime? inicio, DateTime? fim)
            => inicio.HasValue && fim.HasValue && fim.Value >= inicio.Value
                ? (fim.Value - inicio.Value).TotalSeconds
                : null;

        private void Emitir()
        {
            foreach (var pendente in _pendentes)
            {
                var status = new KeyValuePair<string, object?>("status", pendente.StatusDestino);

                if (pendente.Criada)
                    AutoReparosMetrics.OrdensServicoCriadas.Add(1, status);

                AutoReparosMetrics.OrdensServicoTransicoesStatus.Add(1, status);

                if (pendente.StatusDestino == nameof(EStatusOrdemServico.AguardandoAprovacao)
                    && pendente.TempoDiagnosticoSegundos is { } diagnostico)
                    AutoReparosMetrics.TempoDiagnostico.Record(diagnostico);

                if (pendente.StatusDestino == nameof(EStatusOrdemServico.Finalizada)
                    && pendente.TempoExecucaoSegundos is { } execucao)
                    AutoReparosMetrics.TempoExecucao.Record(execucao);

                if (pendente.TempoPermanenciaSegundos is { } permanencia)
                    AutoReparosMetrics.TempoTotalPermanencia.Record(permanencia);
            }

            _pendentes.Clear();
        }
    }
}
