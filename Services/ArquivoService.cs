using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;

namespace Eva.Services
{
    public class ArquivoService
    {
        private readonly EvaDbContext _context;
        private readonly ISubmissaoService _submissaoService;

        // 5 MB limit
        private const int MaxFileSize = 5 * 1024 * 1024;

        // Allowed signatures for PDF, JPEG, PNG
        private static readonly byte[][] AllowedSignatures =
        {
            new byte[] { 0x25, 0x50, 0x44, 0x46 }, // PDF
            new byte[] { 0xFF, 0xD8, 0xFF },       // JPEG
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }  // PNG
        };

        public ArquivoService(EvaDbContext context, ISubmissaoService submissaoService)
        {
            _context = context;
            _submissaoService = submissaoService;
        }

        public async Task<Documento> SalvarDocumentoAsync(IFormFile file, string tipoDoc, string entTipo, string entId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido ou vazio.");

            if (file.Length > MaxFileSize)
                throw new ArgumentException("O arquivo excede o limite máximo permitido de 5MB.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            if (!IsValidFileSignature(fileBytes))
                throw new ArgumentException("Formato de arquivo não suportado. Apenas PDF, JPG e PNG são permitidos.");

            string hash;
            using (var md5 = MD5.Create())
            {
                hash = Convert.ToHexString(md5.ComputeHash(fileBytes)).ToLowerInvariant();
            }

            var doc = new Documento
            {
                DocumentoTipoNome = tipoDoc,
                Conteudo = fileBytes,
                NomeArquivo = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                Tamanho = file.Length,
                Hash = hash,
                DataUpload = DateTime.UtcNow // Always use UTC for PostgreSQL
            };

            _context.Documentos.Add(doc);

            if (entTipo == "VIAGEM" && int.TryParse(entId, out int vId))
                _context.DocumentoViagens.Add(new DocumentoViagem { Documento = doc, ViagemId = vId });

            await _context.SaveChangesAsync();

            if (entTipo != "VIAGEM")
            {
                await _submissaoService.VincularDocumentoAoDraftAsync(entTipo, entId, doc.Id, tipoDoc, null);
            }

            return doc;
        }

        public async Task DeletarDocumentoAsync(int docId, string entTipo, string entId)
        {
            var doc = await _context.Documentos.FindAsync(docId);
            if (doc != null)
            {
                _context.Documentos.Remove(doc);
                await _context.SaveChangesAsync();

                if (entTipo != "VIAGEM")
                {
                    await _submissaoService.RemoverDocumentoDoDraftAsync(entTipo, entId, docId, null);
                }
            }
        }

        private bool IsValidFileSignature(byte[] fileData)
        {
            if (fileData.Length < 4) return false;

            return AllowedSignatures.Any(signature =>
                fileData.Take(signature.Length).SequenceEqual(signature));
        }
    }
}
