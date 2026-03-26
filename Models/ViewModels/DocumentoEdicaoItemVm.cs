using Eva.Models;

namespace Eva.Models.ViewModels
{
    public class DocumentoEdicaoItemVm
    {
        public Documento Documento { get; init; } = null!;
        public string? StatusRevisao { get; init; }
        public string? MotivoRejeicao { get; init; }
        public bool CarregadoDeDocumentoAtual { get; init; }
    }
}
