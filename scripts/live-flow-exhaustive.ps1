param(
    [string]$BaseUrl = "https://localhost:7143",
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
Add-Type -AssemblyName System.Net

function New-TempFilePath {
    param([string]$Suffix)
    Join-Path ([System.IO.Path]::GetTempPath()) ("eva-live-" + [guid]::NewGuid().ToString("N") + $Suffix)
}

function New-PdfFile {
    param([string]$Name)
    $path = New-TempFilePath ("-" + $Name)
    $bytes = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n1 0 obj`n<<>>`nendobj`ntrailer`n<<>>`n%%EOF")
    [System.IO.File]::WriteAllBytes($path, $bytes)
    $path
}

function Get-StatusCodeFromHeaders {
    param([string]$Headers)
    $firstLine = ($Headers -split "`r?`n" | Select-Object -First 1)
    if ($firstLine -match 'HTTP/\S+\s+(\d{3})') { return [int]$matches[1] }
    throw "Could not parse HTTP status."
}

function Get-HeaderValue {
    param([string]$Headers, [string]$HeaderName)
    $match = [regex]::Match($Headers, "(?im)^" + [regex]::Escape($HeaderName) + ":\s*(.+)$")
    if ($match.Success) { return $match.Groups[1].Value.Trim() }
    $null
}

function Invoke-CurlGet {
    param([string]$Url, [string]$CookieFile)
    $headersFile = New-TempFilePath ".headers"
    $bodyFile = New-TempFilePath ".body"
    try {
        & curl.exe -k -sS -D $headersFile -o $bodyFile -b $CookieFile -c $CookieFile $Url | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "curl GET failed for $Url" }
        $headers = Get-Content -Raw -LiteralPath $headersFile
        @{
            Headers = $headers
            Body = Get-Content -Raw -LiteralPath $bodyFile
            StatusCode = Get-StatusCodeFromHeaders -Headers $headers
            Location = Get-HeaderValue -Headers $headers -HeaderName "Location"
        }
    }
    finally {
        Remove-Item -LiteralPath $headersFile, $bodyFile -ErrorAction SilentlyContinue
    }
}

function Invoke-CurlPostForm {
    param([string]$Url, [string]$CookieFile, [hashtable]$Fields)
    $headersFile = New-TempFilePath ".headers"
    $bodyFile = New-TempFilePath ".body"
    $args = @("-k", "-sS", "-D", $headersFile, "-o", $bodyFile, "-b", $CookieFile, "-c", $CookieFile, "-X", "POST")
    foreach ($key in $Fields.Keys) {
        $args += @("--data-urlencode", "$key=$($Fields[$key])")
    }
    $args += $Url
    try {
        & curl.exe @args | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "curl POST failed for $Url" }
        $headers = Get-Content -Raw -LiteralPath $headersFile
        @{
            Headers = $headers
            Body = Get-Content -Raw -LiteralPath $bodyFile
            StatusCode = Get-StatusCodeFromHeaders -Headers $headers
            Location = Get-HeaderValue -Headers $headers -HeaderName "Location"
        }
    }
    finally {
        Remove-Item -LiteralPath $headersFile, $bodyFile -ErrorAction SilentlyContinue
    }
}

function Invoke-CurlMultipart {
    param([string]$Url, [string]$CookieFile, [hashtable]$Fields, [hashtable]$Files)
    $headersFile = New-TempFilePath ".headers"
    $bodyFile = New-TempFilePath ".body"
    $args = @("-k", "-sS", "-D", $headersFile, "-o", $bodyFile, "-b", $CookieFile, "-c", $CookieFile, "-X", "POST")
    foreach ($key in $Fields.Keys) {
        $args += @("-F", "$key=$($Fields[$key])")
    }
    foreach ($key in $Files.Keys) {
        $args += @("-F", "$key=@$($Files[$key]);type=application/pdf")
    }
    $args += $Url
    try {
        & curl.exe @args | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "curl multipart failed for $Url" }
        $headers = Get-Content -Raw -LiteralPath $headersFile
        @{
            Headers = $headers
            Body = Get-Content -Raw -LiteralPath $bodyFile
            StatusCode = Get-StatusCodeFromHeaders -Headers $headers
            Location = Get-HeaderValue -Headers $headers -HeaderName "Location"
        }
    }
    finally {
        Remove-Item -LiteralPath $headersFile, $bodyFile -ErrorAction SilentlyContinue
    }
}

function Get-HiddenFieldValue {
    param([string]$Html, [string]$FieldName)
    $pattern = 'name="' + [regex]::Escape($FieldName) + '"[^>]*value="([^"]+)"'
    $match = [regex]::Match($Html, $pattern)
    if (-not $match.Success) { throw "Hidden field '$FieldName' not found." }
    $match.Groups[1].Value
}

function Assert-Status {
    param([string]$Label, [int]$Actual, [int[]]$Allowed)
    if ($Allowed -notcontains $Actual) {
        throw "$Label returned $Actual; expected: $($Allowed -join ', ')"
    }
}

function Assert-Contains {
    param([string]$Label, [string]$Text, [string]$Needle)
    $decoded = [System.Net.WebUtility]::HtmlDecode($Text)
    if ($decoded -notmatch [regex]::Escape($Needle)) {
        throw "$Label does not contain '$Needle'."
    }
}

function Login-User {
    param([string]$Email, [string]$ExpectedLocation)
    $cookieFile = New-TempFilePath ".cookies"
    Set-Content -LiteralPath $cookieFile -Value "" | Out-Null
    $loginPage = Invoke-CurlGet -Url "$BaseUrl/Login" -CookieFile $cookieFile
    Assert-Status -Label "GET /Login for $Email" -Actual $loginPage.StatusCode -Allowed @(200)
    $token = Get-HiddenFieldValue -Html $loginPage.Body -FieldName "__RequestVerificationToken"
    $posted = Invoke-CurlPostForm -Url "$BaseUrl/Login" -CookieFile $cookieFile -Fields @{
        "__RequestVerificationToken" = $token
        "Input.Email" = $Email
        "Input.Senha" = $Password
    }
    Assert-Status -Label "POST /Login for $Email" -Actual $posted.StatusCode -Allowed @(302)
    if ($ExpectedLocation -and ($posted.Location -notlike "*$ExpectedLocation")) {
        throw "Login for $Email redirected to '$($posted.Location)', expected '$ExpectedLocation'."
    }
    $cookieFile
}

function Get-DbScalar {
    param([string]$Sql)
    $env:PGPASSWORD = $DbPassword
    $result = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "Database query failed." }
    ($result | Out-String).Trim()
}

function Invoke-DbNonQuery {
    param([string]$Sql)
    $env:PGPASSWORD = $DbPassword
    $null = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "Database command failed." }
}

function Get-PageAndToken {
    param([string]$CookieFile, [string]$Path)
    $page = Invoke-CurlGet -Url "$BaseUrl$Path" -CookieFile $CookieFile
    Assert-Status -Label "GET $Path" -Actual $page.StatusCode -Allowed @(200)
    @{
        Page = $page
        Token = Get-HiddenFieldValue -Html $page.Body -FieldName "__RequestVerificationToken"
    }
}

function Get-LatestSubmissionId {
    param([string]$EntityType, [string]$EntityId)
    Get-DbScalar "select id from eventual.submissao where entidade_tipo = '$EntityType' and entidade_id = '$EntityId' order by id desc limit 1;"
}

function Get-LatestSubmissionDocId {
    param([int]$SubmissionId, [string]$TipoNome)
    Get-DbScalar "select id from eventual.submissao_documento where submissao_id = $SubmissionId and documento_tipo_nome = '$TipoNome' and ativo_na_submissao = true order by id desc limit 1;"
}

function Get-LatestSubmissionDocumentId {
    param([int]$SubmissionId, [string]$TipoNome)
    Get-DbScalar "select documento_id from eventual.submissao_documento where submissao_id = $SubmissionId and documento_tipo_nome = '$TipoNome' and ativo_na_submissao = true order by id desc limit 1;"
}

function New-UniqueCpf {
    do {
        $cpf = -join ((1..11) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
        $exists = Get-DbScalar "select count(*) from eventual.motorista where cpf = '$cpf';"
    } while ($exists -ne "0")
    $cpf
}

function New-UniquePlate {
    do {
        $letters = -join ((1..3) | ForEach-Object { [char](Get-Random -Minimum 65 -Maximum 91) })
        $digits = -join ((1..4) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
        $placa = $letters + $digits
        $exists = Get-DbScalar "select count(*) from geral.veiculo where placa = '$placa';"
    } while ($exists -ne "0")
    $placa
}

function Get-ListFragment {
    param([string]$Html, [string]$Needle)
    $decoded = [System.Net.WebUtility]::HtmlDecode($Html)
    $index = $decoded.IndexOf($Needle)
    if ($index -lt 0) { return $null }
    $start = [Math]::Max(0, $index - 500)
    $length = [Math]::Min(2000, $decoded.Length - $start)
    $decoded.Substring($start, $length)
}

$empresaCookies = $null
$analistaCookies = $null
$adminCookies = $null
$companyPdf = $null
$motoristaPdf1 = $null
$motoristaPdf2 = $null
$veiculoPdf = $null

try {
    $suffix = Get-Date -Format "yyyyMMddHHmmss"
    $empresaCookies = Login-User -Email $EmpresaEmail -ExpectedLocation "/Empresa/MinhaEmpresa"
    $analistaCookies = Login-User -Email $AnalistaEmail -ExpectedLocation "/Metroplan/Analista"
    $adminCookies = Login-User -Email $AdminEmail -ExpectedLocation "/Metroplan/Admin"

    $report = New-Object System.Collections.Generic.List[object]

    $companyPdf = New-PdfFile "company-$suffix.pdf"
    $motoristaPdf1 = New-PdfFile "motorista-$suffix-1.pdf"
    $motoristaPdf2 = New-PdfFile "motorista-$suffix-2.pdf"
    $veiculoPdf = New-PdfFile "veiculo-$suffix.pdf"

    $empresaCnpj = Get-DbScalar "select empresa_cnpj from web.usuario where email = '$EmpresaEmail';"
    $empresaNome = Get-DbScalar "select nome from geral.empresa where cnpj = '$empresaCnpj';"

    Invoke-DbNonQuery @"
delete from eventual.submissao_evento
where submissao_id in (
    select id from eventual.submissao
    where entidade_tipo = 'EMPRESA'
      and entidade_id = '$empresaCnpj'
      and status <> 'APROVADA'
);

delete from eventual.submissao_documento
where submissao_id in (
    select id from eventual.submissao
    where entidade_tipo = 'EMPRESA'
      and entidade_id = '$empresaCnpj'
      and status <> 'APROVADA'
);

delete from eventual.submissao_dados
where submissao_id in (
    select id from eventual.submissao
    where entidade_tipo = 'EMPRESA'
      and entidade_id = '$empresaCnpj'
      and status <> 'APROVADA'
);

delete from eventual.submissao
where entidade_tipo = 'EMPRESA'
  and entidade_id = '$empresaCnpj'
  and status <> 'APROVADA';
"@

    foreach ($path in @(
        "/Empresa/MinhaEmpresa",
        "/Empresa/EditarEmpresa",
        "/Empresa/MeusMotoristas",
        "/Empresa/MeusVeiculos",
        "/Empresa/MinhasViagens",
        "/Metroplan/Analista",
        "/Metroplan/Admin"
    )) {
        $who = if ($path -like "/Metroplan/Analista*") { $analistaCookies } elseif ($path -like "/Metroplan/Admin*") { $adminCookies } else { $empresaCookies }
        $page = Invoke-CurlGet -Url "$BaseUrl$path" -CookieFile $who
        Assert-Status -Label "GET $path" -Actual $page.StatusCode -Allowed @(200)
    }
    $report.Add([pscustomobject]@{ Flow = "Basic route availability"; Result = "OK"; Reference = "empresa, analista, admin" })

    # Company reject flow
    $empresaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarEmpresa"
    $novoNomeEmpresa = "$empresaNome TESTE $suffix"
    $saveEmpresa = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarEmpresa" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $empresaEdit.Token
        "Input.Cnpj" = $empresaCnpj
        "Input.Nome" = $novoNomeEmpresa
    }
    Assert-Status -Label "POST EditarEmpresa save" -Actual $saveEmpresa.StatusCode -Allowed @(302)

    $empresaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarEmpresa"
    $uploadEmpresa = Invoke-CurlMultipart -Url "$BaseUrl/Empresa/EditarEmpresa?handler=Upload" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $empresaEdit.Token
        "TipoDocumentoUpload" = "CARTAO_CNPJ"
    } -Files @{
        "UploadArquivo" = $companyPdf
    }
    Assert-Status -Label "POST EditarEmpresa upload" -Actual $uploadEmpresa.StatusCode -Allowed @(200)
    Assert-Contains -Label "Empresa docs partial" -Text $uploadEmpresa.Body -Needle "company-$suffix.pdf"

    $empresaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarEmpresa"
    $sendEmpresa = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarEmpresa?handler=Enviar" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $empresaEdit.Token
    }
    Assert-Status -Label "POST EditarEmpresa send" -Actual $sendEmpresa.StatusCode -Allowed @(302)

    $empresaSubId = [int](Get-LatestSubmissionId -EntityType "EMPRESA" -EntityId $empresaCnpj)
    $empresaDocSubId = [int](Get-LatestSubmissionDocId -SubmissionId $empresaSubId -TipoNome "CARTAO_CNPJ")
    if ((Get-DbScalar "select status from eventual.submissao where id = $empresaSubId;") -ne "AGUARDANDO_ANALISE") {
        throw "Company submission did not move to AGUARDANDO_ANALISE."
    }

    $reviewEmpresa = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$empresaSubId"
    $startEmpresa = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=Iniciar" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewEmpresa.Token
        "SubmissaoId" = $empresaSubId
    }
    Assert-Status -Label "POST Revisao empresa iniciar" -Actual $startEmpresa.StatusCode -Allowed @(302)

    $reviewEmpresa = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$empresaSubId"
    $rejEmpresaDados = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarDados" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewEmpresa.Token
        "SubmissaoId" = $empresaSubId
        "MotivoRejeicaoDados" = "Company data rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao empresa rejeitar dados" -Actual $rejEmpresaDados.StatusCode -Allowed @(302)

    $reviewEmpresa = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$empresaSubId"
    $rejEmpresaDoc = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarDocumento" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewEmpresa.Token
        "SubmissaoId" = $empresaSubId
        "DocumentoEmAcaoId" = $empresaDocSubId
        "MotivoRejeicaoDocumento" = "Company document rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao empresa rejeitar documento" -Actual $rejEmpresaDoc.StatusCode -Allowed @(302)

    $reviewEmpresa = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$empresaSubId"
    $rejEmpresaSub = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarSubmissao" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewEmpresa.Token
        "SubmissaoId" = $empresaSubId
        "ObservacaoRejeicaoSubmissao" = "Company submission rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao empresa rejeitar submissao" -Actual $rejEmpresaSub.StatusCode -Allowed @(302)
    if ((Get-DbScalar "select status from eventual.submissao where id = $empresaSubId;") -ne "REJEITADA") {
        throw "Company submission did not end REJEITADA."
    }

    $empresaRejectedPage = Invoke-CurlGet -Url "$BaseUrl/Empresa/EditarEmpresa" -CookieFile $empresaCookies
    Assert-Contains -Label "EditarEmpresa data rejection reason" -Text $empresaRejectedPage.Body -Needle "Company data rejected in live flow test"
    Assert-Contains -Label "EditarEmpresa doc rejection reason" -Text $empresaRejectedPage.Body -Needle "Company document rejected in live flow test"
    $report.Add([pscustomobject]@{ Flow = "Company reject flow"; Result = "OK"; Reference = "submissao $empresaSubId" })

    # Motorista create -> reject -> fix -> approve
    $motoristaCpf = New-UniqueCpf
    $motoristaNome = "Codex Motorista $suffix"
    $motoristaEmail = "codex-motorista-$suffix@example.com"
    $motoristaCnh = -join ((1..9) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })

    $novoMotorista = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/NovoMotorista"
    $createMotorista = Invoke-CurlMultipart -Url "$BaseUrl/Empresa/NovoMotorista" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $novoMotorista.Token
        "Input.Nome" = $motoristaNome
        "Input.Cpf" = $motoristaCpf
        "Input.Cnh" = $motoristaCnh
        "Input.Email" = $motoristaEmail
    } -Files @{
        "UploadCnh" = $motoristaPdf1
    }
    Assert-Status -Label "POST NovoMotorista" -Actual $createMotorista.StatusCode -Allowed @(302)

    $motoristaId = [int](Get-DbScalar "select id from eventual.motorista where cpf = '$motoristaCpf';")
    $motoristaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Contains -Label "EditarMotorista initial upload" -Text $motoristaEdit.Page.Body -Needle "motorista-$suffix-1.pdf"

    $sendMotorista = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarMotorista/${motoristaId}?handler=Enviar" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $motoristaEdit.Token
    }
    Assert-Status -Label "POST EditarMotorista enviar" -Actual $sendMotorista.StatusCode -Allowed @(302)

    $motoristaSubId = [int](Get-LatestSubmissionId -EntityType "MOTORISTA" -EntityId $motoristaId)
    $motoristaDocSubId = [int](Get-LatestSubmissionDocId -SubmissionId $motoristaSubId -TipoNome "CNH")
    $motoristaDocId = [int](Get-LatestSubmissionDocumentId -SubmissionId $motoristaSubId -TipoNome "CNH")
    if ((Get-DbScalar "select status from eventual.submissao where id = $motoristaSubId;") -ne "AGUARDANDO_ANALISE") {
        throw "Motorista submission did not move to AGUARDANDO_ANALISE."
    }

    $reviewMotorista = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId"
    $startMotorista = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=Iniciar" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista.Token
        "SubmissaoId" = $motoristaSubId
    }
    Assert-Status -Label "POST Revisao motorista iniciar" -Actual $startMotorista.StatusCode -Allowed @(302)

    $reviewMotorista = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId"
    $rejMotoristaDados = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarDados" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista.Token
        "SubmissaoId" = $motoristaSubId
        "MotivoRejeicaoDados" = "Motorista data rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao motorista rejeitar dados" -Actual $rejMotoristaDados.StatusCode -Allowed @(302)

    $reviewMotorista = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId"
    $rejMotoristaDoc = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarDocumento" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista.Token
        "SubmissaoId" = $motoristaSubId
        "DocumentoEmAcaoId" = $motoristaDocSubId
        "MotivoRejeicaoDocumento" = "Motorista CNH rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao motorista rejeitar documento" -Actual $rejMotoristaDoc.StatusCode -Allowed @(302)

    $reviewMotorista = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId"
    $rejMotoristaSub = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=RejeitarSubmissao" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista.Token
        "SubmissaoId" = $motoristaSubId
        "ObservacaoRejeicaoSubmissao" = "Motorista submission rejected in live flow test"
    }
    Assert-Status -Label "POST Revisao motorista rejeitar submissao" -Actual $rejMotoristaSub.StatusCode -Allowed @(302)
    if ((Get-DbScalar "select status from eventual.submissao where id = $motoristaSubId;") -ne "REJEITADA") {
        throw "Motorista submission did not end REJEITADA."
    }

    $motoristasList = Invoke-CurlGet -Url "$BaseUrl/Empresa/MeusMotoristas" -CookieFile $empresaCookies
    $motoristaFragment = Get-ListFragment -Html $motoristasList.Body -Needle $motoristaCpf
    if (-not $motoristaFragment) { throw "Motorista row not found in list after rejection." }
    if ($motoristaFragment -notmatch "Rejeitado") { throw "Motorista list row did not show Rejeitado after analyst rejection." }

    $motoristaEditRejected = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    Assert-Contains -Label "EditarMotorista data rejection reason" -Text $motoristaEditRejected.Page.Body -Needle "Motorista data rejected in live flow test"
    Assert-Contains -Label "EditarMotorista doc rejection reason" -Text $motoristaEditRejected.Page.Body -Needle "Motorista CNH rejected in live flow test"

    $deleteMotoristaDoc = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarMotorista/${motoristaId}?handler=DeleteDoc" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $motoristaEditRejected.Token
        "docId" = $motoristaDocId
    }
    Assert-Status -Label "POST EditarMotorista delete doc" -Actual $deleteMotoristaDoc.StatusCode -Allowed @(200)
    Assert-Contains -Label "EditarMotorista after delete" -Text $deleteMotoristaDoc.Body -Needle "Nenhum documento anexado."

    $motoristaEditNoDoc = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    $uploadMotoristaDoc = Invoke-CurlMultipart -Url "$BaseUrl/Empresa/EditarMotorista/${motoristaId}?handler=Upload" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $motoristaEditNoDoc.Token
    } -Files @{
        "UploadArquivo" = $motoristaPdf2
    }
    Assert-Status -Label "POST EditarMotorista upload replacement" -Actual $uploadMotoristaDoc.StatusCode -Allowed @(200)
    Assert-Contains -Label "EditarMotorista replacement upload partial" -Text $uploadMotoristaDoc.Body -Needle "motorista-$suffix-2.pdf"

    $motoristaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    $saveMotorista = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarMotorista/$motoristaId" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $motoristaEdit.Token
        "Input.Id" = $motoristaId
        "Input.Nome" = "$motoristaNome aprovado"
        "Input.Cpf" = $motoristaCpf
        "Input.Cnh" = $motoristaCnh
        "Input.Email" = $motoristaEmail
    }
    Assert-Status -Label "POST EditarMotorista save" -Actual $saveMotorista.StatusCode -Allowed @(302)

    $motoristaEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    $resendMotorista = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarMotorista/${motoristaId}?handler=Enviar" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $motoristaEdit.Token
    }
    Assert-Status -Label "POST EditarMotorista resend" -Actual $resendMotorista.StatusCode -Allowed @(302)

    $motoristaSubId2 = [int](Get-LatestSubmissionId -EntityType "MOTORISTA" -EntityId $motoristaId)
    if ($motoristaSubId2 -le $motoristaSubId) {
        throw "A new motorista submission was not created after rejection."
    }
    $motoristaDocSubId2 = [int](Get-LatestSubmissionDocId -SubmissionId $motoristaSubId2 -TipoNome "CNH")

    $reviewMotorista2 = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId2"
    $startMotorista2 = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=Iniciar" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista2.Token
        "SubmissaoId" = $motoristaSubId2
    }
    Assert-Status -Label "POST Revisao motorista reiniciar" -Actual $startMotorista2.StatusCode -Allowed @(302)

    $reviewMotorista2 = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId2"
    $approveMotoristaDados = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarDados" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista2.Token
        "SubmissaoId" = $motoristaSubId2
    }
    Assert-Status -Label "POST Revisao motorista aprovar dados" -Actual $approveMotoristaDados.StatusCode -Allowed @(302)

    $reviewMotorista2 = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId2"
    $approveMotoristaDoc = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarDocumento" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista2.Token
        "SubmissaoId" = $motoristaSubId2
        "DocumentoEmAcaoId" = $motoristaDocSubId2
        "ValidadeDocumento" = "2030-12-31"
    }
    Assert-Status -Label "POST Revisao motorista aprovar documento" -Actual $approveMotoristaDoc.StatusCode -Allowed @(302)

    $reviewMotorista2 = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$motoristaSubId2"
    $approveMotoristaSub = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarSubmissao" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewMotorista2.Token
        "SubmissaoId" = $motoristaSubId2
    }
    Assert-Status -Label "POST Revisao motorista aprovar submissao" -Actual $approveMotoristaSub.StatusCode -Allowed @(302)

    if ((Get-DbScalar "select status from eventual.submissao where id = $motoristaSubId2;") -ne "APROVADA") {
        throw "Motorista final submission did not end APROVADA."
    }
    if ([int](Get-DbScalar "select count(*) from eventual.entidade_documento_atual where entidade_tipo = 'MOTORISTA' and entidade_id = '$motoristaId' and documento_tipo_nome = 'CNH';") -lt 1) {
        throw "Motorista approval did not promote CNH into entidade_documento_atual."
    }

    $motoristasListFinal = Invoke-CurlGet -Url "$BaseUrl/Empresa/MeusMotoristas" -CookieFile $empresaCookies
    $motoristaFragmentFinal = Get-ListFragment -Html $motoristasListFinal.Body -Needle $motoristaCpf
    if (-not $motoristaFragmentFinal) { throw "Motorista row not found in list after approval." }
    if ($motoristaFragmentFinal -notmatch "Legalizado") { throw "Motorista list row did not show Legalizado after approval." }
    $report.Add([pscustomobject]@{ Flow = "Motorista reject, fix, approve"; Result = "OK"; Reference = "motorista $motoristaId / submissao $motoristaSubId2" })

    # Veiculo create -> approve
    $placa = New-UniquePlate
    $novoVeiculo = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/NovoVeiculo"
    $createVeiculo = Invoke-CurlMultipart -Url "$BaseUrl/Empresa/NovoVeiculo" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $novoVeiculo.Token
        "Input.Placa" = $placa
        "Input.Modelo" = "Codex Veiculo $suffix"
        "Input.ChassiNumero" = "CHASSI$suffix"
        "Input.Renavan" = "REN$suffix"
        "Input.AnoFabricacao" = "2020"
        "Input.ModeloAno" = "2021"
        "Input.VeiculoCombustivelNome" = "DIESEL"
        "Input.NumeroLugares" = "20"
        "Input.PotenciaMotor" = "180"
    } -Files @{
        "UploadCrlv" = $veiculoPdf
    }
    Assert-Status -Label "POST NovoVeiculo" -Actual $createVeiculo.StatusCode -Allowed @(302)

    $veiculoEdit = Get-PageAndToken -CookieFile $empresaCookies -Path "/Empresa/EditarVeiculo/$placa"
    Assert-Contains -Label "EditarVeiculo initial upload" -Text $veiculoEdit.Page.Body -Needle "veiculo-$suffix.pdf"

    $sendVeiculo = Invoke-CurlPostForm -Url "$BaseUrl/Empresa/EditarVeiculo/${placa}?handler=Enviar" -CookieFile $empresaCookies -Fields @{
        "__RequestVerificationToken" = $veiculoEdit.Token
    }
    Assert-Status -Label "POST EditarVeiculo enviar" -Actual $sendVeiculo.StatusCode -Allowed @(302)

    $veiculoSubId = [int](Get-LatestSubmissionId -EntityType "VEICULO" -EntityId $placa)
    $veiculoDocSubId = [int](Get-LatestSubmissionDocId -SubmissionId $veiculoSubId -TipoNome "CRLV")

    $reviewVeiculo = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$veiculoSubId"
    $startVeiculo = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=Iniciar" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewVeiculo.Token
        "SubmissaoId" = $veiculoSubId
    }
    Assert-Status -Label "POST Revisao veiculo iniciar" -Actual $startVeiculo.StatusCode -Allowed @(302)

    $reviewVeiculo = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$veiculoSubId"
    $approveVeiculoDados = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarDados" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewVeiculo.Token
        "SubmissaoId" = $veiculoSubId
    }
    Assert-Status -Label "POST Revisao veiculo aprovar dados" -Actual $approveVeiculoDados.StatusCode -Allowed @(302)

    $reviewVeiculo = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$veiculoSubId"
    $approveVeiculoDoc = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarDocumento" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewVeiculo.Token
        "SubmissaoId" = $veiculoSubId
        "DocumentoEmAcaoId" = $veiculoDocSubId
        "ValidadeDocumento" = "2030-12-31"
    }
    Assert-Status -Label "POST Revisao veiculo aprovar documento" -Actual $approveVeiculoDoc.StatusCode -Allowed @(302)

    $reviewVeiculo = Get-PageAndToken -CookieFile $analistaCookies -Path "/Metroplan/Analista/Revisao?submissaoId=$veiculoSubId"
    $approveVeiculoSub = Invoke-CurlPostForm -Url "$BaseUrl/Metroplan/Analista/Revisao?handler=AprovarSubmissao" -CookieFile $analistaCookies -Fields @{
        "__RequestVerificationToken" = $reviewVeiculo.Token
        "SubmissaoId" = $veiculoSubId
    }
    Assert-Status -Label "POST Revisao veiculo aprovar submissao" -Actual $approveVeiculoSub.StatusCode -Allowed @(302)

    if ((Get-DbScalar "select status from eventual.submissao where id = $veiculoSubId;") -ne "APROVADA") {
        throw "Veiculo final submission did not end APROVADA."
    }
    if ([int](Get-DbScalar "select count(*) from eventual.entidade_documento_atual where entidade_tipo = 'VEICULO' and entidade_id = '$placa' and documento_tipo_nome = 'CRLV';") -lt 1) {
        throw "Veiculo approval did not promote CRLV into entidade_documento_atual."
    }

    $veiculosList = Invoke-CurlGet -Url "$BaseUrl/Empresa/MeusVeiculos" -CookieFile $empresaCookies
    $veiculoFragment = Get-ListFragment -Html $veiculosList.Body -Needle $placa
    if (-not $veiculoFragment) { throw "Veiculo row not found in list after approval." }
    if ($veiculoFragment -notmatch "Legalizado") { throw "Veiculo list row did not show Legalizado after approval." }
    $report.Add([pscustomobject]@{ Flow = "Veiculo create and approve"; Result = "OK"; Reference = "placa $placa / submissao $veiculoSubId" })

    Write-Host ""
    $report | Format-Table -AutoSize
    Write-Host ""
    Write-Host "Live flow exhaustive OK" -ForegroundColor Green
}
finally {
    foreach ($file in @($empresaCookies, $analistaCookies, $adminCookies, $companyPdf, $motoristaPdf1, $motoristaPdf2, $veiculoPdf)) {
        if ($file) {
            Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
        }
    }
}
