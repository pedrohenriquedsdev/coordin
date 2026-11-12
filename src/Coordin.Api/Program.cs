using Coordin.Api.Features.Diagnostics;
using Coordin.Api.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoordinJson();
builder.Services.AddCoordinProblemDetails();

var app = builder.Build();

app.UseCoordinProblemDetails();

app.MapTestOnlyEndpoints();
app.MapApiFallback();

app.Run();

// Exposto para a WebApplicationFactory dos testes de integração.
public partial class Program;
