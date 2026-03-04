using System;

namespace Eva.Workflow
{
    public static class WorkflowValidator
    {
        public const string AguardandoAnalise = "AGUARDANDO_ANALISE";
        public const string EmAnalise = "EM_ANALISE";
        public const string Aprovado = "APROVADO";
        public const string Rejeitado = "REJEITADO";

        /// <summary>
        /// Evaluates a state transition for eventual_status compliance.
        /// Throws exceptions for invalid workflows; returns silently for valid paths.
        /// </summary>
        public static void ValidateTransition(
            string? currentState,
            string nextState,
            string? currentAnalistaEmail,
            string? nextAnalistaEmail,
            string? motivo = null)
        {
            // 1. Idempotency (Double-Clicks)
            if (currentState == nextState)
            {
                // Prevent lock stealing (mirrors fn_avancar_pendencia)
                if (nextState == EmAnalise &&
                    !string.IsNullOrWhiteSpace(currentAnalistaEmail) &&
                    currentAnalistaEmail != nextAnalistaEmail)
                {
                    throw new InvalidOperationException($"Lock violation: Cannot steal an EM_ANALISE lock from another analyst. Currently locked by {currentAnalistaEmail}.");
                }

                return; // Silent success for all other double-clicks
            }

            // 2. The Company Reset
            if (nextState == AguardandoAnalise)
            {
                // Cannot rip the item out of an analyst's hands
                if (currentState == EmAnalise)
                {
                    throw new InvalidOperationException("Invalid transition: Cannot reset to AGUARDANDO_ANALISE while the item is currently EM_ANALISE.");
                }

                // Allowed for other states, bypasses ownership rules
                return;
            }

            // 3. Database Triggers / Mandatory Fields
            // Mirrors fn_analista_obrigatorio
            if (string.IsNullOrWhiteSpace(nextAnalistaEmail))
            {
                throw new ArgumentException("An Analista (email) is mandatory for any state other than AGUARDANDO_ANALISE.", nameof(nextAnalistaEmail));
            }

            // Mirrors fn_motivo_obrigatorio
            if (nextState == Rejeitado && string.IsNullOrWhiteSpace(motivo))
            {
                throw new ArgumentException("A Motivo is mandatory when rejecting.", nameof(motivo));
            }

            // 4. Strict Analyst Workflow & 5. Ownership
            if (nextState == Aprovado || nextState == Rejeitado)
            {
                if (currentState != EmAnalise)
                {
                    throw new InvalidOperationException($"Invalid sequence: Cannot transition to {nextState} from {currentState}. The item must be {EmAnalise} first.");
                }

                if (currentAnalistaEmail != nextAnalistaEmail)
                {
                    throw new UnauthorizedAccessException("Ownership violation: Only the analyst who locked the item can approve or reject it.");
                }
            }
        }
    }
}