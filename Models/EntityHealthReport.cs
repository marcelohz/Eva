using System.Collections.Generic;
using System.Linq;

namespace Eva.Models
{
    public class EntityHealthReport
    {
        public bool IsLegal { get; set; }

        // The latest decisive status (APROVADO or REJEITADO) - used to validate trips
        public string AnalystStatus { get; set; } = "INCOMPLETO";

        // The absolute latest status from the workflow - used to show "Em Análise" correctly
        public string CurrentStatus { get; set; } = "INCOMPLETO";

        public List<string> MissingMandatoryDocs { get; set; } = new List<string>();
        public List<string> ExpiredDocs { get; set; } = new List<string>();
        public List<string> PendingDocs { get; set; } = new List<string>();

        // We now check the CurrentStatus, so new entities correctly show as Pending!
        public bool HasPendingChanges => PendingDocs.Any() || CurrentStatus == "EM_ANALISE" || CurrentStatus == "AGUARDANDO_ANALISE";
    }
}