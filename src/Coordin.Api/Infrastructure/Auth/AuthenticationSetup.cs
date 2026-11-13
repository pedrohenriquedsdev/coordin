using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

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

        return services;
    }
}
