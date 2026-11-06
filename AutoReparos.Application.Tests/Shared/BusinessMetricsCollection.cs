using Xunit;

namespace AutoReparos.Application.Tests.Shared;

/// <summary>
/// Coleção xUnit compartilhada para testes que escutam ou emitem medições no Meter estático
/// <see cref="AutoReparos.Application.Shared.Metrics.AutoReparosMetrics.Meter"/>.
/// Como o Meter e os instrumentos são globais ao processo, as medições de testes executados
/// em paralelo vazariam entre si, causando intermitências (flaky tests).
/// </summary>
[CollectionDefinition("BusinessMetrics", DisableParallelization = true)]
public class BusinessMetricsCollection
{
}
