using Npgsql;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>
/// Reconhece, pelo SQLSTATE do PostgreSQL, qual regra de integridade o banco recusou. O banco é quem
/// decide em caso de corrida (duas requisições simultâneas); a API traduz o erro em <c>409</c>.
/// </summary>
public static class PostgresErrors
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string RestrictViolation = "23001";

    /// <summary>Índice único violado: o valor já existe.</summary>
    public static bool IsUniqueViolation(Exception exception) => SqlStateOf(exception) == UniqueViolation;

    /// <summary>Chave estrangeira violada ao inserir ou alterar: a referência não existe.</summary>
    public static bool IsForeignKeyViolation(Exception exception) => SqlStateOf(exception) == ForeignKeyViolation;

    /// <summary>
    /// <c>ON DELETE RESTRICT</c> violado ao excluir: o registro ainda é referenciado. O PostgreSQL usa
    /// um código diferente do da chave estrangeira comum (<c>23001</c>, e não <c>23503</c>).
    /// </summary>
    public static bool IsRestrictViolation(Exception exception) => SqlStateOf(exception) == RestrictViolation;

    /// <summary>Exclusão recusada porque ainda há registros dependentes (restrict ou chave estrangeira).</summary>
    public static bool IsDependentRecordsViolation(Exception exception) =>
        IsRestrictViolation(exception) || IsForeignKeyViolation(exception);

    private static string? SqlStateOf(Exception exception) =>
        (exception as PostgresException ?? exception.InnerException as PostgresException)?.SqlState;
}
