using System.Linq;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;

namespace Eva.Services
{
    public class ViagemCreationRequest
    {
        public string EmpresaCnpj { get; set; } = string.Empty;
        public NovaViagemVM Input { get; set; } = new();
    }

    public class ViagemCreationResult
    {
        public int ViagemId { get; init; }
        public decimal Valor { get; init; }
    }

    public interface IViagemCreationService
    {
        Task<ViagemCreationResult> CreateAsync(ViagemCreationRequest request);
    }

    public class ViagemCreationService : IViagemCreationService
    {
        private readonly EvaDbContext _context;

        public ViagemCreationService(EvaDbContext context)
        {
            _context = context;
        }

        public async Task<ViagemCreationResult> CreateAsync(ViagemCreationRequest request)
        {
            var valorCalculado = CalcularValor(request.Input);

            var viagem = new Viagem
            {
                EmpresaCnpj = request.EmpresaCnpj,
                ViagemTipoNome = request.Input.ViagemTipoNome,
                NomeContratante = request.Input.NomeContratante,
                CpfCnpjContratante = request.Input.CpfCnpjContratante,
                RegiaoCodigo = request.Input.RegiaoCodigo,
                IdaEm = request.Input.IdaEm,
                VoltaEm = request.Input.VoltaEm,
                MunicipioOrigem = request.Input.MunicipioOrigem,
                MunicipioDestino = request.Input.MunicipioDestino,
                VeiculoPlaca = request.Input.VeiculoPlaca,
                MotoristaId = request.Input.MotoristaId,
                MotoristaAuxId = request.Input.MotoristaAuxId > 0 ? request.Input.MotoristaAuxId : null,
                Descricao = request.Input.Descricao,
                Valor = valorCalculado,
                Pago = false
            };

            foreach (var passageiro in request.Input.Passageiros)
            {
                viagem.Passageiros.Add(new Passageiro
                {
                    Nome = passageiro.Nome,
                    Cpf = passageiro.Documento
                });
            }

            _context.Viagens.Add(viagem);
            await _context.SaveChangesAsync();

            return new ViagemCreationResult
            {
                ViagemId = viagem.Id,
                Valor = viagem.Valor
            };
        }

        private static decimal CalcularValor(NovaViagemVM input)
        {
            var valorCalculado = 150.00m;

            if (input.MunicipioOrigem.Trim().ToLower() != input.MunicipioDestino.Trim().ToLower())
            {
                valorCalculado += 235.50m;
            }

            return valorCalculado;
        }
    }
}
