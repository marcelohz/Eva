param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$DbName = "metroplan",
    [string]$DbUser = "postgres",
    [string]$DbPassword = "master",
    [string]$Email = "codex-smoke@example.com",
    [string]$EmpresaCnpj = "55544433322211",
    [string]$Password = "Codex123!"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function New-WebSession {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $handler.AllowAutoRedirect = $false

    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = [Uri]$BaseUrl

    return @{
        Client = $client
        Handler = $handler
    }
}

function Invoke-Get {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path
    )

    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{
        Response = $response
        Body = $body
    }
}

function Invoke-PostForm {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [hashtable]$Fields
    )

    $pairs = New-Object "System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]"
    foreach ($key in $Fields.Keys) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string, string]]::new($key, [string]$Fields[$key]))
    }

    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $response = $Client.PostAsync($Path, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    return @{
        Response = $response
        Body = $body
    }
}

function Get-HiddenFieldValue {
    param(
        [string]$Html,
        [string]$FieldName
    )

    $pattern = 'name="' + [Regex]::Escape($FieldName) + '"[^>]*value="([^"]+)"'
    $match = [Regex]::Match($Html, $pattern)
    if (-not $match.Success) {
        throw "Campo oculto '$FieldName' nao encontrado."
    }

    return $match.Groups[1].Value
}

function Assert-StatusCode {
    param(
        [string]$Label,
        [System.Net.Http.HttpResponseMessage]$Response,
        [int[]]$Allowed
    )

    $statusCode = [int]$Response.StatusCode
    if ($Allowed -notcontains $statusCode) {
        throw "$Label retornou status inesperado $statusCode."
    }
}

function Seed-SmokeUser {
    $token = ("CODEXSMOKE" + [Guid]::NewGuid().ToString("N")).ToUpperInvariant()
    $env:PGPASSWORD = $DbPassword

    $sql = @"
begin;
delete from web.token_validacao_email where usuario_id in (select id from web.usuario where email = '$Email');
delete from web.usuario where email = '$Email';
delete from geral.empresa where cnpj = '$EmpresaCnpj';
insert into geral.empresa (cnpj, email, nome, nome_fantasia)
values ('$EmpresaCnpj', '$Email', 'Codex Smoke Ltda', 'Codex Smoke');
insert into web.usuario (papel_nome, email, nome, empresa_cnpj, senha, ativo, email_validado, criado_em)
values ('EMPRESA', '$Email', 'Codex Smoke', '$EmpresaCnpj', 'placeholder', true, false, now());
insert into web.token_validacao_email (usuario_id, token, criado_em, expira_em)
select id, '$token', now(), now() + interval '24 hours'
from web.usuario where email = '$Email';
commit;
"@

    & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c $sql | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao semear usuario de smoke test."
    }

    return $token
}

function Confirm-Access {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Token
    )

    $get = Invoke-Get -Client $Client -Path "/ConfirmarAcesso?token=$Token"
    Assert-StatusCode -Label "GET /ConfirmarAcesso" -Response $get.Response -Allowed @(200)

    $verificationToken = Get-HiddenFieldValue -Html $get.Body -FieldName "__RequestVerificationToken"
    $posted = Invoke-PostForm -Client $Client -Path "/ConfirmarAcesso" -Fields @{
        "__RequestVerificationToken" = $verificationToken
        "Token" = $Token
        "Input.Senha" = $Password
        "Input.ConfirmarSenha" = $Password
    }

    Assert-StatusCode -Label "POST /ConfirmarAcesso" -Response $posted.Response -Allowed @(302)
    if ($posted.Response.Headers.Location -eq $null -or $posted.Response.Headers.Location.OriginalString -ne "/Login") {
        throw "POST /ConfirmarAcesso nao redirecionou para /Login."
    }
}

function Invoke-Login {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$CandidatePassword,
        [bool]$ExpectSuccess
    )

    $get = Invoke-Get -Client $Client -Path "/Login"
    Assert-StatusCode -Label "GET /Login" -Response $get.Response -Allowed @(200)

    $verificationToken = Get-HiddenFieldValue -Html $get.Body -FieldName "__RequestVerificationToken"
    $posted = Invoke-PostForm -Client $Client -Path "/Login" -Fields @{
        "__RequestVerificationToken" = $verificationToken
        "Input.Email" = $Email
        "Input.Senha" = $CandidatePassword
    }

    if ($ExpectSuccess) {
        Assert-StatusCode -Label "POST /Login" -Response $posted.Response -Allowed @(302)
        if ($posted.Response.Headers.Location -eq $null -or $posted.Response.Headers.Location.OriginalString -ne "/Empresa/MinhaEmpresa") {
            throw "POST /Login nao redirecionou para /Empresa/MinhaEmpresa."
        }
    }
    else {
        Assert-StatusCode -Label "POST /Login invalido" -Response $posted.Response -Allowed @(200)
        if ($posted.Body -notmatch "E-mail ou senha inv") {
            throw "POST /Login invalido nao exibiu a mensagem esperada."
        }
    }
}

function Test-Route {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [int[]]$AllowedStatuses
    )

    $result = Invoke-Get -Client $Client -Path $Path
    Assert-StatusCode -Label "GET $Path" -Response $result.Response -Allowed $AllowedStatuses
    return $result
}

$session = New-WebSession
$client = $session.Client

try {
    $health = Invoke-Get -Client $client -Path "/Login"
    Assert-StatusCode -Label "App availability" -Response $health.Response -Allowed @(200)

    $token = Seed-SmokeUser
    Confirm-Access -Client $client -Token $token

    Invoke-Login -Client $client -CandidatePassword "SenhaErrada123!" -ExpectSuccess:$false
    Invoke-Login -Client $client -CandidatePassword $Password -ExpectSuccess:$true

    $routeChecks = @(
        @{ Path = "/Empresa/MinhaEmpresa"; Allowed = @(200) },
        @{ Path = "/Empresa/EditarEmpresa"; Allowed = @(200) },
        @{ Path = "/Empresa/MeusVeiculos"; Allowed = @(200) },
        @{ Path = "/Empresa/MeusMotoristas"; Allowed = @(200) },
        @{ Path = "/Empresa/MinhasViagens"; Allowed = @(200) },
        @{ Path = "/Empresa/NovaViagem"; Allowed = @(302) }
    )

    $results = foreach ($check in $routeChecks) {
        $result = Test-Route -Client $client -Path $check.Path -AllowedStatuses $check.Allowed
        $location = $null
        if ($result.Response.Headers.Location -ne $null) {
            $location = $result.Response.Headers.Location.OriginalString
        }

        [PSCustomObject]@{
            Path = $check.Path
            StatusCode = [int]$result.Response.StatusCode
            Location = $location
        }
    }

    $results | Format-Table -AutoSize
    Write-Host ""
    Write-Host "Smoke auth flow OK for $Email" -ForegroundColor Green
}
finally {
    $client.Dispose()
}
