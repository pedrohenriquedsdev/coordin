using Microsoft.Extensions.Options;

namespace Coordin.Api.Infrastructure.Auth;

/// <summary>Autenticação do gestor: JWT + Identity (DT-28, DT-29). Cresce a cada task da change.</summary>
public static class AuthenticationSetup
{
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

        return services;
    }
}
