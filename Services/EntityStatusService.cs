using Eva.Data;
using Eva.Models;
using Eva.Workflow;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Services
{
    public interface IEntityStatusService
    {
        Task<EntityHealthReport> GetHealthAsync(string entityType, string entityId);
        Task<Dictionary<string, EntityHealthReport>> GetBulkHealthAsync(string entityType, IEnumerable<string> entityIds);
    }

    public class EntityStatusService : IEntityStatusService
    {
        private readonly EvaDbContext _context;

        public EntityStatusService(EvaDbContext context)
        {
            _context = context;
        }

        public async Task<EntityHealthReport> GetHealthAsync(string entityType, string entityId)
        {
            var bulkResult = await GetBulkHealthAsync(entityType, new[] { entityId });
            return bulkResult.TryGetValue(entityId, out var report) ? report : new EntityHealthReport();
        }

        public async Task<Dictionary<string, EntityHealthReport>> GetBulkHealthAsync(string entityType, IEnumerable<string> entityIds)
        {
            var ids = entityIds.Distinct().ToList();
            var results = new Dictionary<string, EntityHealthReport>();

            if (!ids.Any()) return results;

            var entityTypeSafe = entityType.Trim().ToUpper();

            // 1. Fetch Mandatory Document Types for this Entity Type via Vinculos
            var mandatoryTypes = await _context.DocumentoTipoVinculos
                .Include(v => v.DocumentoTipo)
                .Where(v => v.EntidadeTipo.Trim().ToUpper() == entityTypeSafe && v.DocumentoTipo != null && v.DocumentoTipo.Obrigatorio)
                .Select(v => v.TipoNome)
                .ToListAsync();

            var latestSubmissions = await _context.Submissoes
                .Where(s => s.EntidadeTipo == entityTypeSafe && ids.Contains(s.EntidadeId))
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var latestSubmissionByEntity = latestSubmissions
                .GroupBy(s => s.EntidadeId)
                .ToDictionary(g => g.Key, g => g.First());

            var latestSubmissionIds = latestSubmissionByEntity.Values.Select(s => s.Id).ToList();

            var latestSubmissionData = latestSubmissionIds.Any()
                ? await _context.SubmissaoDados
                    .Where(sd => latestSubmissionIds.Contains(sd.SubmissaoId))
                    .ToDictionaryAsync(sd => sd.SubmissaoId, sd => sd)
                : new Dictionary<int, SubmissaoDados>();

            var latestSubmissionDocs = latestSubmissionIds.Any()
                ? await _context.SubmissaoDocumentos
                    .Where(sd => latestSubmissionIds.Contains(sd.SubmissaoId) && sd.AtivoNaSubmissao)
                    .ToListAsync()
                : new List<SubmissaoDocumento>();

            var submissionDocsBySubmission = latestSubmissionDocs
                .GroupBy(sd => sd.SubmissaoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var currentDocs = await FetchCurrentAcceptedDocumentsAsync(entityTypeSafe, ids);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var id in ids)
            {
                latestSubmissionByEntity.TryGetValue(id, out var latestSubmission);
                latestSubmissionData.TryGetValue(latestSubmission?.Id ?? 0, out var latestDados);
                submissionDocsBySubmission.TryGetValue(latestSubmission?.Id ?? 0, out var latestDocsForEntity);

                var docsForEntity = currentDocs.TryGetValue(id, out var acceptedDocs) && acceptedDocs.Any()
                    ? acceptedDocs
                    : new List<Documento>();

                var report = new EntityHealthReport
                {
                    LatestSubmissionStatus = MapSubmissionStatus(latestSubmission?.Status),
                    LastRejectionReason = BuildLatestRejectionReason(latestSubmission, latestDados, latestDocsForEntity)
                };

                foreach (var reqType in mandatoryTypes)
                {
                    var doc = docsForEntity
                        .Where(d => d.DocumentoTipoNome == reqType)
                        .OrderByDescending(d => d.DataUpload)
                        .FirstOrDefault();

                    if (doc == null)
                    {
                        report.MissingMandatoryDocs.Add(reqType);
                    }
                    else
                    {
                        if (doc.Validade.HasValue && doc.Validade.Value < today)
                        {
                            report.ExpiredDocs.Add(reqType);
                        }

                    }
                }

                report.PendingDocs = BuildPendingRequiredDocs(mandatoryTypes, latestSubmission, latestDocsForEntity);

                report.IsLegal = !report.MissingMandatoryDocs.Any() && !report.ExpiredDocs.Any();
                report.AnalystStatus = report.IsLegal ? WorkflowStatus.Aprovado : WorkflowStatus.Incompleto;
                report.CurrentStatus = BuildCurrentStatus(report.IsLegal, latestSubmission);

                results[id] = report;
            }

            return results;
        }

        private static string BuildCurrentStatus(bool isLegal, Submissao? latestSubmission)
        {
            var mappedSubmissionStatus = MapSubmissionStatus(latestSubmission?.Status);
            if (mappedSubmissionStatus == WorkflowStatus.Rejeitado)
            {
                return WorkflowStatus.Rejeitado;
            }

            if (WorkflowStatus.IsPending(mappedSubmissionStatus))
            {
                return mappedSubmissionStatus!;
            }

            return isLegal ? WorkflowStatus.Aprovado : WorkflowStatus.Incompleto;
        }

        private static string? MapSubmissionStatus(string? status) => status switch
        {
            SubmissaoWorkflow.AguardandoAnalise => WorkflowStatus.AguardandoAnalise,
            SubmissaoWorkflow.EmAnalise => WorkflowStatus.EmAnalise,
            SubmissaoWorkflow.Rejeitada => WorkflowStatus.Rejeitado,
            SubmissaoWorkflow.Aprovada => WorkflowStatus.Aprovado,
            _ => WorkflowStatus.Incompleto
        };

        private static string? BuildLatestRejectionReason(Submissao? latestSubmission, SubmissaoDados? dados, List<SubmissaoDocumento>? docs)
        {
            if (latestSubmission?.Status != SubmissaoWorkflow.Rejeitada)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(latestSubmission.ObservacaoAnalista))
            {
                return latestSubmission.ObservacaoAnalista;
            }

            if (dados?.StatusRevisao == SubmissaoWorkflow.RevisaoRejeitada && !string.IsNullOrWhiteSpace(dados.MotivoRejeicao))
            {
                return dados.MotivoRejeicao;
            }

            return docs?
                .Where(d => d.StatusRevisao == SubmissaoWorkflow.RevisaoRejeitada && !string.IsNullOrWhiteSpace(d.MotivoRejeicao))
                .OrderBy(d => d.Id)
                .Select(d => d.MotivoRejeicao)
                .FirstOrDefault();
        }

        private static List<string> BuildPendingRequiredDocs(List<string> mandatoryTypes, Submissao? latestSubmission, List<SubmissaoDocumento>? docs)
        {
            if (latestSubmission == null || !SubmissaoWorkflow.EstaBloqueadaParaEmpresa(latestSubmission.Status) || docs == null)
            {
                return new List<string>();
            }

            return mandatoryTypes
                .Where(tipo =>
                {
                    var docsDoTipo = docs.Where(d => d.DocumentoTipoNome == tipo).ToList();
                    return docsDoTipo.Any() && docsDoTipo.Any(d => d.StatusRevisao == SubmissaoWorkflow.RevisaoPendente);
                })
                .Distinct()
                .ToList();
        }

        private async Task<Dictionary<string, List<Documento>>> FetchCurrentAcceptedDocumentsAsync(string entityType, List<string> ids)
        {
            var rows = await _context.EntidadeDocumentosAtuais
                .Where(eda => eda.EntidadeTipo == entityType && ids.Contains(eda.EntidadeId))
                .ToListAsync();

            if (!rows.Any())
            {
                return new Dictionary<string, List<Documento>>();
            }

            var docIds = rows.Select(r => r.DocumentoId).Distinct().ToList();
            var documentos = await _context.Documentos
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d);

            return rows
                .Where(r => documentos.ContainsKey(r.DocumentoId))
                .GroupBy(r => r.EntidadeId)
                .ToDictionary(g => g.Key, g => g.Select(r => documentos[r.DocumentoId]).ToList());
        }

    }
}
