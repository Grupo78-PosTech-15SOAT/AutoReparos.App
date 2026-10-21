using AutoReparos.Application.Auth.DTOs.Request;
using AutoReparos.Application.Auth.DTOs.Response;
using AutoReparos.Application.Auth.Services.Interfaces;
using AutoReparos.Domain.Usuarios.Entities;
using AutoReparos.Domain.Usuarios.Enums;
using AutoReparos.Infra.Data;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace AutoReparos.IntegrationTests.Infrastructure
{
    [Collection("IntegrationTests")]
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected const string DefaultAdminEmail = "admin@autoreparos.com";
        protected const string DefaultAdminPassword = "Admin@123";

        protected readonly CustomWebApplicationFactory<Program> Factory;
        protected readonly HttpClient Client;
        protected readonly IServiceProvider Services;

        protected IntegrationTestBase(CustomWebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = Factory.CreateClient();
            Services = Factory.Services;
        }

        protected async Task AuthenticateAsync()
        {
            var loginRequest = new LoginRequestDto(DefaultAdminEmail, DefaultAdminPassword);
            var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);
            }
        }

        protected void AuthenticateAsRole(ETipoUsuario tipoUsuario)
        {
            using var scope = Services.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var usuario = new Usuario("Test User", $"test_{tipoUsuario.ToString().ToLower()}@example.com", tipoUsuario);
            var token = jwtService.GenerateToken(usuario);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task InitializeAsync()
        {
            await Factory.ResetDatabaseAsync();
            await DbInitializer.SeedDataAsync(Services, skipMigration: true);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
