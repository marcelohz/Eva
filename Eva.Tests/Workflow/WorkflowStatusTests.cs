using Eva.Workflow;
using Xunit;

namespace Eva.Tests.Workflow;

public class WorkflowStatusTests
{
    [Theory]
    [InlineData(WorkflowStatus.AguardandoAnalise, "Aguardando Análise")]
    [InlineData(WorkflowStatus.EmAnalise, "Em Análise")]
    [InlineData(WorkflowStatus.Aprovado, "Aprovado")]
    [InlineData(WorkflowStatus.Rejeitado, "Rejeitado")]
    [InlineData(WorkflowStatus.Incompleto, "Incompleto")]
    public void GetDisplayLabel_returns_canonical_label(string status, string expected)
    {
        Assert.Equal(expected, WorkflowStatus.GetDisplayLabel(status));
    }

    [Theory]
    [InlineData(WorkflowStatus.AguardandoAnalise, "Aguardando análise da Metroplan")]
    [InlineData(WorkflowStatus.EmAnalise, "Em análise pela Metroplan")]
    [InlineData(WorkflowStatus.Aprovado, "Operação Regular")]
    public void GetConformidadeHint_returns_expected_hint(string status, string expected)
    {
        Assert.Equal(expected, WorkflowStatus.GetConformidadeHint(status));
    }
}
