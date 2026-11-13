using System.Text;
using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Coordin.Api.Infrastructure.Auth;

/// <summary>Autenticação do gestor: JWT + Identity (DT-28, DT-29). Cresce a cada task da change.</summary>
public static class AuthenticationSetup
{
    public const int MinimumPasswordLength = 12;
    public const int MaxFailedAccessAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddCoordinAuthentication(this IServiceCollection services)
    {
        // ValidateOnStart: uma chave ausente ou curta impede a API de subir, em vez de falhar
        // só no primeiro login.
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddOptions<ManagerOptions>()
            .BindConfiguration(ManagerOptions.SectionName);

        // AddIdentityCore (e não AddIdentity): só usuário, hash de senha e bloqueio. Sem cookie,
        // sem telas e sem endpoints prontos (DT-28).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                // O DT-28 fixa só o tamanho mínimo; não se exige composição de caracteres.
                options.Password.RequiredLength = MinimumPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<CoordinDbContext>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ManagerAccountSeeder>();
        services.AddSingleton<TokenService>();
        services.AddScoped<RefreshTokenService>();

        AddJwtBearer(services);

        // Proteção por padrão (DT-28): todo endpoint sem regra própria exige o gestor autenticado.
        // As poucas exceções usam AllowAnonymous de forma explícita.
        services.AddAuthorizationBuilder().SetFallbackPolicy(AuthenticatedOrNoEndpointPolicy());

        return services;
    }

    /// <summary>Authentication antes de Authorization; os dois depois dos arquivos estáticos (D5).</summary>
    public static WebApplication UseCoordinAuthentication(this WebApplication app)
    {
        // Chamados direto no app (e não dentro de UseWhen): o WebApplication só reconhece assim que
        // a autorização já foi posicionada e deixa de inseri-la sozinho antes dos arquivos estáticos.
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// "Usuário autenticado", com uma única exceção: a política de fallback também é aplicada a uma
    /// requisição que não casou com nenhum endpoint (por exemplo, um arquivo estático inexistente
    /// como <c>/x.js</c>), e ela responderia 401 em vez de 404 (spec web-shell). Sem endpoint não há
    /// código que devolva dados, então liberar esse caso não expõe nada.
    /// </summary>
    private static AuthorizationPolicy AuthenticatedOrNoEndpointPolicy() =>
        new AuthorizationPolicyBuilder()
            .RequireAssertion(context =>
                context.User.Identity?.IsAuthenticated == true
                || (context.Resource is HttpContext http && http.GetEndpoint() is null))
            .Build();

    private static void AddJwtBearer(IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // As opções do JwtBearer são montadas na primeira requisição, quando a configuração final
        // (user-secrets ou variável de ambiente) já existe e foi validada na inicialização.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Mantém os nomes originais das claims ('sub', 'email') em vez de renomeá-las.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    // Só HMAC-SHA256, o algoritmo que a API emite: recusa tokens "none" ou de outro algoritmo.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireSignedTokens = true,

                    // 15 minutos exatos: sem os 5 minutos de tolerância que o padrão da biblioteca concede.
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });
    }
}
