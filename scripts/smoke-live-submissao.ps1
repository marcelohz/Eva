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
$TextDocumentacao = U @(68,111,99,117,109,101,110,116,97,231,227,111)
$TextDocumentacaoVeiculo = U @(68,111,99,117,109,101,110,116,97,231,227,111,32,100,111,32,86,101,237,99,117,108,111)
$TextAuditoriaSubmissao = U @(65,117,100,105,116,111,114,105,97,32,100,97,32,115,117,98,109,105,115,115,227,111)
$TextDecisaoFinal = U @(68,101,99,105,115,227,111,32,102,105,110,97,108)

$empresaSession = $null
$analistaSession = $null
$adminSession = $null

try {
    $empresaSession = New-LiveSession -BaseUrl $BaseUrl
    $analistaSession = New-LiveSession -BaseUrl $BaseUrl
    $adminSession = New-LiveSession -BaseUrl $BaseUrl

    Login-LiveUser -Session $empresaSession -Email $EmpresaEmail -Password $Password -ExpectedLocation '/Empresa/MinhaEmpresa'
    Login-LiveUser -Session $analistaSession -Email $AnalistaEmail -Password $Password -ExpectedLocation '/Metroplan/Analista'
    Login-LiveUser -Session $adminSession -Email $AdminEmail -Password $Password -ExpectedLocation '/Metroplan/Admin'

    $empresaCnpj = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select empresa_cnpj from web.usuario where email = '$EmpresaEmail';"
    $motoristaId = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select id from eventual.motorista where empresa_cnpj = '$empresaCnpj' order by id limit 1;"
    $veiculoPlaca = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select placa from geral.veiculo where empresa_cnpj = '$empresaCnpj' order by placa limit 1;"
    $submissaoAnalistaId = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select id from eventual.submissao where status in ('AGUARDANDO_ANALISE','EM_ANALISE') order by id desc limit 1;"

    $checks = New-Object System.Collections.Generic.List[object]
    foreach ($path in @('/Empresa/MinhaEmpresa', '/Empresa/EditarEmpresa', '/Empresa/MeusMotoristas', '/Empresa/MeusVeiculos', '/Empresa/CentralConformidade')) {
        $page = Invoke-LiveGet -Session $empresaSession -Path $path
        Assert-Status -Label "GET $path" -Actual $page.StatusCode -Allowed @(200)
        $checks.Add([pscustomobject]@{ Usuario = $EmpresaEmail; Path = $path; Status = $page.StatusCode })
    }

    $editarEmpresa = Invoke-LiveGet -Session $empresaSession -Path '/Empresa/EditarEmpresa'
    Assert-Contains -Label 'EditarEmpresa' -Text $editarEmpresa.Body -Needle $TextSituacaoOperacional
    Assert-Contains -Label 'EditarEmpresa' -Text $editarEmpresa.Body -Needle $TextUltimaSubmissao
    Assert-Contains -Label 'EditarEmpresa' -Text $editarEmpresa.Body -Needle 'Dados Cadastrais'
    Assert-Contains -Label 'EditarEmpresa' -Text $editarEmpresa.Body -Needle $TextDocumentacao

    $meusMotoristas = Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusMotoristas'
    Assert-Contains -Label 'MeusMotoristas' -Text $meusMotoristas.Body -Needle $TextSituacaoOperacional
    Assert-Contains -Label 'MeusMotoristas' -Text $meusMotoristas.Body -Needle $TextUltimaSubmissao

    $meusVeiculos = Invoke-LiveGet -Session $empresaSession -Path '/Empresa/MeusVeiculos'
    Assert-Contains -Label 'MeusVeiculos' -Text $meusVeiculos.Body -Needle $TextSituacaoOperacional
    Assert-Contains -Label 'MeusVeiculos' -Text $meusVeiculos.Body -Needle $TextUltimaSubmissao

    $centralConformidade = Invoke-LiveGet -Session $empresaSession -Path '/Empresa/CentralConformidade'
    Assert-Contains -Label 'CentralConformidade' -Text $centralConformidade.Body -Needle 'Central de Conformidade'
    Assert-Contains -Label 'CentralConformidade' -Text $centralConformidade.Body -Needle $TextSituacaoOperacional
    Assert-Contains -Label 'CentralConformidade' -Text $centralConformidade.Body -Needle $TextUltimaSubmissao

    if ($motoristaId) {
        $editarMotorista = Invoke-LiveGet -Session $empresaSession -Path "/Empresa/EditarMotorista/$motoristaId"
        Assert-Status -Label "GET /Empresa/EditarMotorista/$motoristaId" -Actual $editarMotorista.StatusCode -Allowed @(200)
        Assert-Contains -Label 'EditarMotorista' -Text $editarMotorista.Body -Needle $TextSituacaoOperacional
        Assert-Contains -Label 'EditarMotorista' -Text $editarMotorista.Body -Needle $TextUltimaSubmissao
        Assert-Contains -Label 'EditarMotorista' -Text $editarMotorista.Body -Needle $TextDocumentacao
        $checks.Add([pscustomobject]@{ Usuario = $EmpresaEmail; Path = "/Empresa/EditarMotorista/$motoristaId"; Status = $editarMotorista.StatusCode })
    }

    if ($veiculoPlaca) {
        $editarVeiculo = Invoke-LiveGet -Session $empresaSession -Path "/Empresa/EditarVeiculo/$veiculoPlaca"
        Assert-Status -Label "GET /Empresa/EditarVeiculo/$veiculoPlaca" -Actual $editarVeiculo.StatusCode -Allowed @(200)
        Assert-Contains -Label 'EditarVeiculo' -Text $editarVeiculo.Body -Needle $TextSituacaoOperacional
        Assert-Contains -Label 'EditarVeiculo' -Text $editarVeiculo.Body -Needle $TextUltimaSubmissao
        Assert-Contains -Label 'EditarVeiculo' -Text $editarVeiculo.Body -Needle $TextDocumentacaoVeiculo
        $checks.Add([pscustomobject]@{ Usuario = $EmpresaEmail; Path = "/Empresa/EditarVeiculo/$veiculoPlaca"; Status = $editarVeiculo.StatusCode })
    }

    $analistaIndex = Invoke-LiveGet -Session $analistaSession -Path '/Metroplan/Analista'
    Assert-Status -Label 'GET /Metroplan/Analista' -Actual $analistaIndex.StatusCode -Allowed @(200)
    $checks.Add([pscustomobject]@{ Usuario = $AnalistaEmail; Path = '/Metroplan/Analista'; Status = $analistaIndex.StatusCode })

    if ($submissaoAnalistaId) {
        $reviewPage = Invoke-LiveGet -Session $analistaSession -Path "/Metroplan/Analista/Revisao?submissaoId=$submissaoAnalistaId"
        Assert-Status -Label 'GET Revisao' -Actual $reviewPage.StatusCode -Allowed @(200)
        Assert-Contains -Label 'Revisao' -Text $reviewPage.Body -Needle $TextAuditoriaSubmissao
        Assert-Contains -Label 'Revisao' -Text $reviewPage.Body -Needle $TextDecisaoFinal
        $checks.Add([pscustomobject]@{ Usuario = $AnalistaEmail; Path = "/Metroplan/Analista/Revisao?submissaoId=$submissaoAnalistaId"; Status = $reviewPage.StatusCode })
    }

    $adminIndex = Invoke-LiveGet -Session $adminSession -Path '/Metroplan/Admin'
    Assert-Status -Label 'GET /Metroplan/Admin' -Actual $adminIndex.StatusCode -Allowed @(200)
    $checks.Add([pscustomobject]@{ Usuario = $AdminEmail; Path = '/Metroplan/Admin'; Status = $adminIndex.StatusCode })

    Write-Host ''
    $checks | Format-Table -AutoSize
    Write-Host ''
    Write-Host 'Smoke live de submissao OK' -ForegroundColor Green
}
finally {
    foreach ($session in @($empresaSession, $analistaSession, $adminSession)) {
        if ($session) { Close-LiveSession -Session $session }
    }
}
