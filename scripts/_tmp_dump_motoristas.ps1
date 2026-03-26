param([string]$BaseUrl='"'"'https://localhost:7143'"'"')
Set-StrictMode -Version Latest
$ErrorActionPreference='"'"'Stop'"'"'
function New-Temp([string]$s){ Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('"'"'N'"'"') + $s) }
function GetHidden([string]$Html,[string]$Name){ $m=[regex]::Match($Html,'"'"'name="'"'"'+[regex]::Escape($Name)+'"'"'"[^>]*value="'"'"([^"]+)"'"'"'); if(-not $m.Success){throw "missing $Name"}; $m.Groups[1].Value }
$cookie=New-Temp '"'"'.cookies'"'"'; Set-Content $cookie ''
$loginHtml=New-Temp '"'"'.html'"'"'; $loginHeaders=New-Temp '"'"'.h'"'"'
curl.exe -k -sS -D $loginHeaders -o $loginHtml -c $cookie -b $cookie "$BaseUrl/Login" | Out-Null
$html = Get-Content -Raw $loginHtml
$token=GetHidden $html '"'"'__RequestVerificationToken'"'"'
$postHeaders=New-Temp '"'"'.h'"'"'; $postBody=New-Temp '"'"'.b'"'"'
curl.exe -k -sS -D $postHeaders -o $postBody -c $cookie -b $cookie -X POST --data-urlencode "__RequestVerificationToken=$token" --data-urlencode "Input.Email=net@net" --data-urlencode "Input.Senha=master" "$BaseUrl/Login" | Out-Null
$pageHeaders=New-Temp '"'"'.h'"'"'; $pageBody=New-Temp '"'"'.html'"'"'
curl.exe -k -sS -D $pageHeaders -o $pageBody -c $cookie -b $cookie "$BaseUrl/Empresa/MeusMotoristas" | Out-Null
Get-Content -Raw $pageBody
Remove-Item $cookie,$loginHtml,$loginHeaders,$postHeaders,$postBody,$pageHeaders,$pageBody -ErrorAction SilentlyContinue
