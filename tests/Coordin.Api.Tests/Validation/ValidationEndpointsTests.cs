using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// Spec validation-api (RF-24; DT-37; RNF-04): a área de validação só existe com <c>Validation:Enabled</c>. Desligada,
/// as rotas respondem 404, até para o gestor autenticado. Ligada, o gestor autenticado tem o estado da validação.
/// </summary>
public sealed class ValidationEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class Disabled(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
    {
        [Fact]
        public async Task Status_IsNotFound_EvenForTheAuthenticatedManager_WhenTheAreaIsDisabled()
        {
            using var client = await factory.CreateAuthenticatedClientAsync();

            var response = await client.GetAsync("/api/validation/status", Ct);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Status_IsNotFound_ForAnonymousClients_WhenTheAreaIsDisabled()
        {
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/validation/status", Ct);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public sealed class Enabled(ValidationEnabledApiFactory factory) : IClassFixture<ValidationEnabledApiFactory>
    {
        [Fact]
        public async Task Status_RespondsUnauthorized_WithoutAToken()
        {
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/validation/status", Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Status_ReportsNothingLoaded_BeforeAnyLoad()
        {
            using var client = await factory.CreateAuthenticatedClientAsync();

            var response = await client.GetAsync("/api/validation/status", Ct);
            var status = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, status.GetProperty("demandCount").GetInt32());
            Assert.Equal(0, status.GetProperty("baselineCount").GetInt32());
            Assert.False(status.GetProperty("currentPlanCaptured").GetBoolean());
            Assert.Equal(JsonValueKind.Null, status.GetProperty("currentPlanCapturedAt").ValueKind);
        }
    }
}
