using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Workflow;
using System.Linq;
using System.Text.Json;
using Hangfire;

namespace Eva.Services
{
    public class PendenciaService
    {
        private readonly EvaDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobs;

        public PendenciaService(EvaDbContext context, IBackgroundJobClient backgroundJobs)
        {
            _context = context;
            _backgroundJobs = backgroundJobs;
        }

        public async Task SalvarDadosPropostosAsync(string tipo, string id, string dadosPropostos, bool isOverride = false)
        {
            tipo = tipo.ToUpperInvariant();
            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo && p.EntidadeId == id);

            if (atual?.Status == WorkflowValidator.AguardandoAnalise)
            {
                var ticket = await _context.FluxoPendencias
                    .Where(f => f.EntidadeTipo == tipo && f.EntidadeId == id && f.Status == WorkflowValidator.AguardandoAnalise)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync();

                if (ticket != null)
                {
                    ticket.DadosPropostos = dadosPropostos;
                    // A data de criação (CriadoEm) NÃO deve ser alterada aqui para não quebrar 
                    // a ordem temporal e a View v_pendencia_atual do banco de dados.
                    await _context.SaveChangesAsync();
                    return;
                }
            }

            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise, null, null, dadosPropostos, isOverride);
        }

        public async Task AvancarEntidadeAsync(string tipo, string id, bool isOverride = false) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise, null, null, null, isOverride);

        public async Task DevolverParaFilaAsync(string tipo, string id, string analista, bool isOverride = false) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise, analista, null, null, isOverride);

        public async Task IniciarAnaliseAsync(string tipo, string id, string analista, bool isOverride = false) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.EmAnalise, analista, null, null, isOverride);
        public async Task AprovarAsync(string tipo, string id, string analista, bool isOverride = false) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Aprovado, analista, null, null, isOverride);
        public async Task RejeitarAsync(string tipo, string id, string analista, string motivo, bool isOverride = false) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Rejeitado, analista, motivo, null, isOverride);

        private async Task ProcessarTransicaoAsync(string entidadeTipo, string entidadeId, string novoStatus, string? analistaEmail = null, string? motivo = null, string? novosDadosPropostos = null, bool isOverride = false)
        {
            entidadeTipo = entidadeTipo.ToUpperInvariant();
            analistaEmail = analistaEmail?.ToLowerInvariant();

            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            WorkflowValidator.ValidateTransition(atual?.Status, novoStatus, atual?.Analista, analistaEmail, motivo, isOverride);

            if (atual?.Status == novoStatus && novosDadosPropostos == null) return;

            string? dadosPropostosFinais = novosDadosPropostos ?? atual?.DadosPropostos;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var novaPendencia = new FluxoPendencia
                {
                    EntidadeTipo = entidadeTipo,
                    EntidadeId = entidadeId,
                    Status = novoStatus,
                    Analista = analistaEmail,
                    Motivo = motivo,
                    DadosPropostos = dadosPropostosFinais,
                    CriadoEm = DateTime.UtcNow
                };

                _context.FluxoPendencias.Add(novaPendencia);

                if (novoStatus == WorkflowValidator.Aprovado && !string.IsNullOrEmpty(dadosPropostosFinais))
                {
                    await AplicarDadosAprovadosAsync(entidadeTipo, entidadeId, dadosPropostosFinais);
                }

                await _context.SaveChangesAsync();

                if (novoStatus == WorkflowValidator.Aprovado)
                {
                    await LinkApprovedDocumentsAsync(entidadeTipo, entidadeId, novaPendencia.Id);
                }

                await transaction.CommitAsync();

                if (novoStatus == WorkflowValidator.Aprovado)
                {
                    await EnviarEmailNotificacaoAsync(entidadeTipo, entidadeId, novoStatus, null);
                }
                else if (novoStatus == WorkflowValidator.Rejeitado)
                {
                    await EnviarEmailNotificacaoAsync(entidadeTipo, entidadeId, novoStatus, motivo);
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task EnviarEmailNotificacaoAsync(string tipo, string id, string status, string? motivo)
        {
            try
            {
                string? emailDestino = null;
                string nomeEntidade = tipo;

                if (tipo == "EMPRESA")
                {
                    var emp = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == id);
                    emailDestino = emp?.Email;
                    nomeEntidade = $"Empresa {emp?.NomeFantasia ?? id}";
                }
                else if (tipo == "VEICULO")
                {
                    var veic = await _context.Veiculos.IgnoreQueryFilters().Include(v => v.Empresa).FirstOrDefaultAsync(v => v.Placa == id);
                    emailDestino = veic?.Empresa?.Email;
                    nomeEntidade = $"Veículo {id}";
                }
                else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId))
                {
                    var mot = await _context.Motoristas.IgnoreQueryFilters().Include(m => m.Empresa).FirstOrDefaultAsync(m => m.Id == mId);
                    emailDestino = mot?.Empresa?.Email;
                    nomeEntidade = $"Motorista {mot?.Nome ?? id}";
                }

                if (!string.IsNullOrEmpty(emailDestino))
                {
                    string assunto = status == WorkflowValidator.Aprovado
                        ? $"Aprovação de {nomeEntidade}"
                        : $"Pendência em {nomeEntidade}";

                    string corpo = status == WorkflowValidator.Aprovado
                        ? $"<p>O cadastro para <strong>{nomeEntidade}</strong> foi analisado e <strong>Aprovado</strong> pela Metroplan.</p>"
                        : $"<p>O cadastro para <strong>{nomeEntidade}</strong> foi analisado e requer ajustes.</p><p><strong>Motivo apontado:</strong> {motivo}</p><p>Acesse o sistema para regularizar a situação.</p>";

                    _backgroundJobs.Enqueue<IEmailService>(x => x.SendEmailAsync(emailDestino, assunto, corpo));
                }
            }
            catch (Exception ex)
            {
                // Silently trap the error so the workflow transition does not crash the UI
                Console.WriteLine($"Erro ao tentar enfileirar notificação de e-mail: {ex.Message}");
            }
        }

        private async Task AplicarDadosAprovadosAsync(string tipo, string id, string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (tipo == "VEICULO")
            {
                var live = await _context.Veiculos.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Placa == id);
                var draft = JsonSerializer.Deserialize<VeiculoVM>(json, options);
                if (live != null && draft != null)
                {
                    if (draft.ChassiNumero != null) live.ChassiNumero = draft.ChassiNumero;
                    if (draft.Renavan != null) live.Renavan = draft.Renavan;
                    if (draft.Modelo != null) live.Modelo = draft.Modelo;
                    if (draft.PotenciaMotor != null) live.PotenciaMotor = draft.PotenciaMotor;
                    if (draft.NumeroLugares != 0) live.NumeroLugares = draft.NumeroLugares;
                    if (draft.AnoFabricacao != 0) live.AnoFabricacao = draft.AnoFabricacao;
                    if (draft.ModeloAno != 0) live.ModeloAno = draft.ModeloAno;
                    if (draft.VeiculoCombustivelNome != null) live.VeiculoCombustivelNome = draft.VeiculoCombustivelNome;
                }
            }
            else if (tipo == "EMPRESA")
            {
                var live = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == id);
                var draft = JsonSerializer.Deserialize<EmpresaVM>(json, options);
                if (live != null && draft != null)
                {
                    if (draft.Nome != null) live.Nome = draft.Nome;
                    if (draft.NomeFantasia != null) live.NomeFantasia = draft.NomeFantasia;
                    if (draft.Email != null) live.Email = draft.Email;
                    if (draft.Telefone != null) live.Telefone = draft.Telefone;
                    if (draft.Endereco != null) live.Endereco = draft.Endereco;
                    if (draft.Cep != null) live.Cep = draft.Cep;
                    if (draft.EnderecoNumero != null) live.EnderecoNumero = draft.EnderecoNumero;
                    if (draft.EnderecoComplemento != null) live.EnderecoComplemento = draft.EnderecoComplemento;
                    if (draft.Bairro != null) live.Bairro = draft.Bairro;
                    if (draft.Cidade != null) live.Cidade = draft.Cidade;
                    if (draft.Estado != null) live.Estado = draft.Estado;
                }
            }
            else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId))
            {
                var live = await _context.Motoristas.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == mId);
                var draft = JsonSerializer.Deserialize<Motorista>(json, options);
                if (live != null && draft != null)
                {
                    if (draft.Cpf != null) live.Cpf = draft.Cpf;
                    if (draft.Cnh != null) live.Cnh = draft.Cnh;
                    if (draft.Nome != null) live.Nome = draft.Nome;
                    if (draft.Email != null) live.Email = draft.Email;
                }
            }
        }

        private async Task LinkApprovedDocumentsAsync(string tipo, string id, int fluxoId)
        {
            IQueryable<Documento> query = _context.Documentos.Where(d => d.FluxoPendenciaId == null);

            if (tipo == "EMPRESA")
                query = query.Where(d => _context.DocumentoEmpresas.Any(de => de.Id == d.Id && de.EmpresaCnpj == id));
            else if (tipo == "VEICULO")
                query = query.Where(d => _context.DocumentoVeiculos.Any(dv => dv.Id == d.Id && dv.VeiculoPlaca == id));
            else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId))
                query = query.Where(d => _context.DocumentoMotoristas.Any(dm => dm.Id == d.Id && dm.MotoristaId == mId));
            else
                return;

            var allUnlinkedDocs = await query.ToListAsync();

            var docsToApprove = allUnlinkedDocs
                .GroupBy(d => d.DocumentoTipoNome)
                .Select(g => g.OrderByDescending(d => d.DataUpload).ThenByDescending(d => d.Id).First())
                .ToList();

            foreach (var doc in docsToApprove)
            {
                doc.FluxoPendenciaId = fluxoId;
                doc.AprovadoEm = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<FluxoPendencia>> GetHistoricoAsync(string tipo, string id) => await _context.FluxoPendencias.Where(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id).OrderByDescending(p => p.CriadoEm).ToListAsync();
        public async Task<string?> GetStatusAsync(string tipo, string id) => (await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id))?.Status;
    }
}