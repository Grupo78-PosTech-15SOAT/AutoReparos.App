using AutoReparos.API.Middlewares;
using AutoReparos.IntegrationTests.Infrastructure;
using FluentAssertions;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace AutoReparos.IntegrationTests.Features.Middlewares
{
    public class CorrelationIdIntegrationTests(CustomWebApplicationFactory<Program> factory)
        : IntegrationTestBase(factory)
    {
        [Fact(DisplayName = "E2E: Request sem X-Correlation-Id deve gerar novo GUID no response header")]
        public async Task Request_SemCorrelationId_DeveGerarNovoGuidNoHeaderResponse()
        {
            // Act
            var response = await Client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeaderName, out var values).Should().BeTrue();
            var correlationId = values!.FirstOrDefault();
            correlationId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(correlationId, out _).Should().BeTrue();
        }

        [Fact(DisplayName = "E2E: Request com X-Correlation-Id válido deve preservar e ecoar no response header")]
        public async Task Request_ComCorrelationIdValido_DevePreservarEEcoarNoResponseHeader()
        {
            // Arrange
            var expectedCorrelationId = "test-corr-id-abc-123456";
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeaderName, expectedCorrelationId);

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeaderName, out var values).Should().BeTrue();
            var actualId = values!.FirstOrDefault();
            actualId.Should().Be(expectedCorrelationId);
        }

        [Fact(DisplayName = "E2E: Request com X-Correlation-Id excessivamente grande deve gerar novo GUID")]
        public async Task Request_ComCorrelationIdExcessivamenteGrande_DeveGerarNovoGuid()
        {
            // Arrange
            var excessiveId = new string('x', 150);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeaderName, excessiveId);

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeaderName, out var values).Should().BeTrue();
            var actualId = values!.FirstOrDefault();
            actualId.Should().NotBeNullOrWhiteSpace();
            actualId.Should().NotBe(excessiveId);
            Guid.TryParse(actualId, out _).Should().BeTrue();
        }

        [Fact(DisplayName = "E2E: Request com caracteres maliciosos deve gerar novo GUID seguro")]
        public async Task Request_ComCaracteresInvalidos_DeveGerarNovoGuid()
        {
            // Arrange
            var maliciousId = "invalid_id_with_special_chars!@#$%";
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeaderName, maliciousId);

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Headers.TryGetValues(CorrelationIdMiddleware.CorrelationIdHeaderName, out var values).Should().BeTrue();
            var actualId = values!.FirstOrDefault();
            actualId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(actualId, out _).Should().BeTrue();
        }
    }
}
