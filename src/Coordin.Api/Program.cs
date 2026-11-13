using Coordin.Api.Features.Auth;
using Coordin.Api.Features.Diagnostics;
using Coordin.Api.Features.Health;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoordinJson();
builder.Services.AddCoordinProblemDetails();
builder.Services.AddCoordinTransportSecurity();
builder.Services.AddCoordinPersistence();
builder.Services.AddCoordinAuthentication();
builder.Services.AddCoordinHealth();
builder.Services.AddCoordinApiDocumentation();

var app = builder.Build();

// O banco precisa estar na versão esperada pelo código antes de qualquer requisição.
await app.MigrateCoordinDatabaseAsync();

// A conta do gestor vem da configuração (DT-29); sem cadastro público.
await app.EnsureManagerAccountAsync();

// A ordem importa (D5): esquema original e HTTPS primeiro, cabeçalhos de segurança antes de
// tudo o que gera resposta e, só então, o formato de erro.
app.UseCoordinTransportSecurity();
app.UseCoordinSecurityHeaders();
app.UseCoordinProblemDetails();

app.UseCoordinApiDocumentation();
app.UseCoordinSpaFiles();
app.UseCoordinAuthentication();

app.MapHealthEndpoint();
app.MapAuthEndpoints();
app.MapTeamEndpoints();
app.MapPersonEndpoints();
app.MapTestOnlyEndpoints();
app.MapApiFallback();
app.MapSpaFallback(); // por último: o fallback de /api é mais específico e vem antes

app.Run();

// Exposto para a WebApplicationFactory dos testes de integração.
public partial class Program;
