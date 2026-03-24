using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Eva.Tests.Infrastructure;

internal sealed class TestHttpContextBuilder
{
    private readonly List<Claim> _claims = [];

    public static TestHttpContextBuilder WithAnalista(string email = "analista@metroplan.rs.gov.br")
    {
        return new TestHttpContextBuilder()
            .AddClaim(ClaimTypes.Email, email)
            .AddRole("ANALISTA");
    }

    public static TestHttpContextBuilder WithEmpresa(string empresaCnpj)
    {
        return new TestHttpContextBuilder()
            .AddClaim(ClaimTypes.Email, "empresa@teste.com")
            .AddClaim("EmpresaCnpj", empresaCnpj)
            .AddRole("EMPRESA");
    }

    public TestHttpContextBuilder AddClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public TestHttpContextBuilder AddRole(string role)
    {
        _claims.Add(new Claim(ClaimTypes.Role, role));
        return this;
    }

    public DefaultHttpContext Build()
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(_claims, "TestAuth"))
        };
    }
}
