using System.Security.Claims;
using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Eva.Tests.Services;

[Trait("Category", "unit")]
public class CurrentUserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsync_deve_buscar_usuario_logado_pelo_email()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, "empresa@teste.com")],
                "TestAuth"))
        };

        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(GetCurrentUserAsync_deve_buscar_usuario_logado_pelo_email),
            httpContext);

        context.Usuarios.Add(new Usuario
        {
            Nome = "Empresa Teste",
            Email = "empresa@teste.com",
            EmpresaCnpj = "12345678000199",
            PapelNome = "EMPRESA",
            Ativo = true,
            EmailValidado = true,
            Senha = "hash"
        });
        await context.SaveChangesAsync();

        var service = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });

        var user = await service.GetCurrentUserAsync();

        Assert.NotNull(user);
        Assert.Equal("empresa@teste.com", user!.Email);
        Assert.Equal("12345678000199", user.EmpresaCnpj);
    }

    [Fact]
    public void GetCurrentEmpresaCnpj_deve_retornar_claim_quando_existir()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("EmpresaCnpj", "98765432000155")],
                "TestAuth"))
        };

        using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(GetCurrentEmpresaCnpj_deve_retornar_claim_quando_existir),
            httpContext);

        var service = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal("98765432000155", service.GetCurrentEmpresaCnpj());
    }
}
