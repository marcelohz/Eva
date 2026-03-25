using System;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Microsoft.EntityFrameworkCore;

namespace Eva.Services
{
    public enum ViagemEditMode
    {
        Full = 0,
        PassengersOnly = 1,
        ReadOnly = 2
    }

    public sealed class ViagemManagementAccessResult
    {
        public bool ExistsAndBelongsToEmpresa { get; init; }
        public ViagemEditMode EditMode { get; init; }
        public string ActionLabel { get; init; } = "Detalhes";
        public string? Message { get; init; }

        public bool CanEditFull => ExistsAndBelongsToEmpresa && EditMode == ViagemEditMode.Full;
        public bool CanEditPassengersOnly => ExistsAndBelongsToEmpresa && EditMode == ViagemEditMode.PassengersOnly;
        public bool IsReadOnly => !ExistsAndBelongsToEmpresa || EditMode == ViagemEditMode.ReadOnly;
    }

    public interface IViagemManagementService
    {
        Task<ViagemManagementAccessResult> GetAccessAsync(string empresaCnpj, int viagemId);
        ViagemManagementAccessResult GetAccess(Viagem viagem, DateTime? utcNow = null);
    }

    public class ViagemManagementService : IViagemManagementService
    {
        public const int PassengerEditWindowHours = 2;

        private readonly EvaDbContext _context;

        public ViagemManagementService(EvaDbContext context)
        {
            _context = context;
        }

        public async Task<ViagemManagementAccessResult> GetAccessAsync(string empresaCnpj, int viagemId)
        {
            var viagem = await _context.Viagens
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == viagemId && v.EmpresaCnpj == empresaCnpj);

            if (viagem == null)
            {
                return new ViagemManagementAccessResult
                {
                    ExistsAndBelongsToEmpresa = false,
                    EditMode = ViagemEditMode.ReadOnly,
                    ActionLabel = "Detalhes",
                    Message = "Viagem não encontrada ou acesso negado."
                };
            }

            return GetAccess(viagem);
        }

        public ViagemManagementAccessResult GetAccess(Viagem viagem, DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;

            if (!viagem.Pago)
            {
                return new ViagemManagementAccessResult
                {
                    ExistsAndBelongsToEmpresa = true,
                    EditMode = ViagemEditMode.Full,
                    ActionLabel = "Editar"
                };
            }

            if (viagem.IdaEm > now.AddHours(PassengerEditWindowHours))
            {
                return new ViagemManagementAccessResult
                {
                    ExistsAndBelongsToEmpresa = true,
                    EditMode = ViagemEditMode.PassengersOnly,
                    ActionLabel = "Editar Passageiros",
                    Message = $"Apenas a lista de passageiros pode ser alterada até {PassengerEditWindowHours} horas antes da viagem."
                };
            }

            return new ViagemManagementAccessResult
            {
                ExistsAndBelongsToEmpresa = true,
                EditMode = ViagemEditMode.ReadOnly,
                ActionLabel = "Detalhes",
                Message = $"Esta viagem está bloqueada para edições porque faltam menos de {PassengerEditWindowHours} horas para a partida."
            };
        }
    }
}
