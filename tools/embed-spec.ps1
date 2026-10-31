# Writes a host's stub: stub/Stub.cs with hosts/<host>/spec.json embedded as the Spec constant.
#
#   tools\embed-spec.ps1 -HostName blackjack -Out "C:\...\Blackjack Game\BlackjackGame-GUI.cs"
#
# The HOST section of the template still has to fit the host (action names, paths, cooldown ids); this only
# puts the JSON in. Run again after every change to spec.json.
param(
    [Parameter(Mandatory = $true)] [string] $HostName,
    [string] $Out
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$template = [IO.File]::ReadAllText((Join-Path $root 'stub\Stub.cs'), [Text.Encoding]::UTF8)
$specPath = Join-Path $root "hosts\$HostName\spec.json"
if (-not (Test-Path -LiteralPath $specPath)) { throw "Not found: $specPath" }

# Minified, so the constant is one line; verbatim string: a quote becomes two quotes.
Add-Type -AssemblyName System.Web.Extensions
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue
$json = $serializer.Serialize($serializer.DeserializeObject([IO.File]::ReadAllText($specPath, [Text.Encoding]::UTF8)))
$embedded = $json.Replace('"', '""')

if (-not $template.Contains('@"__SPEC_JSON__"')) { throw 'The template has no __SPEC_JSON__ marker.' }
$result = $template.Replace('__SPEC_JSON__', $embedded)
if (-not $Out) { $Out = Join-Path $root "hosts\$HostName\Stub.cs" }
[IO.File]::WriteAllText($Out, $result, (New-Object Text.UTF8Encoding $false))
Write-Host "wrote $Out ($($json.Length) chars of spec)"
