namespace Coordin.Api.Infrastructure.Auth;

/// <summary>
/// Conta do gestor criada na inicialização (DT-29). Os valores vêm de variáveis de ambiente
/// (<c>Manager__Email</c> etc.) ou de <c>dotnet user-secrets</c>; nunca de arquivo versionado.
/// Esta classe não deve ser escrita em log: <c>Password</c> é um segredo.
/// </summary>
public sealed class ManagerOptions
{
    public const string SectionName = "Manager";

    public string? Email { get; init; }

    public string? Password { get; init; }

    /// <summary>Quando verdadeiro, redefine a senha da conta existente a partir de <c>Password</c>.</summary>
    public bool ResetPassword { get; init; }
}
