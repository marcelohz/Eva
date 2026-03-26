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
    return (Join-Path ([System.IO.Path]::GetTempPath()) ("eva-live-" + [guid]::NewGuid().ToString("N") + $Suffix))
}

function Invoke-CurlGet {
    param(
        [string]$Url,
        [string]$CookieFile
    )

    $headersFile = New-TempFilePath ".headers"
    $bodyFile = New-TempFilePath ".body"

    try {
        & curl.exe -k -sS -D $headersFile -o $bodyFile -b $CookieFile -c $CookieFile $Url | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "curl GET falhou para $Url"
        }

        return @{
            Headers = Get-Content -Raw -LiteralPath $headersFile
            Body = Get-Content -Raw -LiteralPath $bodyFile
            StatusCode = Get-StatusCodeFromHeaders -Headers (Get-Content -Raw -LiteralPath $headersFile)
        }
    }
    finally {
        Remove-Item -LiteralPath $headersFile, $bodyFile -ErrorAction SilentlyContinue
    }
}

function Invoke-CurlPostForm {
    param(
        [string]$Url,
        [string]$CookieFile,
        [hashtable]$Fields
    )

    $headersFile = New-TempFilePath ".headers"
    $bodyFile = New-TempFilePath ".body"

    $args = @("-k", "-sS", "-D", $headersFile, "-o", $bodyFile, "-b", $CookieFile, "-c", $CookieFile, "-X", "POST")
    foreach ($key in $Fields.Keys) {
        $args += @("--data-urlencode", "$key=$($Fields[$key])")
    }
    $args += $Url

    try {
        & curl.exe @args | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "curl POST falhou para $Url"
        }

        return @{
            Headers = Get-Content -Raw -LiteralPath $headersFile
            Body = Get-Content -Raw -LiteralPath $bodyFile
            StatusCode = Get-StatusCodeFromHeaders -Headers (Get-Content -Raw -LiteralPath $headersFile)
            Location = Get-HeaderValue -Headers (Get-Content -Raw -LiteralPath $headersFile) -HeaderName "Location"
        }
    }
    finally {
        Remove-Item -LiteralPath $headersFile, $bodyFile -ErrorAction SilentlyContinue
    }
}

function Get-StatusCodeFromHeaders {
    param([string]$Headers)

    $firstLine = ($Headers -split "`r?`n" | Select-Object -First 1)
    if ($firstLine -match 'HTTP/\S+\s+(\d{3})') {
        return [int]$matches[1]
    }

    throw "Nao foi possivel extrair o status HTTP."
}

function Get-HeaderValue {
    param(
        [string]$Headers,
        [string]$HeaderName
    )

    $match = [regex]::Match($Headers, "(?im)^" + [regex]::Escape($HeaderName) + ":\s*(.+)$")
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $null
}

function Get-HiddenFieldValue {
    param(
        [string]$Html,
        [string]$FieldName
    )

    $pattern = 'name="' + [regex]::Escape($FieldName) + '"[^>]*value="([^"]+)"'
    $match = [regex]::Match($Html, $pattern)
    if (-not $match.Success) {
        throw "Campo oculto '$FieldName' nao encontrado."
    }

    return $match.Groups[1].Value
}

function Assert-Status {
    param(
        [string]$Label,
        [int]$Actual,
        [int[]]$Allowed
    )

    if ($Allowed -notcontains $Actual) {
        throw "$Label retornou $Actual; esperado: $($Allowed -join ', ')"
    }
}

function Login-User {
    param(
        [string]$Email,
        [string]$Password,
        [string]$ExpectedLocation
    )

    $cookieFile = New-TempFilePath ".cookies"
    Set-Content -LiteralPath $cookieFile -Value "" | Out-Null

    $loginPage = Invoke-CurlGet -Url "$BaseUrl/Login" -CookieFile $cookieFile
    Assert-Status -Label "GET /Login para $Email" -Actual $loginPage.StatusCode -Allowed @(200)

    $token = Get-HiddenFieldValue -Html $loginPage.Body -FieldName "__RequestVerificationToken"
    $loginPost = Invoke-CurlPostForm -Url "$BaseUrl/Login" -CookieFile $cookieFile -Fields @{
        "__RequestVerificationToken" = $token
        "Input.Email" = $Email
        "Input.Senha" = $Password
    }

    Assert-Status -Label "POST /Login para $Email" -Actual $loginPost.StatusCode -Allowed @(302)
    if ($ExpectedLocation -and ($loginPost.Location -notlike "*$ExpectedLocation")) {
        throw "Login de $Email redirecionou para '$($loginPost.Location)', esperado '$ExpectedLocation'."
    }

    return $cookieFile
}

function Get-DbValue {
    param([string]$Sql)
    $env:PGPASSWORD = $DbPassword
    $result = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao consultar o banco."
    }
    return ($result | Out-String).Trim()
}

function Test-Page {
    param(
        [string]$CookieFile,
        [string]$Path,
        [string[]]$MustContain = @()
    )

    $result = Invoke-CurlGet -Url "$BaseUrl$Path" -CookieFile $CookieFile
    Assert-Status -Label "GET $Path" -Actual $result.StatusCode -Allowed @(200)
    foreach ($snippet in $MustContain) {
        if ($result.Body -notmatch [regex]::Escape($snippet)) {
            throw "GET $Path nao contem '$snippet'."
        }
    }

    return $result
}

