using System.Collections.Generic;
using System.Linq;

namespace Eva.Models
{
    public class EntityHealthReport
    {
        public bool IsLegal { get; set; }

        // The latest decisive status (APROVADO or REJEITADO) - used to validate trips
        public string AnalystStatus { get; set; } = "INCOMPLETO";

        // The absolute latest status from the workflow
        public string CurrentStatus { get; set; } = "INCOMPLETO";

        public List<string> MissingMandatoryDocs { get; set; } = new List<string>();
        public List<string> ExpiredDocs { get; set; } = new List<string>();
        public List<string> PendingDocs { get; set; } = new List<string>();

        // Logic check for active workflow states
        public bool HasPendingChanges => CurrentStatus == "EM_ANALISE" || CurrentStatus == "AGUARDANDO_ANALISE";

        // Matches the locking logic used in the Empresa edit screens
        public bool IsLocked => CurrentStatus == "EM_ANALISE";
    }
}