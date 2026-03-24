using System.Collections.Generic;
using System.Linq;
using Eva.Workflow;

namespace Eva.Models
{
    public class EntityHealthReport
    {
        public bool IsLegal { get; set; }

        // The latest decisive status (APROVADO or REJEITADO) - used to validate trips
        public string AnalystStatus { get; set; } = WorkflowStatus.Incompleto;

        // The absolute latest status from the workflow
        public string CurrentStatus { get; set; } = WorkflowStatus.Incompleto;

        public List<string> MissingMandatoryDocs { get; set; } = new List<string>();
        public List<string> ExpiredDocs { get; set; } = new List<string>();
        public List<string> PendingDocs { get; set; } = new List<string>();

        // Logic check for active workflow states
        public bool HasPendingChanges => WorkflowStatus.IsPending(CurrentStatus);

        // Matches the locking logic used in the Empresa edit screens
        public bool IsLocked => CurrentStatus == WorkflowStatus.EmAnalise;
    }
}
