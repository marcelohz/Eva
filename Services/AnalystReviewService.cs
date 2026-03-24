using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eva.Data;

namespace Eva.Services
{
    public class ReviewActionResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? SuccessMessage { get; init; }

        public static ReviewActionResult Ok(string? successMessage = null) =>
            new()
            {
                Success = true,
                SuccessMessage = successMessage
            };

        public static ReviewActionResult Failed(string errorMessage) =>
            new()
            {
                Success = false,
                ErrorMessage = errorMessage
            };
    }

    public interface IAnalystReviewService
    {
        Task<ReviewActionResult> IniciarAnaliseAsync(string tipo, string id, string analistaEmail);
        Task<ReviewActionResult> AprovarAsync(string tipo, string id, string analistaEmail, Dictionary<int, DateOnly?> validades);
        Task<ReviewActionResult> RejeitarAsync(string tipo, string id, string analistaEmail, string motivoRejeicao);
    }

    public class AnalystReviewService : IAnalystReviewService
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly IEntityStatusService _statusService;

        public AnalystReviewService(
            EvaDbContext context,
            PendenciaService pendenciaService,
            IEntityStatusService statusService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _statusService = statusService;
        }

        public async Task<ReviewActionResult> IniciarAnaliseAsync(string tipo, string id, string analistaEmail)
        {
            try
            {
                await _pendenciaService.IniciarAnaliseAsync(tipo, id, analistaEmail);
                return ReviewActionResult.Ok();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                return ReviewActionResult.Failed(ex.Message);
            }
        }

        public async Task<ReviewActionResult> AprovarAsync(string tipo, string id, string analistaEmail, Dictionary<int, DateOnly?> validades)
        {
            try
            {
                foreach (var kvp in validades)
                {
                    if (!kvp.Value.HasValue)
                    {
                        continue;
                    }

                    var doc = await _context.Documentos.FindAsync(kvp.Key);
                    if (doc != null)
                    {
                        doc.Validade = kvp.Value.Value;
                    }
                }

                await _context.SaveChangesAsync();

                var health = await _statusService.GetHealthAsync(tipo, id);
                if (health.MissingMandatoryDocs.Any())
                {
                    return ReviewActionResult.Failed($"Faltam documentos obrigatórios: {string.Join(", ", health.MissingMandatoryDocs)}.");
                }

                await _pendenciaService.AprovarAsync(tipo, id, analistaEmail);
                return ReviewActionResult.Ok("Aprovação concluída com sucesso.");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                return ReviewActionResult.Failed(ex.Message);
            }
        }

        public async Task<ReviewActionResult> RejeitarAsync(string tipo, string id, string analistaEmail, string motivoRejeicao)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivoRejeicao))
                {
                    return ReviewActionResult.Failed("O motivo é obrigatório para rejeições.");
                }

                await _pendenciaService.RejeitarAsync(tipo, id, analistaEmail, motivoRejeicao);
                return ReviewActionResult.Ok("Rejeição registrada com sucesso.");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                return ReviewActionResult.Failed(ex.Message);
            }
        }
    }
}
