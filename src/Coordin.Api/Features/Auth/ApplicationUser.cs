using Microsoft.AspNetCore.Identity;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Usuário do sistema. Existe uma única conta, a do gestor, criada na inicialização a partir da
/// configuração (DT-29). Não há campos extras: o Identity já guarda e-mail, hash da senha e o
/// controle de bloqueio por tentativas (DT-28; RNF-05).
/// </summary>
public sealed class ApplicationUser : IdentityUser;
