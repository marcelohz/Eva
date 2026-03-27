param(
    [string]$BaseUrl = "http://localhost:5103",
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$DbName = "metroplan",
    [string]$DbUser = "postgres",
    [string]$DbPassword = "master",
    [string]$EmpresaEmail = "net@net",
    [string]$AnalistaEmail = "ana@ana",
    [string]$AdminEmail = "adm@adm",
    [string]$Password = "master"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\\live-test-lib.ps1"

$TextSituacaoOperacional = U @(83,105,116,117,97,231,227,111,32,79,112,101,114,97,99,105,111,110,97,108)
$TextUltimaSubmissao = U @(218,108,116,105,109,97,32,83,117,98,109,105,115,115,227,111)
$TextEmEdicao = U @(69,109,32,101,100,105,231,227,111)
$TextEmpresaRejeitada = U @(83,117,98,109,105,115,115,227,111,32,100,97,32,101,109,112,114,101,115,97,32,114,101,106,101,105,116,97,100,97,32,110,111,32,108,105,118,101,32,102,108,111,119)

function Assert-PageStatuses {
    param([string]$Label, [string]$Html, [string]$SituacaoOperacional, [string]$UltimaSubmissao)
    Assert-Contains -Label $Label -Text $Html -Needle $TextSituacaoOperacional
    Assert-Contains -Label $Label -Text $Html -Needle $TextUltimaSubmissao
    Assert-Contains -Label $Label -Text $Html -Needle $SituacaoOperacional
    Assert-Contains -Label $Label -Text $Html -Needle $UltimaSubmissao
}

function Start-Review {
    param($Session, [int]$SubmissaoId)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=Iniciar' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
    }
    Assert-Status -Label "POST Revisao iniciar $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Approve-Data {
    param($Session, [int]$SubmissaoId)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=AprovarDados' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
    }
    Assert-Status -Label "POST AprovarDados $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Approve-Document {
    param($Session, [int]$SubmissaoId, [int]$DocumentoEmAcaoId)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=AprovarDocumento' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
        'DocumentoEmAcaoId' = $DocumentoEmAcaoId
        'ValidadeDocumento' = '2030-12-31'
    }
    Assert-Status -Label "POST AprovarDocumento $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Reject-Data {
    param($Session, [int]$SubmissaoId, [string]$Reason)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=RejeitarDados' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
        'MotivoRejeicaoDados' = $Reason
    }
    Assert-Status -Label "POST RejeitarDados $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Reject-Document {
    param($Session, [int]$SubmissaoId, [int]$DocumentoEmAcaoId, [string]$Reason)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=RejeitarDocumento' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
        'DocumentoEmAcaoId' = $DocumentoEmAcaoId
        'MotivoRejeicaoDocumento' = $Reason
    }
    Assert-Status -Label "POST RejeitarDocumento $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Approve-Submission {
    param($Session, [int]$SubmissaoId)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=AprovarSubmissao' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
    }
    Assert-Status -Label "POST AprovarSubmissao $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

function Reject-Submission {
    param($Session, [int]$SubmissaoId, [string]$Reason)
    $review = Get-PageAndToken -Session $Session -Path "/Metroplan/Analista/Revisao?submissaoId=$SubmissaoId"
    $response = Invoke-LivePostForm -Session $Session -Path '/Metroplan/Analista/Revisao?handler=RejeitarSubmissao' -Fields @{
        '__RequestVerificationToken' = $review.Token
        'SubmissaoId' = $SubmissaoId
        'ObservacaoRejeicaoSubmissao' = $Reason
    }
    Assert-Status -Label "POST RejeitarSubmissao $SubmissaoId" -Actual $response.StatusCode -Allowed @(302)
}

$empresaSession = $null
$analistaSession = $null
$adminSession = $null
$companyPdf = $null
$motoristaPdf1 = $null
$motoristaPdf2 = $null
$veiculoPdf = $null

