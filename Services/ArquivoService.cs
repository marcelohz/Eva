using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;

namespace Eva.Services
{
    public class ArquivoService
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public ArquivoService(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        public async Task<Documento> SalvarDocumentoAsync(
            IFormFile file,
            string tipoDocumento,
            string entidadeTipo,
            string entidadeId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido");

            // 1. Read content into byte array
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // 2. Calculate Hash (MD5) for consistency with old system
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(fileBytes);
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // 3. Create Documento record
            var doc = new Documento
            {
                DocumentoTipoNome = tipoDocumento,
                Conteudo = fileBytes,
                NomeArquivo = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                Tamanho = file.Length,
                Hash = hashString,
                DataUpload = DateTime.Now
            };

            _context.Documentos.Add(doc);
            await _context.SaveChangesAsync(); // We need the ID for the link table

            // 4. Link to the specific Entity
            if (entidadeTipo == "EMPRESA")
            {
                var link = new DocumentoEmpresa { Id = doc.Id, EmpresaCnpj = entidadeId };
                _context.DocumentoEmpresas.Add(link);
            }
            else if (entidadeTipo == "VEICULO")
            {
                var link = new DocumentoVeiculo { Id = doc.Id, VeiculoPlaca = entidadeId };
                _context.DocumentoVeiculos.Add(link);
            }
            else if (entidadeTipo == "MOTORISTA" && int.TryParse(entidadeId, out int mId))
            {
                var link = new DocumentoMotorista { Id = doc.Id, MotoristaId = mId };
                _context.DocumentoMotoristas.Add(link);
            }

            await _context.SaveChangesAsync();

            // 5. Trigger Workflow
            await _pendenciaService.AvancarEntidadeAsync(entidadeTipo, entidadeId);

            return doc;
        }

        public async Task DeletarDocumentoAsync(int documentoId, string entidadeTipo, string entidadeId)
        {
            var doc = await _context.Documentos.FindAsync(documentoId);
            if (doc != null)
            {
                _context.Documentos.Remove(doc);
                await _context.SaveChangesAsync();

                // Trigger workflow on deletion too, as it changes the entity state
                await _pendenciaService.AvancarEntidadeAsync(entidadeTipo, entidadeId);
            }
        }
    }
}