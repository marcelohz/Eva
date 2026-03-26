using System.Collections.Generic;
using System.Linq;
using Eva.Workflow;

namespace Eva.Models
{
    public class EntityHealthReport
    {
        public bool IsLegal { get; set; }

        // Official operational status derived from the currently accepted state.
        public string AnalystStatus { get; set; } = WorkflowStatus.Incompleto;

        // UI-facing status combining the official state with the latest submission state.
        public string CurrentStatus { get; set; } = WorkflowStatus.Incompleto;

        public string? LatestSubmissionStatus { get; set; }
        public string? LastRejectionReason { get; set; }

        public List<string> MissingMandatoryDocs { get; set; } = new List<string>();
        public List<string> ExpiredDocs { get; set; } = new List<string>();
        public List<string> PendingDocs { get; set; } = new List<string>();

        // Logic check for active workflow states
        public bool HasPendingChanges => WorkflowStatus.IsPending(CurrentStatus);

        // Matches the locking logic used in the Empresa edit screens
        public bool IsLocked => WorkflowStatus.IsPending(CurrentStatus);
    }
}
