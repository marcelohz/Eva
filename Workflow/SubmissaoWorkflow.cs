namespace Eva.Workflow
{
    public static class SubmissaoWorkflow
    {
        public const string EmEdicao = "EM_EDICAO";
        public const string AguardandoAnalise = "AGUARDANDO_ANALISE";
        public const string EmAnalise = "EM_ANALISE";
        public const string Aprovada = "APROVADA";
        public const string Rejeitada = "REJEITADA";

        public const string RevisaoPendente = "PENDENTE";
        public const string RevisaoAprovada = "APROVADO";
        public const string RevisaoRejeitada = "REJEITADO";

        public static bool EstaSubmetida(string status) =>
            status == AguardandoAnalise || status == EmAnalise || status == Aprovada || status == Rejeitada;

        public static bool EstaBloqueadaParaEmpresa(string status) =>
            status == AguardandoAnalise || status == EmAnalise;

        public static string GetDisplayLabel(string? status) => status switch
        {
            EmEdicao => "Em edi\u00E7\u00E3o",
            AguardandoAnalise => "Aguardando an\u00E1lise",
            EmAnalise => "Em an\u00E1lise",
            Aprovada => "Aprovada",
            Rejeitada => "Rejeitada",
            _ => "Sem submiss\u00E3o"
        };

        public static string GetBadgeClass(string? status) => status switch
        {
            EmEdicao => "bg-warning text-dark",
            AguardandoAnalise => "bg-info text-dark",
            EmAnalise => "bg-orange text-dark",
            Aprovada => "bg-success",
            Rejeitada => "bg-danger",
            _ => "bg-secondary"
        };

        public static string GetBadgeIcon(string? status) => status switch
        {
            EmEdicao => "bi-pencil-square",
            AguardandoAnalise => "bi-hourglass-split",
            EmAnalise => "bi-search",
            Aprovada => "bi-check-circle",
            Rejeitada => "bi-x-circle",
            _ => "bi-circle"
        };

        public static string GetReviewDisplayLabel(string? status) => status switch
        {
            RevisaoPendente => "Pendente",
            RevisaoAprovada => "Aprovado",
            RevisaoRejeitada => "Rejeitado",
            _ => "Sem revis\u00E3o"
        };

        public static string GetReviewBadgeClass(string? status) => status switch
        {
            RevisaoPendente => "bg-warning text-dark",
            RevisaoAprovada => "bg-success",
            RevisaoRejeitada => "bg-danger",
            _ => "bg-secondary"
        };
    }
}