function Get-ContextSnippet {
    param(
        [string]$Text,
        [string]$Needle,
        [int]$Before = 500,
        [int]$After = 1200
    )

    $index = $Text.IndexOf($Needle)
    if ($index -lt 0) {
        return $null
    }

    $start = [Math]::Max(0, $index - $Before)
    $length = [Math]::Min($Before + $After, $Text.Length - $start)
    return $Text.Substring($start, $length)
}

function Get-FirstMatchingStatus {
    param(
        [string]$HtmlFragment,
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if ($HtmlFragment -match [regex]::Escape($candidate)) {
            return $candidate
        }
    }

    return $null
}

$empresaCookies = $null
$analistaCookies = $null
$adminCookies = $null

try {
    $latestAnalystSubmission = Get-DbValue "select entidade_tipo || '|' || entidade_id from eventual.submissao where status in ('AGUARDANDO_ANALISE','EM_ANALISE') order by id desc limit 1;"
    $submissaoAnalistaId = Get-DbValue "select id from eventual.submissao where status in ('AGUARDANDO_ANALISE','EM_ANALISE') order by id desc limit 1;"
    $analistaId = Get-DbValue "select id from web.usuario where email = '$AnalistaEmail';"

    $submissionParts = $latestAnalystSubmission -split '\|', 2
    $submissionEntityType = if ($submissionParts.Length -ge 1) { $submissionParts[0] } else { "" }
    $submissionEntityId = if ($submissionParts.Length -ge 2) { $submissionParts[1] } else { "" }

    $motoristaId = if ($submissionEntityType -eq "MOTORISTA") {
        $submissionEntityId
    } else {
        Get-DbValue "select id from eventual.motorista where empresa_cnpj = (select empresa_cnpj from web.usuario where email = '$EmpresaEmail') order by id limit 1;"
    }

    $empresaCookies = Login-User -Email $EmpresaEmail -Password $Password -ExpectedLocation "/Empresa/MinhaEmpresa"
    $analistaCookies = Login-User -Email $AnalistaEmail -Password $Password -ExpectedLocation "/Metroplan/Analista"
    $adminCookies = Login-User -Email $AdminEmail -Password $Password -ExpectedLocation "/Metroplan/Admin"

    $checks = New-Object System.Collections.Generic.List[object]

    foreach ($path in @(
        "/Empresa/MinhaEmpresa",
        "/Empresa/EditarEmpresa",
        "/Empresa/MeusMotoristas",
        "/Empresa/MeusVeiculos",
        "/Empresa/EditarMotorista/$motoristaId"
    )) {
        $result = Test-Page -CookieFile $empresaCookies -Path $path
        $checks.Add([pscustomobject]@{ Usuario = $EmpresaEmail; Path = $path; Status = $result.StatusCode })
    }

    foreach ($path in @(
        "/Metroplan/Analista",
        "/Metroplan/Analista/Revisao?submissaoId=$submissaoAnalistaId"
    )) {
        $result = Test-Page -CookieFile $analistaCookies -Path $path
        $checks.Add([pscustomobject]@{ Usuario = $AnalistaEmail; Path = $path; Status = $result.StatusCode })
    }

    foreach ($path in @(
        "/Metroplan/Admin"
    )) {
        $result = Invoke-CurlGet -Url "$BaseUrl$path" -CookieFile $adminCookies
        $checks.Add([pscustomobject]@{ Usuario = $AdminEmail; Path = $path; Status = $result.StatusCode })
    }

    $motoristaPage = Test-Page -CookieFile $empresaCookies -Path "/Empresa/EditarMotorista/$motoristaId"
    $motoristasListPage = Test-Page -CookieFile $empresaCookies -Path "/Empresa/MeusMotoristas"
    $motoristaPageHtml = [System.Net.WebUtility]::HtmlDecode($motoristaPage.Body)
    $motoristasListHtml = [System.Net.WebUtility]::HtmlDecode($motoristasListPage.Body)
    $motoristaSignals = @()
    foreach ($signal in @("Rejeitado", "Em Análise", "CNH", "Motivo da rejeição", "Atenção (Cadastro Rejeitado)")) {
        if ($motoristaPageHtml -match [regex]::Escape($signal)) {
            $motoristaSignals += $signal
        }
    }
    $motoristaCpf = Get-DbValue "select cpf from eventual.motorista where id = $motoristaId;"
    $motoristaRow = Get-ContextSnippet -Text $motoristasListHtml -Needle $motoristaCpf
    $motoristaListStatus = if ($motoristaRow) {
        Get-FirstMatchingStatus -HtmlFragment $motoristaRow -Candidates @("Rejeitado", "Em Análise", "Aguardando Análise", "Incompleto", "Legalizado")
    } else {
        $null
    }

    Write-Host ""
    $checks | Format-Table -AutoSize
    Write-Host ""
    Write-Host "Sinais encontrados na tela do motorista:" -ForegroundColor Cyan
    if ($motoristaSignals.Count -gt 0) {
        $motoristaSignals | ForEach-Object { Write-Host " - $_" }
    } else {
        Write-Host " - nenhum dos sinais esperados foi encontrado"
    }
    Write-Host ""
    Write-Host "Status encontrado na linha do motorista na lista:" -ForegroundColor Cyan
    if ($motoristaListStatus) {
        Write-Host " - $motoristaListStatus"
    } else {
        Write-Host " - status não identificado"
    }
    Write-Host ""
    Write-Host "Smoke live OK" -ForegroundColor Green
}
finally {
    foreach ($cookie in @($empresaCookies, $analistaCookies, $adminCookies)) {
        if ($cookie) {
            Remove-Item -LiteralPath $cookie -ErrorAction SilentlyContinue
        }
    }
}
