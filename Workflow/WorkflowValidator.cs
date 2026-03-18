using System;

namespace Eva.Workflow
{
    public static class WorkflowValidator
    {
        public const string AguardandoAnalise = "AGUARDANDO_ANALISE";
        public const string EmAnalise = "EM_ANALISE";
        public const string Aprovado = "APROVADO";
        public const string Rejeitado = "REJEITADO";

        public static void ValidateTransition(string? currentState, string nextState, string? currentAnalista, string? nextAnalista, string? motivo = null, bool isOverride = false)
        {
            // Admins can bypass workflow locks to resolve deadlocks
            if (isOverride) return;

            if (currentState == nextState)
            {
                if (nextState == EmAnalise && !string.IsNullOrWhiteSpace(currentAnalista) && currentAnalista != nextAnalista)
                    throw new InvalidOperationException($"Bloqueio: Este item já está sendo analisado por {currentAnalista}.");
                return;
            }

            if (nextState == AguardandoAnalise)
            {
                // Allow returning the item to the queue, but only if you are the current analyst
                if (currentState == EmAnalise && !string.IsNullOrWhiteSpace(currentAnalista) && currentAnalista != nextAnalista)
                    throw new InvalidOperationException("Apenas o analista atual pode devolver o item para a fila, ou solicite a um Administrador.");
                return;
            }

            if (string.IsNullOrWhiteSpace(nextAnalista) && nextState != AguardandoAnalise)
                throw new ArgumentException("O e-mail do analista é obrigatório.");

            if (nextState == Rejeitado && string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("O motivo é obrigatório para rejeições.");

            if (nextState == Aprovado || nextState == Rejeitado)
            {
                if (currentState != EmAnalise)
                    throw new InvalidOperationException("O item deve estar em análise antes de ser aprovado ou rejeitado.");
                if (currentAnalista != nextAnalista)
                    throw new UnauthorizedAccessException("Apenas o analista que iniciou a análise pode concluí-la.");
            }
        }
    }
}