try {
    $empresaSession = New-LiveSession -BaseUrl $BaseUrl
    $analistaSession = New-LiveSession -BaseUrl $BaseUrl
    $adminSession = New-LiveSession -BaseUrl $BaseUrl

    Login-LiveUser -Session $empresaSession -Email $EmpresaEmail -Password $Password -ExpectedLocation '/Empresa/MinhaEmpresa'
    Login-LiveUser -Session $analistaSession -Email $AnalistaEmail -Password $Password -ExpectedLocation '/Metroplan/Analista'
    Login-LiveUser -Session $adminSession -Email $AdminEmail -Password $Password -ExpectedLocation '/Metroplan/Admin'

    $companyPdf = New-PdfFile -Name 'company.pdf'
    $motoristaPdf1 = New-PdfFile -Name 'motorista-1.pdf'
    $motoristaPdf2 = New-PdfFile -Name 'motorista-2.pdf'
    $veiculoPdf = New-PdfFile -Name 'veiculo.pdf'

    $suffix = Get-Date -Format 'yyyyMMddHHmmss'
    $report = New-Object System.Collections.Generic.List[object]

    $empresaCnpj = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select empresa_cnpj from web.usuario where email = '$EmpresaEmail';"
    $empresaNome = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select nome from geral.empresa where cnpj = '$empresaCnpj';"

    foreach ($path in @('/Empresa/MinhaEmpresa','/Empresa/EditarEmpresa','/Empresa/MeusMotoristas','/Empresa/MeusVeiculos','/Empresa/CentralConformidade','/Empresa/MinhasViagens','/Metroplan/Analista','/Metroplan/Admin')) {
        $session = if ($path -like '/Metroplan/Analista*') { $analistaSession } elseif ($path -like '/Metroplan/Admin*') { $adminSession } else { $empresaSession }
        $page = Invoke-LiveGet -Session $session -Path $path
        Assert-Status -Label "GET $path" -Actual $page.StatusCode -Allowed @(200)
    }
    $report.Add([pscustomobject]@{ Flow = 'Basic route availability'; Result = 'OK'; Reference = 'empresa, analista, admin' })

    Invoke-DbNonQuery -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql @"
update eventual.submissao
set submissao_origem_id = null
where entidade_tipo = 'EMPRESA'
  and entidade_id = '$empresaCnpj'
  and submissao_origem_id in (
      select id from eventual.submissao
      where entidade_tipo = 'EMPRESA'
        and entidade_id = '$empresaCnpj'
        and status <> 'APROVADA'
  );
delete from eventual.submissao_evento where submissao_id in (select id from eventual.submissao where entidade_tipo = 'EMPRESA' and entidade_id = '$empresaCnpj' and status <> 'APROVADA');
delete from eventual.submissao_documento where submissao_id in (select id from eventual.submissao where entidade_tipo = 'EMPRESA' and entidade_id = '$empresaCnpj' and status <> 'APROVADA');
delete from eventual.submissao_dados where submissao_id in (select id from eventual.submissao where entidade_tipo = 'EMPRESA' and entidade_id = '$empresaCnpj' and status <> 'APROVADA');
delete from eventual.submissao where entidade_tipo = 'EMPRESA' and entidade_id = '$empresaCnpj' and status <> 'APROVADA';
"@

    $empresaNomeAprovado = "$empresaNome APROVADO $suffix"
    $empresaNomeRejeitado = "$empresaNome REJEITADO $suffix"
    $empresaDocRejectedReason = 'Documento da empresa rejeitado no live flow'

    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa save approved' -Actual (Invoke-LivePostForm -Session $empresaSession -Path '/Empresa/EditarEmpresa' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'Input.Cnpj' = $empresaCnpj; 'Input.Nome' = $empresaNomeAprovado; 'Input.NomeFantasia' = $empresaNomeAprovado; 'Input.Email' = $EmpresaEmail }).StatusCode -Allowed @(200)
    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa upload approved' -Actual (Invoke-LivePostMultipart -Session $empresaSession -Path '/Empresa/EditarEmpresa?handler=Upload' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'TipoDocumentoUpload' = 'CARTAO_CNPJ' } -Files @{ 'UploadArquivo' = $companyPdf }).StatusCode -Allowed @(200)
    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa send approved' -Actual (Invoke-LivePostForm -Session $empresaSession -Path '/Empresa/EditarEmpresa?handler=Enviar' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'Input.Cnpj' = $empresaCnpj; 'Input.Nome' = $empresaNomeAprovado; 'Input.NomeFantasia' = $empresaNomeAprovado; 'Input.Email' = $EmpresaEmail }).StatusCode -Allowed @(302)

    $empresaSubId = [int](Get-LatestSubmissionId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -EntityType 'EMPRESA' -EntityId $empresaCnpj)
    $empresaDocSubId = [int](Get-LatestSubmissionDocId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $empresaSubId -TipoNome 'CARTAO_CNPJ')
    Start-Review -Session $analistaSession -SubmissaoId $empresaSubId
    Approve-Data -Session $analistaSession -SubmissaoId $empresaSubId
    Approve-Document -Session $analistaSession -SubmissaoId $empresaSubId -DocumentoEmAcaoId $empresaDocSubId
    Approve-Submission -Session $analistaSession -SubmissaoId $empresaSubId
    Assert-PageStatuses -Label 'EditarEmpresa approved' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/EditarEmpresa').Body -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'

    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa save rejected' -Actual (Invoke-LivePostForm -Session $empresaSession -Path '/Empresa/EditarEmpresa' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'Input.Cnpj' = $empresaCnpj; 'Input.Nome' = $empresaNomeRejeitado; 'Input.NomeFantasia' = $empresaNomeRejeitado; 'Input.Email' = $EmpresaEmail }).StatusCode -Allowed @(200)
    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa upload rejected' -Actual (Invoke-LivePostMultipart -Session $empresaSession -Path '/Empresa/EditarEmpresa?handler=Upload' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'TipoDocumentoUpload' = 'CARTAO_CNPJ' } -Files @{ 'UploadArquivo' = $companyPdf }).StatusCode -Allowed @(200)
    $empresaEdit = Get-PageAndToken -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Status -Label 'POST EditarEmpresa send rejected' -Actual (Invoke-LivePostForm -Session $empresaSession -Path '/Empresa/EditarEmpresa?handler=Enviar' -Fields @{ '__RequestVerificationToken' = $empresaEdit.Token; 'Input.Cnpj' = $empresaCnpj; 'Input.Nome' = $empresaNomeRejeitado; 'Input.NomeFantasia' = $empresaNomeRejeitado; 'Input.Email' = $EmpresaEmail }).StatusCode -Allowed @(302)

    $empresaSubId2 = [int](Get-LatestSubmissionId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -EntityType 'EMPRESA' -EntityId $empresaCnpj)
    $empresaDocSubId2 = [int](Get-LatestSubmissionDocId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $empresaSubId2 -TipoNome 'CARTAO_CNPJ')
    Start-Review -Session $analistaSession -SubmissaoId $empresaSubId2
    Approve-Data -Session $analistaSession -SubmissaoId $empresaSubId2
    Reject-Document -Session $analistaSession -SubmissaoId $empresaSubId2 -DocumentoEmAcaoId $empresaDocSubId2 -Reason $empresaDocRejectedReason
    Reject-Submission -Session $analistaSession -SubmissaoId $empresaSubId2 -Reason $TextEmpresaRejeitada

    $empresaNomeLive = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select nome from geral.empresa where cnpj = '$empresaCnpj';"
    if ($empresaNomeLive -ne $empresaNomeAprovado) { throw 'Live company name changed after rejected replacement.' }
    $empresaRejectedPage = Invoke-LiveGet -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-PageStatuses -Label 'EditarEmpresa rejected replacement' -Html $empresaRejectedPage.Body -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Rejeitada'
    Assert-Contains -Label 'EditarEmpresa rejection reason' -Text $empresaRejectedPage.Body -Needle $TextEmpresaRejeitada
    Assert-Contains -Label 'EditarEmpresa rejected doc reason' -Text $empresaRejectedPage.Body -Needle $empresaDocRejectedReason
    Assert-RowStatuses -Label 'CentralConformidade empresa' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/CentralConformidade').Body -Needle $empresaNomeAprovado -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Rejeitada'
    $report.Add([pscustomobject]@{ Flow = 'Company approved then rejected replacement'; Result = 'OK'; Reference = 'operacional Aprovado / ultima submissao Rejeitada' })

    $motoristaCpf = New-UniqueCpf -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword
    $motoristaNome = "Codex Motorista $suffix"
    $motoristaEmail = "codex-motorista-$suffix@example.com"
    $motoristaCnh = -join ((1..9) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
    $novoMotorista = Get-PageAndToken -Session $empresaSession -Path '/Empresa/NovoMotorista'
    Assert-Status -Label 'POST NovoMotorista' -Actual (Invoke-LivePostMultipart -Session $empresaSession -Path '/Empresa/NovoMotorista' -Fields @{ '__RequestVerificationToken' = $novoMotorista.Token; 'Input.Nome' = $motoristaNome; 'Input.Cpf' = $motoristaCpf; 'Input.Cnh' = $motoristaCnh; 'Input.Email' = $motoristaEmail } -Files @{ 'UploadCnh' = $motoristaPdf1 }).StatusCode -Allowed @(302)

    $motoristaId = [int](Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select id from eventual.motorista where cpf = '$motoristaCpf';")
    if ($motoristaId -le 0) { throw 'Novo motorista was not created in the live table.' }
    $motoristaEdit = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Contains -Label 'EditarMotorista initial upload' -Text $motoristaEdit.Page.Body -Needle 'motorista-1.pdf'
    Assert-PageStatuses -Label 'EditarMotorista initial' -Html $motoristaEdit.Page.Body -SituacaoOperacional 'Incompleto' -UltimaSubmissao $TextEmEdicao
    Assert-Status -Label 'POST EditarMotorista send' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarMotorista/${motoristaId}?handler=Enviar" -Fields @{ '__RequestVerificationToken' = $motoristaEdit.Token; 'Input.Id' = $motoristaId; 'Input.Nome' = $motoristaNome; 'Input.Cpf' = $motoristaCpf; 'Input.Cnh' = $motoristaCnh; 'Input.Email' = $motoristaEmail }).StatusCode -Allowed @(302)

    $motoristaSubId = [int](Get-LatestSubmissionId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -EntityType 'MOTORISTA' -EntityId "$motoristaId")
    $motoristaDocSubId = [int](Get-LatestSubmissionDocId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $motoristaSubId -TipoNome 'CNH')
    $motoristaDocId = [int](Get-LatestSubmissionDocumentId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $motoristaSubId -TipoNome 'CNH')
    Start-Review -Session $analistaSession -SubmissaoId $motoristaSubId
    Reject-Data -Session $analistaSession -SubmissaoId $motoristaSubId -Reason 'Motorista data rejected in live flow test'
    Reject-Document -Session $analistaSession -SubmissaoId $motoristaSubId -DocumentoEmAcaoId $motoristaDocSubId -Reason 'Motorista CNH rejected in live flow test'
    Reject-Submission -Session $analistaSession -SubmissaoId $motoristaSubId -Reason 'Motorista submission rejected in live flow test'

    $motoristaEditRejected = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-PageStatuses -Label 'EditarMotorista rejected' -Html $motoristaEditRejected.Page.Body -SituacaoOperacional 'Incompleto' -UltimaSubmissao 'Rejeitada'
    Assert-Contains -Label 'EditarMotorista data reason' -Text $motoristaEditRejected.Page.Body -Needle 'Motorista data rejected in live flow test'
    Assert-Contains -Label 'EditarMotorista doc reason' -Text $motoristaEditRejected.Page.Body -Needle 'Motorista CNH rejected in live flow test'
    Assert-RowStatuses -Label 'MeusMotoristas rejected' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusMotoristas').Body -Needle $motoristaCpf -SituacaoOperacional 'Incompleto' -UltimaSubmissao 'Rejeitada'
    Assert-RowStatuses -Label 'CentralConformidade motorista rejected' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/CentralConformidade').Body -Needle $motoristaNome -SituacaoOperacional 'Incompleto' -UltimaSubmissao 'Rejeitada'
    Assert-Status -Label 'POST DeleteDoc motorista' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarMotorista/${motoristaId}?handler=DeleteDoc" -Fields @{ '__RequestVerificationToken' = $motoristaEditRejected.Token; 'docId' = $motoristaDocId }).StatusCode -Allowed @(200)

    $motoristaEditNoDoc = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Status -Label 'POST Upload motorista replacement' -Actual (Invoke-LivePostMultipart -Session $empresaSession -Path "/Empresa/EditarMotorista/${motoristaId}?handler=Upload" -Fields @{ '__RequestVerificationToken' = $motoristaEditNoDoc.Token } -Files @{ 'UploadArquivo' = $motoristaPdf2 }).StatusCode -Allowed @(200)
    $motoristaEdit = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Status -Label 'POST Save motorista fixed' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId" -Fields @{ '__RequestVerificationToken' = $motoristaEdit.Token; 'Input.Id' = $motoristaId; 'Input.Nome' = "$motoristaNome aprovado"; 'Input.Cpf' = $motoristaCpf; 'Input.Cnh' = $motoristaCnh; 'Input.Email' = $motoristaEmail }).StatusCode -Allowed @(200)
    $motoristaEdit = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Status -Label 'POST Resend motorista' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarMotorista/${motoristaId}?handler=Enviar" -Fields @{ '__RequestVerificationToken' = $motoristaEdit.Token; 'Input.Id' = $motoristaId; 'Input.Nome' = "$motoristaNome aprovado"; 'Input.Cpf' = $motoristaCpf; 'Input.Cnh' = $motoristaCnh; 'Input.Email' = $motoristaEmail }).StatusCode -Allowed @(302)

    $motoristaSubId2 = [int](Get-LatestSubmissionId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -EntityType 'MOTORISTA' -EntityId "$motoristaId")
    $motoristaDocSubId2 = [int](Get-LatestSubmissionDocId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $motoristaSubId2 -TipoNome 'CNH')
    Start-Review -Session $analistaSession -SubmissaoId $motoristaSubId2
    Approve-Data -Session $analistaSession -SubmissaoId $motoristaSubId2
    Approve-Document -Session $analistaSession -SubmissaoId $motoristaSubId2 -DocumentoEmAcaoId $motoristaDocSubId2
    Approve-Submission -Session $analistaSession -SubmissaoId $motoristaSubId2

    Assert-RowStatuses -Label 'MeusMotoristas approved' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusMotoristas').Body -Needle $motoristaCpf -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    Assert-PageStatuses -Label 'EditarMotorista approved' -Html (Invoke-LiveGet -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId").Body -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    Assert-RowStatuses -Label 'CentralConformidade motorista approved' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/CentralConformidade').Body -Needle "$motoristaNome aprovado" -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    $report.Add([pscustomobject]@{ Flow = 'Motorista reject, fix, approve'; Result = 'OK'; Reference = 'Incompleto/Rejeitada -> Aprovado/Aprovada' })

    $placa = New-UniquePlate -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword
    $veiculoModeloAprovado = "Codex Veiculo $suffix"
    $veiculoModeloDraft = "Codex Veiculo Draft $suffix"
    $novoVeiculo = Get-PageAndToken -Session $empresaSession -Path '/Empresa/NovoVeiculo'
    Assert-Status -Label 'POST NovoVeiculo' -Actual (Invoke-LivePostMultipart -Session $empresaSession -Path '/Empresa/NovoVeiculo' -Fields @{ '__RequestVerificationToken' = $novoVeiculo.Token; 'Input.Placa' = $placa; 'Input.Modelo' = $veiculoModeloAprovado; 'Input.ChassiNumero' = "CHASSI$suffix"; 'Input.Renavan' = "REN$suffix"; 'Input.AnoFabricacao' = '2020'; 'Input.ModeloAno' = '2021'; 'Input.VeiculoCombustivelNome' = 'DIESEL'; 'Input.NumeroLugares' = '20'; 'Input.PotenciaMotor' = '180' } -Files @{ 'UploadCrlv' = $veiculoPdf }).StatusCode -Allowed @(302)
    $veiculoModeloLive = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select modelo from geral.veiculo where placa = '$placa';"
    if ($veiculoModeloLive -ne $veiculoModeloAprovado) { throw 'Novo veiculo was not created in the live table with the expected model.' }
    $veiculoEdit = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarVeiculo/$placa"
    Assert-PageStatuses -Label 'EditarVeiculo initial' -Html $veiculoEdit.Page.Body -SituacaoOperacional 'Incompleto' -UltimaSubmissao $TextEmEdicao
    Assert-Status -Label 'POST EditarVeiculo send' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarVeiculo/${placa}?handler=Enviar" -Fields @{ '__RequestVerificationToken' = $veiculoEdit.Token; 'Input.Placa' = $placa; 'Input.Modelo' = $veiculoModeloAprovado; 'Input.ChassiNumero' = "CHASSI$suffix"; 'Input.Renavan' = "REN$suffix"; 'Input.AnoFabricacao' = '2020'; 'Input.ModeloAno' = '2021'; 'Input.VeiculoCombustivelNome' = 'DIESEL'; 'Input.NumeroLugares' = '20'; 'Input.PotenciaMotor' = '180' }).StatusCode -Allowed @(302)

    $veiculoSubId = [int](Get-LatestSubmissionId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -EntityType 'VEICULO' -EntityId $placa)
    $veiculoDocSubId = [int](Get-LatestSubmissionDocId -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -SubmissionId $veiculoSubId -TipoNome 'CRLV')
    Start-Review -Session $analistaSession -SubmissaoId $veiculoSubId
    Approve-Data -Session $analistaSession -SubmissaoId $veiculoSubId
    Approve-Document -Session $analistaSession -SubmissaoId $veiculoSubId -DocumentoEmAcaoId $veiculoDocSubId
    Approve-Submission -Session $analistaSession -SubmissaoId $veiculoSubId

    Assert-RowStatuses -Label 'MeusVeiculos approved' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusVeiculos').Body -Needle $placa -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    $veiculoEditApproved = Get-PageAndToken -Session $empresaSession -Path "/Empresa/EditarVeiculo/$placa"
    Assert-PageStatuses -Label 'EditarVeiculo approved' -Html $veiculoEditApproved.Page.Body -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    Assert-RowStatuses -Label 'CentralConformidade veiculo approved' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/CentralConformidade').Body -Needle $placa -SituacaoOperacional 'Aprovado' -UltimaSubmissao 'Aprovada'
    Assert-Status -Label 'POST Save veiculo draft' -Actual (Invoke-LivePostForm -Session $empresaSession -Path "/Empresa/EditarVeiculo/$placa" -Fields @{ '__RequestVerificationToken' = $veiculoEditApproved.Token; 'Input.Placa' = $placa; 'Input.Modelo' = $veiculoModeloDraft; 'Input.ChassiNumero' = "CHASSI$suffix"; 'Input.Renavan' = "REN$suffix"; 'Input.AnoFabricacao' = '2020'; 'Input.ModeloAno' = '2021'; 'Input.VeiculoCombustivelNome' = 'DIESEL'; 'Input.NumeroLugares' = '20'; 'Input.PotenciaMotor' = '180' }).StatusCode -Allowed @(200)
    $veiculoModeloLive = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select modelo from geral.veiculo where placa = '$placa';"
    if ($veiculoModeloLive -ne $veiculoModeloAprovado) { throw 'Live vehicle model changed before the new draft was approved.' }
    Assert-PageStatuses -Label 'EditarVeiculo draft' -Html (Invoke-LiveGet -Session $empresaSession -Path "/Empresa/EditarVeiculo/$placa").Body -SituacaoOperacional 'Aprovado' -UltimaSubmissao $TextEmEdicao
    Assert-RowStatuses -Label 'MeusVeiculos draft' -Html (Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusVeiculos').Body -Needle $placa -SituacaoOperacional 'Aprovado' -UltimaSubmissao $TextEmEdicao
    $report.Add([pscustomobject]@{ Flow = 'Veiculo create, approve and draft'; Result = 'OK'; Reference = 'Aprovado/Aprovada -> Aprovado/Em edicao' })

    Write-Host ''
    $report | Format-Table -AutoSize
    Write-Host ''
    Write-Host 'Live flow exhaustive OK' -ForegroundColor Green
}
finally {
    foreach ($session in @($empresaSession, $analistaSession, $adminSession)) {
        if ($session) { Close-LiveSession -Session $session }
    }
    foreach ($pdf in @($companyPdf, $motoristaPdf1, $motoristaPdf2, $veiculoPdf)) {
        if ($pdf) { Remove-Item -LiteralPath $pdf -ErrorAction SilentlyContinue }
    }
}
