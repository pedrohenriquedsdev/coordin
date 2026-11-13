using System.Net;
using System.Text.Json;
using Coordin.Api.Features.Auth;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Auth;

/// <summary>
/// Spec authentication: bloqueio de 15 minutos após 5 tentativas erradas (DT-28). Cada teste usa a
/// sua própria API e o seu próprio banco, porque o estado de bloqueio pertence à conta única.
/// </summary>
public class LockoutTests(PostgresFixture postgres)
{
    private const string WrongPassword = "senha-errada-123456";

    [Fact]
    public async Task FifthWrongAttempt_LocksTheAccount_EvenForTheCorrectPassword()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var wrong = await client.PostLoginAsync(TestManager.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var locked = await client.PostLoginAsync(TestManager.Email, TestManager.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Null(AuthTestClient.SetCookieHeader(locked));
    }

    [Fact]
    public async Task LockedAccount_GetsTheSameResponseAsAWrongPassword()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();
        var wrong = await client.PostLoginAsync(TestManager.Email, WrongPassword);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await client.PostLoginAsync(TestManager.Email, WrongPassword);
        }

        var locked = await client.PostLoginAsync(TestManager.Email, TestManager.Password);

        Assert.Equal(await Describe(wrong), await Describe(locked));
    }

    [Fact]
    public async Task TheLockoutLastsFifteenMinutes()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostLoginAsync(TestManager.Email, WrongPassword);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(TestManager.Email))!;

        Assert.NotNull(user.LockoutEnd);
        Assert.InRange(
            user.LockoutEnd.Value - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(14),
            TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task AfterTheLockoutEnds_TheCorrectPasswordWorksAgain()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostLoginAsync(TestManager.Email, WrongPassword);
        }

        // O Identity mede os 15 minutos com o relógio do sistema; em vez de esperar, o teste
        // empurra o fim do bloqueio para o passado, como se o tempo tivesse passado.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(TestManager.Email))!;
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        var response = await client.PostLoginAsync(TestManager.Email, TestManager.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ASuccessfulLogin_ResetsTheCountOfFailures()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        // 4 + 4 erros seguem sem bloquear porque o acerto no meio zerou a contagem
        // (sem o zero, o oitavo erro já teria bloqueado a conta).
        for (var round = 1; round <= 2; round++)
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                var wrong = await client.PostLoginAsync(TestManager.Email, WrongPassword);
                Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            }

            var ok = await client.PostLoginAsync(TestManager.Email, TestManager.Password);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
    }

    private async Task<TestingApiFactory> NewFactoryAsync()
    {
        var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        return factory;
    }

    private static async Task<string> Describe(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;
        return $"{root.GetProperty("status")}|{root.GetProperty("title")}|{root.GetProperty("detail")}";
    }
}
