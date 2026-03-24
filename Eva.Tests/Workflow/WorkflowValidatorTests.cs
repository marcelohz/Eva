using Eva.Workflow;

namespace Eva.Tests.Workflow;

[Trait("Category", "unit")]
public class WorkflowValidatorTests
{
    [Fact]
    public void Aprovar_fora_de_analise_deve_falhar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkflowValidator.ValidateTransition(
                WorkflowValidator.AguardandoAnalise,
                WorkflowValidator.Aprovado,
                "analista@metroplan.rs.gov.br",
                "analista@metroplan.rs.gov.br"));

        Assert.Equal("O item deve estar em análise antes de ser aprovado ou rejeitado.", ex.Message);
    }

    [Fact]
    public void Rejeitar_sem_motivo_deve_falhar()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WorkflowValidator.ValidateTransition(
                WorkflowValidator.EmAnalise,
                WorkflowValidator.Rejeitado,
                "analista@metroplan.rs.gov.br",
                "analista@metroplan.rs.gov.br",
                ""));

        Assert.Equal("O motivo é obrigatório para rejeições.", ex.Message);
    }

    [Fact]
    public void Aprovar_com_analista_diferente_deve_falhar()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            WorkflowValidator.ValidateTransition(
                WorkflowValidator.EmAnalise,
                WorkflowValidator.Aprovado,
                "analista1@metroplan.rs.gov.br",
                "analista2@metroplan.rs.gov.br"));

        Assert.Equal("Apenas o analista que iniciou a análise pode concluí-la.", ex.Message);
    }

    [Fact]
    public void Aprovar_em_analise_com_mesmo_analista_deve_passar()
    {
        WorkflowValidator.ValidateTransition(
            WorkflowValidator.EmAnalise,
            WorkflowValidator.Aprovado,
            "analista@metroplan.rs.gov.br",
            "analista@metroplan.rs.gov.br");
    }

    [Fact]
    public void Retornar_para_incompleto_fora_de_analise_deve_passar()
    {
        WorkflowValidator.ValidateTransition(
            WorkflowValidator.Rejeitado,
            WorkflowValidator.Incompleto,
            "analista@metroplan.rs.gov.br",
            null);
    }
}
