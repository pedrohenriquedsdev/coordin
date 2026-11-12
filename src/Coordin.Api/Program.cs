using Coordin.Api.Features.Diagnostics;
using Coordin.Api.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoordinJson();
builder.Services.AddCoordinProblemDetails();
builder.Services.AddCoordinTransportSecurity();

var app = builder.Build();

// A ordem importa (D5): esquema original e HTTPS primeiro, cabeçalhos de segurança antes de
// tudo o que gera resposta e, só então, o formato de erro.
app.UseCoordinTransportSecurity();
app.UseCoordinSecurityHeaders();
app.UseCoordinProblemDetails();

app.MapTestOnlyEndpoints();
app.MapApiFallback();

app.Run();

// Exposto para a WebApplicationFactory dos testes de integração.
public partial class Program;
