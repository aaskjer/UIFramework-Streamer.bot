# Writes a host's settings action from the template: stub/Stub.cs with the host's section (hosts/<host>/host-section.cs,
# between the HOST markers) and its spec (hosts/<host>/spec.json, as the Spec constant) spliced in.
#
#   tools\embed-spec.ps1 -HostName blackjack -Out "C:\...\Blackjack Game\BlackjackGame-GUI.cs"
#
# Run again after every change to spec.json or host-section.cs. Without -Out the result lands in hosts/<host>/Stub.cs.
param(
    [Parameter(Mandatory = $true)] [string] $HostName,
    [string] $Out
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object Text.UTF8Encoding $false
$template = [IO.File]::ReadAllText((Join-Path $root 'stub\Stub.cs'), [Text.Encoding]::UTF8)
$specPath = Join-Path $root "hosts\$HostName\spec.json"
$sectionPath = Join-Path $root "hosts\$HostName\host-section.cs"
if (-not (Test-Path -LiteralPath $specPath)) { throw "Not found: $specPath" }

# Minified, so the constant is one line; verbatim string: a quote becomes two quotes.
Add-Type -AssemblyName System.Web.Extensions
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue
$json = $serializer.Serialize($serializer.DeserializeObject([IO.File]::ReadAllText($specPath, [Text.Encoding]::UTF8)))
$embedded = $json.Replace('"', '""')

if (-not $template.Contains('@"__SPEC_JSON__"')) { throw 'The template has no __SPEC_JSON__ marker.' }
$result = $template.Replace('__SPEC_JSON__', $embedded)

if (Test-Path -LiteralPath $sectionPath) {
    $start = $result.IndexOf('    // ==== HOST ====')
    $end = $result.IndexOf('    // ==== END HOST ====')
    if ($start -lt 0 -or $end -lt 0) { throw 'The template has no HOST markers.' }
    $section = [IO.File]::ReadAllText($sectionPath, [Text.Encoding]::UTF8).TrimEnd("`r", "`n")
    $result = $result.Substring(0, $start) + "    // ==== HOST ====`n" + $section + "`n" + $result.Substring($end)
}

if (-not $Out) { $Out = Join-Path $root "hosts\$HostName\Stub.cs" }
[IO.File]::WriteAllText($Out, $result.Replace("`r`n", "`n"), $utf8)
Write-Host "wrote $Out ($($json.Length) chars of spec$(if (Test-Path -LiteralPath $sectionPath) { ', host section spliced' }))"
