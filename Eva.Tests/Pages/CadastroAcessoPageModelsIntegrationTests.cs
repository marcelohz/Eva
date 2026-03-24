using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Pages;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Eva.Tests.Pages;

[Trait("Category", "integration")]
public class CadastroAcessoPageModelsIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CadastroEmpresa_OnPostAsync_deve_retornar_page_quando_turnstile_falha()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        SetForm(httpContext, new Dictionary<string, StringValues>
        {
            ["cf-turnstile-response"] = "token-invalido"
        });

        await using var context = _database.CreateDbContext(httpContext);
        var jobs = new RecordingBackgroundJobClient();
        var page = CreateCadastroEmpresaModel(context, httpContext, new FakeTurnstileService(false), jobs);
        page.Input = new CadastroEmpresaVM
        {
            Cnpj = "12.345.678/0001-99",
            Email = "empresa@teste.com"
        };

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.DoesNotContain(await context.Usuarios.IgnoreQueryFilters().ToListAsync(), u => u.Email == "empresa@teste.com");
        Assert.Empty(jobs.EnqueuedJobs);
    }

    [Fact]
    public async Task CadastroEmpresa_OnPostAsync_deve_criar_usuario_token_e_redirecionar_quando_cadastro_eh_valido()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        SetForm(httpContext, new Dictionary<string, StringValues>
        {
            ["cf-turnstile-response"] = "token-valido"
        });

        await using var context = _database.CreateDbContext(httpContext);
        var jobs = new RecordingBackgroundJobClient();
        var page = CreateCadastroEmpresaModel(context, httpContext, new FakeTurnstileService(true), jobs);
        page.Input = new CadastroEmpresaVM
        {
            Cnpj = "12.345.678/0001-99",
            Email = "empresa@teste.com"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/CadastroEmpresaSucesso", redirect.PageName);
        Assert.Equal("empresa@teste.com", redirect.RouteValues!["email"]);

        var empresa = await context.Empresas.IgnoreQueryFilters().SingleAsync(e => e.Cnpj == "12345678000199");
        var usuario = await context.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Email == "empresa@teste.com");
        var token = await context.TokensValidacaoEmail.IgnoreQueryFilters().SingleAsync(t => t.UsuarioId == usuario.Id);

        Assert.Equal("empresa@teste.com", empresa.Email);
        Assert.False(usuario.EmailValidado);
        Assert.True(usuario.Ativo);
        Assert.NotEmpty(token.Token);
        Assert.Single(jobs.EnqueuedJobs);
        Assert.Equal("SendEmailAsync", jobs.EnqueuedJobs[0].Method.Name);
        Assert.Equal("empresa@teste.com", jobs.EnqueuedJobs[0].Args[0]);
        Assert.Equal("Ativação de Conta - Fretamento Eventual", jobs.EnqueuedJobs[0].Args[1]);
        Assert.Contains("Confirmar E-mail", jobs.EnqueuedJobs[0].Args[2]?.ToString());
    }

    [Fact]
    public async Task CadastroEmpresa_OnPostAsync_deve_retornar_page_quando_email_nao_confere_com_empresa_existente()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        SetForm(httpContext, new Dictionary<string, StringValues>
        {
            ["cf-turnstile-response"] = "token-valido"
        });

        await using var context = _database.CreateDbContext(httpContext);
        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Email = "outro@teste.com",
            NomeFantasia = "Empresa Existente",
            Nome = "Empresa Existente"
        });
        await context.SaveChangesAsync();

        var page = CreateCadastroEmpresaModel(context, httpContext, new FakeTurnstileService(true), new RecordingBackgroundJobClient());
        page.Input = new CadastroEmpresaVM
        {
            Cnpj = "12.345.678/0001-99",
            Email = "empresa@teste.com"
        };

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.DoesNotContain(await context.Usuarios.IgnoreQueryFilters().ToListAsync(), u => u.Email == "empresa@teste.com");
        Assert.True(page.ModelState.ContainsKey("Input.Email"));
    }

    [Fact]
    public async Task ConfirmarAcesso_OnGetAsync_deve_redirecionar_para_login_quando_token_expirou()
    {
        await using var context = _database.CreateDbContext();
        var page = CreateConfirmarAcessoModel(context, new DefaultHttpContext());

        var result = await page.OnGetAsync("token-inexistente");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Login", redirect.PageName);
    }

    [Fact]
    public async Task ConfirmarAcesso_OnPostAsync_deve_confirmar_usuario_remover_token_e_redirecionar_para_login()
    {
        await using var context = _database.CreateDbContext();

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        });
        await context.SaveChangesAsync();

        var usuario = new Usuario
        {
            PapelNome = "EMPRESA",
            Email = "empresa@teste.com",
            Nome = "Empresa Teste",
            EmpresaCnpj = "12345678000199",
            Senha = "temporaria",
            Ativo = true,
            EmailValidado = false
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        context.TokensValidacaoEmail.Add(new TokenValidacaoEmail
        {
            UsuarioId = usuario.Id,
            Token = "token-valido",
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();

        var page = CreateConfirmarAcessoModel(context, new DefaultHttpContext());
        page.Token = "token-valido";
        page.Input = new ConfirmarAcessoModel.ConfirmarSenhaVM
        {
            Senha = "senha-super-segura",
            ConfirmarSenha = "senha-super-segura"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Login", redirect.PageName);

        var usuarioAtualizado = await context.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == usuario.Id);
        var tokenRemovido = await context.TokensValidacaoEmail.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.UsuarioId == usuario.Id);

        Assert.True(usuarioAtualizado.EmailValidado);
        Assert.True(usuarioAtualizado.Ativo);
        Assert.NotEqual("temporaria", usuarioAtualizado.Senha);
        Assert.Null(tokenRemovido);
    }

    private static CadastroEmpresaModel CreateCadastroEmpresaModel(
        Eva.Data.EvaDbContext context,
        DefaultHttpContext httpContext,
        ITurnstileService turnstileService,
        RecordingBackgroundJobClient jobs)
    {
        var page = new CadastroEmpresaModel(context, turnstileService, jobs);
        InitializePageModel(page, httpContext);
        page.Url = new FakeUrlHelper();
        return page;
    }

    private static ConfirmarAcessoModel CreateConfirmarAcessoModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var page = new ConfirmarAcessoModel(context);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static void InitializePageModel(PageModel page, DefaultHttpContext httpContext)
    {
        var pageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };

        page.PageContext = pageContext;
        page.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
    }

    private static void SetForm(DefaultHttpContext httpContext, Dictionary<string, StringValues> values)
    {
        httpContext.Features.Set<IFormFeature>(new FormFeature(new FormCollection(values)));
    }

    private sealed class FakeTurnstileService : ITurnstileService
    {
        private readonly bool _result;

        public FakeTurnstileService(bool result)
        {
            _result = result;
        }

        public Task<bool> VerifyTokenAsync(string token) => Task.FromResult(_result);
    }

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> EnqueuedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            EnqueuedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
        public bool Delete(string jobId) => true;

        public string Enqueue(Job job)
        {
            EnqueuedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Job job, TimeSpan delay) => Guid.NewGuid().ToString();
        public string Schedule(Job job, DateTimeOffset enqueueAt) => Guid.NewGuid().ToString();
        public string ContinueJobWith(string parentId, Job job) => Guid.NewGuid().ToString();
        public string ContinueJobWith(string parentId, Job job, IState nextState) => Guid.NewGuid().ToString();
        public string ContinueJobWith(string parentId, string queue, Job job, IState nextState) => Guid.NewGuid().ToString();
        public string Requeue(string jobId) => jobId;
    }

    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext => new(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        public string? Action(UrlActionContext actionContext) => "https://teste.local/action";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "https://teste.local/link";
        public string? RouteUrl(UrlRouteContext routeContext) => "https://teste.local/rota";
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
