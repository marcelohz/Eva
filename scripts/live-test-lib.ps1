Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function U {
    param([int[]]$Codes)
    -join ($Codes | ForEach-Object { [char]$_ })
}

function New-LiveSession {
    param([string]$BaseUrl)

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $handler.AllowAutoRedirect = $false
    if ($BaseUrl -like 'https://*') {
        $handler.ServerCertificateCustomValidationCallback = { $true }
    }

    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = [Uri]$BaseUrl

    return [pscustomobject]@{
        BaseUrl = $BaseUrl.TrimEnd('/')
        Handler = $handler
        Client = $client
    }
}

function Close-LiveSession {
    param($Session)
    if ($null -ne $Session.Client) { $Session.Client.Dispose() }
    if ($null -ne $Session.Handler) { $Session.Handler.Dispose() }
}

function Invoke-LiveGet {
    param(
        $Session,
        [string]$Path
    )

    $response = $Session.Client.GetAsync($Path).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{
        Response = $response
        Body = $body
        StatusCode = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { $null }
    }
}

function Invoke-LivePostForm {
    param(
        $Session,
        [string]$Path,
        [hashtable]$Fields
    )

    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($key in $Fields.Keys) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string, string]]::new($key, [string]$Fields[$key]))
    }

    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $response = $Session.Client.PostAsync($Path, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    return [pscustomobject]@{
        Response = $response
        Body = $body
        StatusCode = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { $null }
    }
}

function Invoke-LivePostMultipart {
    param(
        $Session,
        [string]$Path,
        [hashtable]$Fields,
        [hashtable]$Files
    )

    $content = [System.Net.Http.MultipartFormDataContent]::new()

    foreach ($key in $Fields.Keys) {
        $content.Add([System.Net.Http.StringContent]::new([string]$Fields[$key]), $key)
    }

    foreach ($key in $Files.Keys) {
        $filePath = [string]$Files[$key]
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/pdf')
        $content.Add($fileContent, $key, [System.IO.Path]::GetFileName($filePath))
    }

    $response = $Session.Client.PostAsync($Path, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $content.Dispose()

    return [pscustomobject]@{
        Response = $response
        Body = $body
        StatusCode = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { $null }
    }
}

function Get-HiddenFieldValue {
    param(
        [string]$Html,
        [string]$FieldName
    )

    $pattern = 'name="' + [regex]::Escape($FieldName) + '"[^>]*value="([^"]+)"'
    $match = [regex]::Match($Html, $pattern)
    if (-not $match.Success) {
        throw "Hidden field '$FieldName' not found."
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
        throw "$Label returned $Actual; expected $($Allowed -join ', ')."
    }
}

function Assert-Contains {
    param(
        [string]$Label,
        [string]$Text,
        [string]$Needle
    )

    $decoded = [System.Net.WebUtility]::HtmlDecode($Text)
    if ($decoded -notmatch [regex]::Escape($Needle)) {
        throw "$Label does not contain '$Needle'."
    }
}

function Login-LiveUser {
    param(
        $Session,
        [string]$Email,
        [string]$Password,
        [string]$ExpectedLocation
    )

    $loginPage = Invoke-LiveGet -Session $Session -Path '/Login'
    Assert-Status -Label "GET /Login for $Email" -Actual $loginPage.StatusCode -Allowed @(200)

    $token = Get-HiddenFieldValue -Html $loginPage.Body -FieldName '__RequestVerificationToken'
    $posted = Invoke-LivePostForm -Session $Session -Path '/Login' -Fields @{
        '__RequestVerificationToken' = $token
        'Input.Email' = $Email
        'Input.Senha' = $Password
    }

    Assert-Status -Label "POST /Login for $Email" -Actual $posted.StatusCode -Allowed @(302)
    if ($ExpectedLocation -and ($posted.Location -notlike "*$ExpectedLocation")) {
        throw "Login for $Email redirected to '$($posted.Location)', expected '$ExpectedLocation'."
    }
}

function Get-DbScalar {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword,
        [string]$Sql
    )

    $env:PGPASSWORD = $DbPassword
    $result = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "Database query failed."
    }

    return ($result | Out-String).Trim()
}

