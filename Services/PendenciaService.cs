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

            if (await DadosPropostosSemAlteracaoAsync(tipo, id, dadosPropostos, atual))
            {
                return;
            }

            var statusAlvo = await DeterminarStatusSubmissaoAsync(tipo, id);

            if ((statusAlvo == WorkflowValidator.AguardandoAnalise || statusAlvo == WorkflowValidator.Incompleto) &&
                atual?.Status == statusAlvo)
            {
                var ticket = await _context.FluxoPendencias
                    .Where(f => f.EntidadeTipo == tipo && f.EntidadeId == id && f.Status == statusAlvo)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync();

                if (ticket != null)
                {
                    ticket.DadosPropostos = dadosPropostos;
                    await _context.SaveChangesAsync();
                    return;
                }
            }

            await ProcessarTransicaoAsync(
                tipo,
                id,
                statusAlvo,
                null,
                null,
                dadosPropostos,
                isOverride,
                preserveCurrentDraft: false);
        }

        private async Task<bool> DadosPropostosSemAlteracaoAsync(
            string tipo,
            string id,
            string novosDadosPropostos,
            VPendenciaAtual? atual)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (tipo == "EMPRESA")
            {
                var novo = JsonSerializer.Deserialize<EmpresaVM>(novosDadosPropostos, options);
                if (novo == null) return false;

                if (!string.IsNullOrWhiteSpace(atual?.DadosPropostos))
                {
                    var draftAtual = JsonSerializer.Deserialize<EmpresaVM>(atual.DadosPropostos, options);
                    return draftAtual != null && EmpresaEhEquivalente(novo, draftAtual);
                }

                var atualDb = await BuscarEmpresaComoVmAsync(id);
                return atualDb != null && EmpresaEhEquivalente(novo, atualDb);
            }

            if (tipo == "VEICULO")
            {
                var novo = JsonSerializer.Deserialize<VeiculoVM>(novosDadosPropostos, options);
                if (novo == null) return false;

                if (!string.IsNullOrWhiteSpace(atual?.DadosPropostos))
                {
                    var draftAtual = JsonSerializer.Deserialize<VeiculoVM>(atual.DadosPropostos, options);
                    return draftAtual != null && VeiculoEhEquivalente(novo, draftAtual);
                }

                var atualDb = await BuscarVeiculoComoVmAsync(id);
                return atualDb != null && VeiculoEhEquivalente(novo, atualDb);
            }

            if (tipo == "MOTORISTA")
            {
                var novo = JsonSerializer.Deserialize<MotoristaVM>(novosDadosPropostos, options);
                if (novo == null) return false;

                if (!string.IsNullOrWhiteSpace(atual?.DadosPropostos))
                {
                    var draftAtual = JsonSerializer.Deserialize<MotoristaVM>(atual.DadosPropostos, options);
                    return draftAtual != null && MotoristaEhEquivalente(novo, draftAtual);
                }

                var atualDb = await BuscarMotoristaComoVmAsync(id);
                return atualDb != null && MotoristaEhEquivalente(novo, atualDb);
            }

            return false;
        }

        public async Task AvancarEntidadeAsync(string tipo, string id, bool isOverride = false)
        {
            tipo = tipo.ToUpperInvariant();
            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo && p.EntidadeId == id);
            var statusAlvo = await DeterminarStatusSubmissaoAsync(tipo, id);

            if (atual?.Status == statusAlvo) return;

            await ProcessarTransicaoAsync(
                tipo,
                id,
                statusAlvo,
                null,
                null,
                null,
                isOverride,
                preserveCurrentDraft: DevePreservarDraftEmSincronizacao(atual?.Status));
        }

        public async Task DevolverParaFilaAsync(string tipo, string id, string analista, bool isOverride = false) =>
            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise, analista, null, null, isOverride);

        public async Task IniciarAnaliseAsync(string tipo, string id, string analista, bool isOverride = false) =>
            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.EmAnalise, analista, null, null, isOverride);

        public async Task AprovarAsync(string tipo, string id, string analista, bool isOverride = false) =>
            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Aprovado, analista, null, null, isOverride);

        public async Task RejeitarAsync(string tipo, string id, string analista, string motivo, bool isOverride = false) =>
            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Rejeitado, analista, motivo, null, isOverride);

        private async Task<string> DeterminarStatusSubmissaoAsync(string tipo, string id) =>
            await IsSubmissionReadyAsync(tipo, id)
                ? WorkflowValidator.AguardandoAnalise
                : WorkflowValidator.Incompleto;

        private static bool DevePreservarDraftEmSincronizacao(string? statusAtual) =>
            statusAtual == WorkflowValidator.Incompleto ||
            statusAtual == WorkflowValidator.AguardandoAnalise ||
            statusAtual == WorkflowValidator.Rejeitado;

        private async Task<bool> IsSubmissionReadyAsync(string entidadeTipo, string entidadeId)
        {
            entidadeTipo = entidadeTipo.ToUpperInvariant();

            var tiposObrigatorios = await _context.DocumentoTipoVinculos
                .Include(v => v.DocumentoTipo)
                .Where(v => v.EntidadeTipo.Trim().ToUpper() == entidadeTipo &&
                            v.DocumentoTipo != null &&
                            v.DocumentoTipo.Obrigatorio)
                .Select(v => v.TipoNome)
                .ToListAsync();

            if (!tiposObrigatorios.Any())
            {
                return true;
            }

            var documentos = await BuscarDocumentosDaEntidadeAsync(entidadeTipo, entidadeId);
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var tipoObrigatorio in tiposObrigatorios)
            {
                var documentoMaisRecente = documentos
                    .Where(d => d.DocumentoTipoNome == tipoObrigatorio)
                    .OrderByDescending(d => d.DataUpload)
                    .ThenByDescending(d => d.Id)
                    .FirstOrDefault();

                if (documentoMaisRecente == null)
                {
                    return false;
                }

                if (documentoMaisRecente.Validade.HasValue && documentoMaisRecente.Validade.Value < hoje)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<List<Documento>> BuscarDocumentosDaEntidadeAsync(string entidadeTipo, string entidadeId)
        {
            if (entidadeTipo == "EMPRESA")
            {
                return await _context.DocumentoEmpresas
                    .Where(de => de.EmpresaCnpj == entidadeId && de.Documento != null)
                    .Select(de => de.Documento!)
                    .ToListAsync();
            }

            if (entidadeTipo == "VEICULO")
            {
                return await _context.DocumentoVeiculos
                    .Where(dv => dv.VeiculoPlaca == entidadeId && dv.Documento != null)
                    .Select(dv => dv.Documento!)
                    .ToListAsync();
            }

            if (entidadeTipo == "MOTORISTA" && int.TryParse(entidadeId, out var motoristaId))
            {
                return await _context.DocumentoMotoristas
                    .Where(dm => dm.MotoristaId == motoristaId && dm.Documento != null)
                    .Select(dm => dm.Documento!)
                    .ToListAsync();
            }

            return new List<Documento>();
        }

        private async Task<EmpresaVM?> BuscarEmpresaComoVmAsync(string id)
        {
            var empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == id);
            if (empresa == null) return null;

            return new EmpresaVM
            {
                Cnpj = empresa.Cnpj,
                Nome = empresa.Nome,
                NomeFantasia = empresa.NomeFantasia,
                Endereco = empresa.Endereco,
                EnderecoNumero = empresa.EnderecoNumero,
                EnderecoComplemento = empresa.EnderecoComplemento,
                Bairro = empresa.Bairro,
                Cidade = empresa.Cidade,
                Estado = empresa.Estado,
                Cep = empresa.Cep,
                Email = empresa.Email,
                Telefone = empresa.Telefone
            };
        }

        private async Task<VeiculoVM?> BuscarVeiculoComoVmAsync(string id)
        {
            var veiculo = await _context.Veiculos.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Placa == id);
            if (veiculo == null) return null;

            return new VeiculoVM
            {
                Placa = veiculo.Placa,
                Modelo = veiculo.Modelo ?? string.Empty,
                ChassiNumero = veiculo.ChassiNumero,
                Renavan = veiculo.Renavan,
                PotenciaMotor = veiculo.PotenciaMotor,
                VeiculoCombustivelNome = veiculo.VeiculoCombustivelNome,
                NumeroLugares = veiculo.NumeroLugares,
                AnoFabricacao = veiculo.AnoFabricacao,
                ModeloAno = veiculo.ModeloAno
            };
        }

        private async Task<MotoristaVM?> BuscarMotoristaComoVmAsync(string id)
        {
            if (!int.TryParse(id, out var motoristaId))
            {
                return null;
            }

            var motorista = await _context.Motoristas.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == motoristaId);
            if (motorista == null) return null;

            return new MotoristaVM
            {
                Id = motorista.Id,
                Nome = motorista.Nome,
                Cpf = motorista.Cpf,
                Cnh = motorista.Cnh,
                Email = motorista.Email
            };
        }

        private static bool EmpresaEhEquivalente(EmpresaVM left, EmpresaVM right)
        {
            return StringsIguais(left.Cnpj, right.Cnpj) &&
                   StringsIguais(left.Nome, right.Nome) &&
                   StringsIguais(left.NomeFantasia, right.NomeFantasia) &&
                   StringsIguais(left.Endereco, right.Endereco) &&
                   StringsIguais(left.EnderecoNumero, right.EnderecoNumero) &&
                   StringsIguais(left.EnderecoComplemento, right.EnderecoComplemento) &&
                   StringsIguais(left.Bairro, right.Bairro) &&
                   StringsIguais(left.Cidade, right.Cidade) &&
                   StringsIguais(left.Estado, right.Estado) &&
                   StringsIguais(left.Cep, right.Cep) &&
                   StringsIguais(left.Email, right.Email) &&
                   StringsIguais(left.Telefone, right.Telefone);
        }

        private static bool VeiculoEhEquivalente(VeiculoVM left, VeiculoVM right)
        {
            return StringsIguais(left.Placa, right.Placa) &&
                   StringsIguais(left.Modelo, right.Modelo) &&
                   StringsIguais(left.ChassiNumero, right.ChassiNumero) &&
                   StringsIguais(left.Renavan, right.Renavan) &&
                   left.PotenciaMotor == right.PotenciaMotor &&
                   StringsIguais(left.VeiculoCombustivelNome, right.VeiculoCombustivelNome) &&
                   left.NumeroLugares == right.NumeroLugares &&
                   left.AnoFabricacao == right.AnoFabricacao &&
                   left.ModeloAno == right.ModeloAno;
        }

        private static bool MotoristaEhEquivalente(MotoristaVM left, MotoristaVM right)
        {
            return left.Id == right.Id &&
                   StringsIguais(left.Nome, right.Nome) &&
                   StringsIguais(left.Cpf, right.Cpf) &&
                   StringsIguais(left.Cnh, right.Cnh) &&
                   StringsIguais(left.Email, right.Email);
        }

        private static bool StringsIguais(string? left, string? right)
        {
            return string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.Ordinal);
        }

        private async Task ProcessarTransicaoAsync(
            string entidadeTipo,
            string entidadeId,
            string novoStatus,
            string? analistaEmail = null,
            string? motivo = null,
            string? novosDadosPropostos = null,
            bool isOverride = false,
            bool preserveCurrentDraft = true)
        {
            entidadeTipo = entidadeTipo.ToUpperInvariant();
            analistaEmail = analistaEmail?.ToLowerInvariant();

            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            WorkflowValidator.ValidateTransition(atual?.Status, novoStatus, atual?.Analista, analistaEmail, motivo, isOverride);

            if (atual?.Status == novoStatus && novosDadosPropostos == null) return;

            string? dadosPropostosFinais = preserveCurrentDraft
                ? novosDadosPropostos ?? atual?.DadosPropostos
                : novosDadosPropostos;

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

                // Links documents to the new workflow record only when the item is really submitted
                if (novoStatus == WorkflowValidator.Aprovado ||
                    novoStatus == WorkflowValidator.Rejeitado ||
                    novoStatus == WorkflowValidator.AguardandoAnalise)
                {
                    await VincularDocumentosAoFluxoAsync(entidadeTipo, entidadeId, novaPendencia.Id, novoStatus == WorkflowValidator.Aprovado);
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

        private async Task VincularDocumentosAoFluxoAsync(string tipo, string id, int fluxoId, bool marcarComoAprovado)
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

            var docsToProcess = allUnlinkedDocs
                .GroupBy(d => d.DocumentoTipoNome)
                .Select(g => g.OrderByDescending(d => d.DataUpload).ThenByDescending(d => d.Id).First())
                .ToList();

            foreach (var doc in docsToProcess)
            {
                doc.FluxoPendenciaId = fluxoId;
                if (marcarComoAprovado)
                {
                    doc.AprovadoEm = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<FluxoPendencia>> GetHistoricoAsync(string tipo, string id) =>
            await _context.FluxoPendencias
                .Where(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id)
                .OrderByDescending(p => p.CriadoEm)
                .ToListAsync();

        public async Task<string?> GetStatusAsync(string tipo, string id) =>
            (await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id))?.Status;
    }
}
