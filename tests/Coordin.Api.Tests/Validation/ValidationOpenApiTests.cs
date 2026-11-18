using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Validation;

/// <summary>A documentação só lista a validação quando ela está ligada (DT-37): o cliente gerado não ganha rotas à toa.</summary>
public sealed class ValidationOpenApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<JsonElement>> OperationsAsync(HttpClient client)
    {
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct);
        return document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value)
            .ToList();
    }

    public sealed class Disabled(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
    {
        [Fact]
        public async Task OpenApi_DoesNotList_TheValidationStatus_WhenDisabled()
        {
            using var client = factory.CreateClient();

            var operations = await OperationsAsync(client);

            Assert.DoesNotContain(operations, operation => operation.GetProperty("operationId").GetString() == "GetValidationStatus");
        }
    }

    public sealed class Enabled(ValidationEnabledDevelopmentFactory factory) : IClassFixture<ValidationEnabledDevelopmentFactory>
    {
        [Fact]
        public async Task OpenApi_ListsTheValidationStatus_UnderTheValidationTag_WhenEnabled()
        {
            using var client = factory.CreateClient();

            var operations = await OperationsAsync(client);

            var found = Assert.Single(operations, operation => operation.GetProperty("operationId").GetString() == "GetValidationStatus");
            Assert.Equal("Validation", found.GetProperty("tags")[0].GetString());
        }
    }
}