function Invoke-DbNonQuery {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword,
        [string]$Sql
    )

    $env:PGPASSWORD = $DbPassword
    $null = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "Database command failed."
    }
}

function Get-PageAndToken {
    param(
        $Session,
        [string]$Path
    )

    $page = Invoke-LiveGet -Session $Session -Path $Path
    Assert-Status -Label "GET $Path" -Actual $page.StatusCode -Allowed @(200)
    return [pscustomobject]@{
        Page = $page
        Token = Get-HiddenFieldValue -Html $page.Body -FieldName '__RequestVerificationToken'
    }
}

function New-PdfFile {
    param([string]$Name)

    $path = Join-Path ([System.IO.Path]::GetTempPath()) ("eva-live-" + [guid]::NewGuid().ToString('N') + "-" + $Name)
    $bytes = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n1 0 obj`n<<>>`nendobj`ntrailer`n<<>>`n%%EOF")
    [System.IO.File]::WriteAllBytes($path, $bytes)
    return $path
}

function Get-LatestSubmissionId {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword,
        [string]$EntityType,
        [string]$EntityId
    )

    Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select id from eventual.submissao where entidade_tipo = '$EntityType' and entidade_id = '$EntityId' order by id desc limit 1;"
}

function Get-LatestSubmissionDocId {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword,
        [int]$SubmissionId,
        [string]$TipoNome
    )

    Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select id from eventual.submissao_documento where submissao_id = $SubmissionId and documento_tipo_nome = '$TipoNome' and ativo_na_submissao = true order by id desc limit 1;"
}

function Get-LatestSubmissionDocumentId {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword,
        [int]$SubmissionId,
        [string]$TipoNome
    )

    Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select documento_id from eventual.submissao_documento where submissao_id = $SubmissionId and documento_tipo_nome = '$TipoNome' and ativo_na_submissao = true order by id desc limit 1;"
}

function Get-ListFragment {
    param(
        [string]$Html,
        [string]$Needle
    )

    $decoded = [System.Net.WebUtility]::HtmlDecode($Html)
    $index = $decoded.IndexOf($Needle)
    if ($index -lt 0) {
        return $null
    }

    $start = [Math]::Max(0, $index - 500)
    $length = [Math]::Min(2000, $decoded.Length - $start)
    return $decoded.Substring($start, $length)
}

function Assert-RowStatuses {
    param(
        [string]$Label,
        [string]$Html,
        [string]$Needle,
        [string]$SituacaoOperacional,
        [string]$UltimaSubmissao
    )

    $fragment = Get-ListFragment -Html $Html -Needle $Needle
    if (-not $fragment) {
        throw "$Label did not find row containing '$Needle'."
    }
    if ($fragment -notmatch [regex]::Escape($SituacaoOperacional)) {
        throw "$Label did not show operational status '$SituacaoOperacional'."
    }
    if ($fragment -notmatch [regex]::Escape($UltimaSubmissao)) {
        throw "$Label did not show latest submission '$UltimaSubmissao'."
    }
}

function New-UniqueCpf {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword
    )

    do {
        $cpf = -join ((1..11) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
        $exists = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select count(*) from eventual.motorista where cpf = '$cpf';"
    } while ($exists -ne '0')

    return $cpf
}

function New-UniquePlate {
    param(
        [string]$DbHost,
        [int]$DbPort,
        [string]$DbName,
        [string]$DbUser,
        [string]$DbPassword
    )

    do {
        $letters = -join ((1..3) | ForEach-Object { [char](Get-Random -Minimum 65 -Maximum 91) })
        $digits = -join ((1..4) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
        $placa = $letters + $digits
        $exists = Get-DbScalar -DbHost $DbHost -DbPort $DbPort -DbName $DbName -DbUser $DbUser -DbPassword $DbPassword -Sql "select count(*) from geral.veiculo where placa = '$placa';"
    } while ($exists -ne '0')

    return $placa
}
