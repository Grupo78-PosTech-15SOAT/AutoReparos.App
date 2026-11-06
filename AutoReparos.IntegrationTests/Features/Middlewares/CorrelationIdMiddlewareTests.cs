using AutoReparos.API.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AutoReparos.IntegrationTests.Features.Middlewares
{
    public class CorrelationIdMiddlewareTests
    {
        private readonly NullLogger<CorrelationIdMiddleware> _logger = new();

        [Fact]
        public async Task InvokeAsync_WithValidCorrelationId_ShouldPreserveIdAndEchoInResponseHeader()
        {
            // Arrange
            var expectedCorrelationId = "test-correlation-id-12345";
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            RequestDelegate next = ctx =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context);
            await context.Response.StartAsync();

            // Assert
            nextInvoked.Should().BeTrue();
            context.Items[CorrelationIdMiddleware.CorrelationIdItemKey].Should().Be(expectedCorrelationId);
            context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString().Should().Be(expectedCorrelationId);
        }

        [Fact]
        public async Task InvokeAsync_WithoutCorrelationId_ShouldGenerateValidGuidAndEchoInResponseHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            RequestDelegate next = ctx =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context);
            await context.Response.StartAsync();

            // Assert
            nextInvoked.Should().BeTrue();
            var generatedId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
            generatedId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(generatedId, out _).Should().BeTrue();
            context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString().Should().Be(generatedId);
        }

        [Fact]
        public async Task InvokeAsync_WithInvalidCharacters_ShouldGenerateNewValidGuid()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "invalid\r\nInjectedHeader: attack<script>";
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            RequestDelegate next = ctx =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context);
            await context.Response.StartAsync();

            // Assert
            nextInvoked.Should().BeTrue();
            var generatedId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
            generatedId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(generatedId, out _).Should().BeTrue();
            generatedId.Should().NotContain("attack");
            context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString().Should().Be(generatedId);
        }

        [Fact]
        public async Task InvokeAsync_WithExcessiveLength_ShouldGenerateNewValidGuid()
        {
            // Arrange
            var excessiveId = new string('a', 150);
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = excessiveId;
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            RequestDelegate next = ctx =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context);
            await context.Response.StartAsync();

            // Assert
            nextInvoked.Should().BeTrue();
            var generatedId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
            generatedId.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(generatedId, out _).Should().BeTrue();
            generatedId.Should().NotBe(excessiveId);
        }

        [Fact]
        public async Task InvokeAsync_WithOptionsRequest_ShouldPassThroughAndSetHeaderWithoutLogScopeError()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Options;
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            RequestDelegate next = ctx =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            var middleware = new CorrelationIdMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context);
            await context.Response.StartAsync();

            // Assert
            nextInvoked.Should().BeTrue();
            context.Response.Headers.ContainsKey(CorrelationIdMiddleware.CorrelationIdHeaderName).Should().BeTrue();
        }
    }
}
