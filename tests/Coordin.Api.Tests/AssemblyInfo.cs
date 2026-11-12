using Coordin.Api.Tests.Infrastructure;

// Um único container PostgreSQL para todo o assembly; as classes de teste e as factories o recebem
// pelo construtor.
[assembly: AssemblyFixture(typeof(PostgresFixture))]
