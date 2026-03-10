using Eva.Data;
using Eva.Models;
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

            // 1. Fetch Mandatory Document Types for this Entity Type
            var mandatoryTypes = await _context.DocumentoTipoPermissoes
                .Include(p => p.DocumentoTipo)
                .Where(p => p.EntidadeTipo.ToUpper() == entityType.ToUpper() && p.DocumentoTipo!.Obrigatorio)
                .Select(p => p.TipoNome)
                .ToListAsync();

            // 2. Fetch Latest Decisive Statuses (APROVADO or REJEITADO) for the requested entities
            var decisiveStatuses = await _context.FluxoPendencias
                .Where(f => f.EntidadeTipo == entityType.ToUpper() && ids.Contains(f.EntidadeId) &&
                            (f.Status == "APROVADO" || f.Status == "REJEITADO"))
                .GroupBy(f => f.EntidadeId)
                .Select(g => g.OrderByDescending(f => f.CriadoEm).First())
                .ToDictionaryAsync(f => f.EntidadeId, f => f.Status);

            // 2.5 NEW: Fetch Absolute Latest Statuses (To fix the "Incompleto" bug on new entities)
            var currentStatuses = await _context.VPendenciasAtuais
                .Where(p => p.EntidadeTipo == entityType.ToUpper() && ids.Contains(p.EntidadeId))
                .ToDictionaryAsync(p => p.EntidadeId, p => p.Status);

            // 3. Fetch Entity Documents based on Entity Type
            var entityDocs = await FetchDocumentsForEntitiesAsync(entityType.ToUpper(), ids);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 4. Calculate Health for each Entity
            foreach (var id in ids)
            {
                var report = new EntityHealthReport();

                // Set Analyst Status (Defaults to INCOMPLETO if no decisive record is found)
                report.AnalystStatus = decisiveStatuses.TryGetValue(id, out var status) ? status : "INCOMPLETO";

                // Set Current Status
                report.CurrentStatus = currentStatuses.TryGetValue(id, out var currStatus) ? currStatus : "INCOMPLETO";

                var docsForEntity = entityDocs.TryGetValue(id, out var docs) ? docs : new List<Documento>();

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

                        if (doc.AprovadoEm == null)
                        {
                            report.PendingDocs.Add(reqType);
                        }
                    }
                }

                // The Master Switch: Only legal if previously APROVADO and no missing/expired docs
                report.IsLegal = report.AnalystStatus == "APROVADO" &&
                                 !report.MissingMandatoryDocs.Any() &&
                                 !report.ExpiredDocs.Any();

                results[id] = report;
            }

            return results;
        }

        private async Task<Dictionary<string, List<Documento>>> FetchDocumentsForEntitiesAsync(string entityType, List<string> ids)
        {
            var dictionary = new Dictionary<string, List<Documento>>();

            switch (entityType)
            {
                case "EMPRESA":
                    var empDocs = await _context.DocumentoEmpresas
                        .Include(de => de.Documento)
                        .Where(de => ids.Contains(de.EmpresaCnpj))
                        .ToListAsync();
                    dictionary = empDocs.GroupBy(de => de.EmpresaCnpj)
                                        .ToDictionary(g => g.Key, g => g.Select(de => de.Documento!).ToList());
                    break;

                case "VEICULO":
                    var veiDocs = await _context.DocumentoVeiculos
                        .Include(dv => dv.Documento)
                        .Where(dv => ids.Contains(dv.VeiculoPlaca))
                        .ToListAsync();
                    dictionary = veiDocs.GroupBy(dv => dv.VeiculoPlaca)
                                        .ToDictionary(g => g.Key, g => g.Select(dv => dv.Documento!).ToList());
                    break;

                case "MOTORISTA":
                    var intIds = ids.Select(id => int.TryParse(id, out var val) ? val : 0).Where(val => val != 0).ToList();
                    var motDocs = await _context.DocumentoMotoristas
                        .Include(dm => dm.Documento)
                        .Where(dm => intIds.Contains(dm.MotoristaId))
                        .ToListAsync();
                    dictionary = motDocs.GroupBy(dm => dm.MotoristaId.ToString())
                                        .ToDictionary(g => g.Key, g => g.Select(dm => dm.Documento!).ToList());
                    break;
            }

            return dictionary;
        }
    }
}