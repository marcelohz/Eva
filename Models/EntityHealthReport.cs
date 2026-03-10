using System.Collections.Generic;
using System.Linq;

namespace Eva.Models
{
    public class EntityHealthReport
    {
        public bool IsLegal { get; set; }
        public string AnalystStatus { get; set; } = "INCOMPLETO";
        public List<string> MissingMandatoryDocs { get; set; } = new List<string>();
        public List<string> ExpiredDocs { get; set; } = new List<string>();
        public List<string> PendingDocs { get; set; } = new List<string>();

        public bool HasPendingChanges => PendingDocs.Any() || AnalystStatus == "EM_ANALISE" || AnalystStatus == "AGUARDANDO_ANALISE";
    }
}