namespace Eva.Workflow
{
    public static class WorkflowStatus
    {
        public const string AguardandoAnalise = "AGUARDANDO_ANALISE";
        public const string EmAnalise = "EM_ANALISE";
        public const string Aprovado = "APROVADO";
        public const string Rejeitado = "REJEITADO";
        public const string Incompleto = "INCOMPLETO";

        public static bool IsPending(string? status) =>
            status == AguardandoAnalise || status == EmAnalise;

        public static string GetDisplayLabel(string? status) => status switch
        {
            AguardandoAnalise => "Aguardando An\u00E1lise",
            EmAnalise => "Em An\u00E1lise",
            Aprovado => "Aprovado",
            Rejeitado => "Rejeitado",
            Incompleto or null => "Incompleto",
            _ => status ?? "Incompleto"
        };

        public static string GetConformidadeHint(string? status) => status switch
        {
            AguardandoAnalise => "Aguardando an\u00E1lise da Metroplan",
            EmAnalise => "Em an\u00E1lise pela Metroplan",
            Aprovado => "Opera\u00E7\u00E3o Regular",
            _ => "Pend\u00EAncia de cadastro ou documenta\u00E7\u00E3o"
        };

        public static string GetBadgeClass(string? status) => status switch
        {
            Incompleto => "bg-warning text-dark",
            AguardandoAnalise => "bg-info text-dark",
            EmAnalise => "bg-orange text-dark",
            Aprovado => "bg-success",
            Rejeitado => "bg-danger",
            _ => "bg-secondary"
        };

        public static string GetBadgeIcon(string? status) => status switch
        {
            Incompleto => "bi-file-earmark-excel",
            AguardandoAnalise => "bi-hourglass-split",
            EmAnalise => "bi-search",
            Aprovado => "bi-check-circle",
            Rejeitado => "bi-x-circle",
            _ => "bi-circle"
        };
    }
}
