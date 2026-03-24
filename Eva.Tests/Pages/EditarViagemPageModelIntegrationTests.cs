using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Pages.Empresa;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Pages;

[Trait("Category", "integration")]
public class EditarViagemPageModelIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OnPostAsync_deve_atualizar_toda_a_viagem_quando_nao_esta_paga()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseAsync(context);
        var viagem = await SeedViagemAsync(context, "12345678000199", pago: false, idaEm: DateTime.UtcNow.AddHours(1));

        var page = CreatePageModel(context, httpContext, viagem.Id);
        page.Input = new NovaViagemVM
        {
            ViagemTipoNome = "ALTERADA",
            NomeContratante = "Novo Contratante",
            CpfCnpjContratante = "98765432100",
            RegiaoCodigo = "RMPOA",
            IdaEm = DateTime.UtcNow.AddDays(1),
            VoltaEm = DateTime.UtcNow.AddDays(1).AddHours(6),
            MunicipioOrigem = "PORTO ALEGRE",
            MunicipioDestino = "CANOAS",
            VeiculoPlaca = "XYZ9Z99",
            MotoristaId = 2,
            Descricao = "Atualizada",
            Passageiros =
            [
                new PassageiroVM { Nome = "Passageiro Novo", Documento = "99999999999" }
            ]
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhasViagens", redirect.PageName);

        var persisted = await context.Viagens.Include(v => v.Passageiros).SingleAsync(v => v.Id == viagem.Id);
        Assert.Equal("Novo Contratante", persisted.NomeContratante);
        Assert.Equal("XYZ9Z99", persisted.VeiculoPlaca);
        Assert.Equal(2, persisted.MotoristaId);
        Assert.Single(persisted.Passageiros);
        Assert.Equal("Passageiro Novo", persisted.Passageiros.Single().Nome);
    }

    [Fact]
    public async Task OnPostAsync_deve_atualizar_apenas_passageiros_quando_viagem_esta_paga_fora_da_janela()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseAsync(context);
        var viagem = await SeedViagemAsync(context, "12345678000199", pago: true, idaEm: DateTime.UtcNow.AddHours(5));

        var page = CreatePageModel(context, httpContext, viagem.Id);
        page.Input = new NovaViagemVM
        {
            ViagemTipoNome = "ALTERADA",
            NomeContratante = "Nao Deve Alterar",
            CpfCnpjContratante = "00000000000",
            RegiaoCodigo = "OUTRA",
            IdaEm = DateTime.UtcNow.AddDays(2),
            VoltaEm = DateTime.UtcNow.AddDays(2).AddHours(3),
            MunicipioOrigem = "CANOAS",
            MunicipioDestino = "PORTO ALEGRE",
            VeiculoPlaca = "XYZ9Z99",
            MotoristaId = 2,
            Descricao = "Nao Deve Alterar",
            Passageiros =
            [
                new PassageiroVM { Nome = "Passageiro Atualizado", Documento = "12312312312" }
            ]
        };

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var persisted = await context.Viagens.Include(v => v.Passageiros).SingleAsync(v => v.Id == viagem.Id);
        Assert.Equal("Contratante Inicial", persisted.NomeContratante);
        Assert.Equal("ABC1D23", persisted.VeiculoPlaca);
        Assert.Equal(1, persisted.MotoristaId);
        Assert.Single(persisted.Passageiros);
        Assert.Equal("Passageiro Atualizado", persisted.Passageiros.Single().Nome);
    }

    [Fact]
    public async Task OnPostAsync_deve_bloquear_edicao_quando_viagem_esta_paga_dentro_da_janela()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseAsync(context);
        var viagem = await SeedViagemAsync(context, "12345678000199", pago: true, idaEm: DateTime.UtcNow.AddMinutes(90));

        var page = CreatePageModel(context, httpContext, viagem.Id);
        page.Input = new NovaViagemVM
        {
            Passageiros =
            [
                new PassageiroVM { Nome = "Bloqueado", Documento = "11111111111" }
            ]
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhasViagens", redirect.PageName);

        var persisted = await context.Viagens.Include(v => v.Passageiros).SingleAsync(v => v.Id == viagem.Id);
        Assert.Equal("Passageiro Inicial", persisted.Passageiros.Single().Nome);
    }

    private static EditarViagemModel CreatePageModel(EvaDbContext context, DefaultHttpContext httpContext, int id)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var managementService = new ViagemManagementService(context);

        return new EditarViagemModel(context, currentUserService, managementService)
        {
            Id = id,
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private static async Task SeedBaseAsync(EvaDbContext context)
    {
        context.Regioes.Add(new Regiao { Codigo = "RMPOA", Nome = "Regiao Metropolitana", Ordem = 1 });
        context.Municipios.AddRange(
            new Municipio { Nome = "PORTO ALEGRE", RegiaoCodigo = "RMPOA" },
            new Municipio { Nome = "CANOAS", RegiaoCodigo = "RMPOA" });
        context.ViagemTipos.AddRange(
            new ViagemTipo { Nome = "EVENTUAL" },
            new ViagemTipo { Nome = "ALTERADA" });
        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        });
        context.Usuarios.Add(new Usuario
        {
            PapelNome = "EMPRESA",
            Email = "empresa@teste.com",
            Nome = "Empresa Teste",
            EmpresaCnpj = "12345678000199",
            Senha = "hash",
            Ativo = true,
            EmailValidado = true
        });
        context.Veiculos.AddRange(
            new Veiculo { Placa = "ABC1D23", EmpresaCnpj = "12345678000199", Modelo = "Onibus" },
            new Veiculo { Placa = "XYZ9Z99", EmpresaCnpj = "12345678000199", Modelo = "Micro" });
        context.Motoristas.AddRange(
            new Motorista { Id = 1, EmpresaCnpj = "12345678000199", Nome = "Motorista Um", Cpf = "11111111111", Cnh = "11111111111" },
            new Motorista { Id = 2, EmpresaCnpj = "12345678000199", Nome = "Motorista Dois", Cpf = "22222222222", Cnh = "22222222222" });

        await context.SaveChangesAsync();
    }

    private static async Task<Viagem> SeedViagemAsync(EvaDbContext context, string empresaCnpj, bool pago, DateTime idaEm)
    {
        var viagem = new Viagem
        {
            EmpresaCnpj = empresaCnpj,
            ViagemTipoNome = "EVENTUAL",
            NomeContratante = "Contratante Inicial",
            CpfCnpjContratante = "12345678900",
            RegiaoCodigo = "RMPOA",
            IdaEm = idaEm,
            VoltaEm = idaEm.AddHours(4),
            MunicipioOrigem = "PORTO ALEGRE",
            MunicipioDestino = "CANOAS",
            VeiculoPlaca = "ABC1D23",
            MotoristaId = 1,
            Descricao = "Descricao inicial",
            Valor = 150m,
            Pago = pago
        };

        viagem.Passageiros.Add(new Passageiro
        {
            Nome = "Passageiro Inicial",
            Cpf = "12345678901"
        });

        context.Viagens.Add(viagem);
        await context.SaveChangesAsync();
        return viagem;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
