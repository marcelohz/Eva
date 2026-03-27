using Eva.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Eva.Tests.Infrastructure;

internal sealed class PostgresTestDatabase
{
    private readonly string _connectionString;
    private readonly string _bootstrapSqlPath;

    public PostgresTestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<PostgresTestDatabase>(optional: true)
            .Build();

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não foi configurada para Eva.Tests.");

        _bootstrapSqlPath = Path.Combine(AppContext.BaseDirectory, "Sql", "postgres-bootstrap.sql");
    }

    public async Task EnsureReadyAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = await File.ReadAllTextAsync(_bootstrapSqlPath);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAsync()
    {
        const string resetSql = """
            TRUNCATE TABLE
                eventual.submissao_evento,
                eventual.entidade_documento_atual,
                eventual.submissao_documento,
                eventual.submissao_dados,
                eventual.submissao,
                eventual.documento_viagem,
                eventual.passageiro,
                eventual.viagem,
                eventual.documento,
                web.usuario,
                eventual.motorista,
                geral.veiculo,
                geral.empresa,
                geral.municipio,
                geral.regiao,
                eventual.viagem_tipo,
                web.papel,
                eventual.documento_tipo_vinculo,
                eventual.documento_tipo
            RESTART IDENTITY CASCADE;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var resetCommand = new NpgsqlCommand(resetSql, connection))
        {
            await resetCommand.ExecuteNonQueryAsync();
        }

        const string referenceSql = """
            INSERT INTO web.papel (nome) VALUES
                ('EMPRESA'),
                ('USUARIO_EMPRESA'),
                ('ANALISTA'),
                ('ADMIN');

            INSERT INTO web.usuario (papel_nome, email, nome, senha, ativo, email_validado)
            VALUES ('ANALISTA', 'analista@metroplan.rs.gov.br', 'Analista Teste', 'hash', true, true);

            INSERT INTO eventual.documento_tipo (nome, descricao, obrigatorio, permite_multiplos) VALUES
                ('CARTAO_CNPJ', 'Cartão CNPJ', true, false),
                ('CONTRATO_SOCIAL', 'Contrato Social da Empresa', false, false),
                ('IDENTIDADE_SOCIO', 'Documento de Identidade do Sócio', false, true),
                ('ALVARA', 'Alvará', false, false),
                ('CRLV', 'Certificado de Registro e Licenciamento de Veículo', true, false),
                ('LAUDO_INSPECAO', 'Laudo Inspeção', false, false),
                ('APOLICE_SEGURO', 'Apólice Seguro', false, false),
                ('CNH', 'Carteira Nacional de Habilitação', true, false);

            INSERT INTO eventual.documento_tipo_vinculo (tipo_nome, entidade_tipo) VALUES
                ('CARTAO_CNPJ', 'EMPRESA'),
                ('CONTRATO_SOCIAL', 'EMPRESA'),
                ('IDENTIDADE_SOCIO', 'EMPRESA'),
                ('ALVARA', 'EMPRESA'),
                ('CRLV', 'VEICULO'),
                ('LAUDO_INSPECAO', 'VEICULO'),
                ('APOLICE_SEGURO', 'VEICULO'),
                ('CNH', 'MOTORISTA');
            """;

        await using var referenceCommand = new NpgsqlCommand(referenceSql, connection);
        await referenceCommand.ExecuteNonQueryAsync();
    }

    public EvaDbContext CreateDbContext(DefaultHttpContext? httpContext = null)
    {
        var options = new DbContextOptionsBuilder<EvaDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var accessor = new HttpContextAccessor
        {
            HttpContext = httpContext ?? TestHttpContextBuilder.WithAnalista().Build()
        };

        return new EvaDbContext(options, accessor);
    }
}
