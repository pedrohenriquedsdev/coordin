using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Garante a conta única do gestor na inicialização (DT-29). Não existe cadastro público: a conta
/// nasce de <c>Manager__Email</c> e <c>Manager__Password</c>, e uma senha esquecida se troca com
/// <c>Manager__ResetPassword=true</c>. Nenhuma mensagem, exceção ou log daqui repete a senha.
/// </summary>
public sealed partial class ManagerAccountSeeder(
    UserManager<ApplicationUser> users,
    CoordinDbContext db,
    IOptions<ManagerOptions> options,
    TimeProvider clock,
    ILogger<ManagerAccountSeeder> logger)
{
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var existing = await users.Users.OrderBy(user => user.Id).FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            await CreateAsync(settings);
            return;
        }

        if (settings.ResetPassword)
        {
            await ResetPasswordAsync(existing, settings, cancellationToken);
        }
    }

    private async Task CreateAsync(ManagerOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrEmpty(settings.Password))
        {
            // A API sobe (o health continua útil para a hospedagem), mas ninguém consegue entrar.
            LogManagerNotConfigured(logger);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
        };

        // CreateAsync valida e-mail e senha (mínimo de 12 caracteres) antes de gravar qualquer coisa.
        var result = await users.CreateAsync(user, settings.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "A conta do gestor não pôde ser criada com 'Manager:Email' e 'Manager:Password'. " +
                Describe(result.Errors));
        }

        LogManagerCreated(logger);
    }

    private async Task ResetPasswordAsync(
        ApplicationUser user,
        ManagerOptions settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(settings.Password))
        {
            throw new InvalidOperationException(
                "'Manager:ResetPassword' está ligado, mas 'Manager:Password' não foi informada.");
        }

        // Valida ANTES de mexer na conta: se a nova senha for inválida, a senha atual continua
        // valendo e o gestor não fica trancado para fora.
        var errors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(users, user, settings.Password);
            errors.AddRange(validation.Errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "A senha de 'Manager:Password' não pôde ser redefinida; a senha atual foi mantida. " +
                Describe(errors));
        }

        if (await users.HasPasswordAsync(user))
        {
            Ensure(await users.RemovePasswordAsync(user));
        }

        Ensure(await users.AddPasswordAsync(user, settings.Password));

        // Quem redefine a senha geralmente está trancado para fora: zera o bloqueio e as tentativas.
        Ensure(await users.SetLockoutEndDateAsync(user, null));
        Ensure(await users.ResetAccessFailedCountAsync(user));

        // Toda sessão aberta com a senha antiga deixa de renovar.
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

        LogManagerPasswordReset(logger);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Falha ao redefinir a conta do gestor. " + Describe(result.Errors));
        }
    }

    // Só códigos e descrições do Identity: eles nunca contêm a senha enviada.
    private static string Describe(IEnumerable<IdentityError> errors) =>
        "Motivos: " + string.Join("; ", errors.Select(error => $"{error.Code} ({error.Description})"));

    [LoggerMessage(Level = LogLevel.Warning, Message =
        "Nenhuma conta de gestor existe e 'Manager:Email' ou 'Manager:Password' não estão configurados: " +
        "ninguém conseguirá entrar até que sejam definidos (README, seção de autenticação).")]
    private static partial void LogManagerNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Conta do gestor criada a partir da configuração.")]
    private static partial void LogManagerCreated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message =
        "Senha do gestor redefinida por 'Manager:ResetPassword'. Remova o sinalizador depois de reiniciar.")]
    private static partial void LogManagerPasswordReset(ILogger logger);
}

public static class ManagerAccountSetup
{
    /// <summary>Cria ou redefine a conta do gestor; roda depois da migração e antes de aceitar requisições.</summary>
    public static async Task EnsureManagerAccountAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ManagerAccountSeeder>();
        await seeder.EnsureAsync();
    }
}
