# Keeps a Streamer.bot export (.sb) on the current scripts: every Execute Code sub-action of a named action gets
# the file named for it (UTF-8 without BOM, CRLF, the way an export holds a script), and the export's meta version
# follows CurrentVersion in the game's file. Everything else stays as Streamer.bot wrote it: actions, triggers,
# commands, ids, references. A new action, trigger or command is made in Streamer.bot and exported again; that
# export becomes the new template.
#
# Without parameters it keeps examples\DemoGame\DemoGame.sb on DemoGame.cs and stub\Stub.cs; a project of your own
# names its export and its map:
#
#   tools\build-import.ps1                                   # the demo's export, written when it is behind
#   tools\build-import.ps1 -Check                            # fail when it is behind (check-stub runs this)
#   tools\build-import.ps1 -Path C:\...\MyGame.sb -Map @{ 'My Game' = 'C:\...\MyGame.cs'; 'My Game Settings' = 'C:\...\Stub.cs' } -VersionFrom C:\...\MyGame.cs
param(
    [string] $Path,
    [hashtable] $Map,
    [string] $VersionFrom,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding $false

if (-not $Path) { $Path = Join-Path $root 'examples\DemoGame\DemoGame.sb' }
if (-not $Map) { $Map = @{ 'Demo Game' = (Join-Path $root 'examples\DemoGame\DemoGame.cs'); '.Demo GUI' = (Join-Path $root 'stub\Stub.cs') } }
if (-not $VersionFrom -and $Map.ContainsKey('Demo Game')) { $VersionFrom = $Map['Demo Game'] }
$name = Split-Path -Leaf $Path

# An export is Base64 of "SBAE" followed by the gzipped JSON.
function ConvertFrom-Export([string] $text) {
    try { $bytes = [Convert]::FromBase64String($text.Trim()) } catch { throw "$name is not a Streamer.bot export: it is not Base64." }
    if ($bytes.Length -lt 5 -or [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'SBAE') { throw "$name is not a Streamer.bot export: it does not start with SBAE." }
    $packed = [System.IO.MemoryStream]::new($bytes, 4, $bytes.Length - 4)
    $gzip = [System.IO.Compression.GZipStream]::new($packed, [System.IO.Compression.CompressionMode]::Decompress)
    $plain = [System.IO.MemoryStream]::new()
    try { $gzip.CopyTo($plain) } catch { throw "$name is not a Streamer.bot export: its content is not gzip." } finally { $gzip.Dispose() }
    $json = $plain.ToArray()
    $bom = $json.Length -ge 3 -and $json[0] -eq 0xEF -and $json[1] -eq 0xBB -and $json[2] -eq 0xBF
    $start = 0
    if ($bom) { $start = 3 }
    return @{ Json = $utf8.GetString($json, $start, $json.Length - $start); Bom = $bom }
}

function ConvertTo-Export([string] $json, [bool] $bom) {
    $body = $utf8.GetBytes($json)
    $packed = [System.IO.MemoryStream]::new()
    $packed.Write([byte[]](0x53, 0x42, 0x41, 0x45), 0, 4)
    $gzip = [System.IO.Compression.GZipStream]::new($packed, [System.IO.Compression.CompressionLevel]::Optimal, $true)
    if ($bom) { $gzip.Write([byte[]](0xEF, 0xBB, 0xBF), 0, 3) }
    $gzip.Write($body, 0, $body.Length)
    $gzip.Dispose()
    return [Convert]::ToBase64String($packed.ToArray())
}

# The code sub-actions of the export, each with the name of the action it sits in.
function Get-Code([string] $json) {
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = [int]::MaxValue
    $serializer.RecursionLimit = 4096
    $doc = $serializer.DeserializeObject($json)
    $code = New-Object 'System.Collections.Generic.List[object]'
    foreach ($action in @($doc['data']['actions'])) {
        foreach ($sub in @($action['subActions'])) {
            if ($sub['byteCode'] -is [string] -and $sub['byteCode'].Length -gt 0) {
                $code.Add([pscustomobject]@{ Action = [string]$action['name']; ByteCode = $sub['byteCode'] })
            }
        }
    }
    return @{ Doc = $doc; Code = $code }
}

function Get-ByteCode([string] $file) {
    if (-not (Test-Path -LiteralPath $file)) { throw "$file is missing." }
    $text = [System.IO.File]::ReadAllText($file, $utf8).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`n", "`r`n")
    return [Convert]::ToBase64String($utf8.GetBytes($text))
}

function Measure-Text([string] $text, [string] $part) {
    $count = 0
    $at = $text.IndexOf($part, [StringComparison]::Ordinal)
    while ($at -ge 0) { $count++; $at = $text.IndexOf($part, $at + $part.Length, [StringComparison]::Ordinal) }
    return $count
}

$raw = [System.IO.File]::ReadAllText($Path)
$export = ConvertFrom-Export $raw
$parts = Get-Code $export.Json
$json = $export.Json
$changes = @()
$failed = 0

# 1. The scripts.
$replace = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($action in $Map.Keys) {
    $subs = @($parts.Code | Where-Object { $_.Action -ceq $action })
    if ($subs.Count -eq 0) { Write-Host "  FAIL no action '$action' with code in $name; make it in Streamer.bot and export again" -ForegroundColor Red; $failed++; continue }
    $want = Get-ByteCode $Map[$action]
    foreach ($sub in $subs) {
        if ($sub.ByteCode -ceq $want) { continue }
        if ((Measure-Text $json ('"' + $sub.ByteCode + '"')) -ne 1) { throw "nothing written: the code of '$action' stands more than once in $name." }
        $replace[$sub.ByteCode] = $want
        $changes += "  new  $action <- $(Split-Path -Leaf $Map[$action])"
    }
}
foreach ($sub in $parts.Code) { if (-not $Map.ContainsKey($sub.Action)) { Write-Host "  note '$($sub.Action)' carries code no file is named for; left as it is" -ForegroundColor DarkYellow } }
foreach ($old in $replace.Keys) { $json = $json.Replace('"' + $old + '"', '"' + $replace[$old] + '"') }

# 2. The version, from CurrentVersion in the game's file (its v dropped), into the export's meta.
if ($VersionFrom) {
    $m = [regex]::Match([System.IO.File]::ReadAllText($VersionFrom, $utf8), 'private const string CurrentVersion = "v?([^"]+)";')
    if ($m.Success) {
        $version = $m.Groups[1].Value
        $meta = [regex]::Match($json, '("meta"\s*:\s*\{[^{}]*?"version"\s*:\s*)("(?:[^"\\]|\\.)*")')
        if (-not $meta.Success) { throw 'nothing written: the export has no meta version.' }
        $wantMeta = '"' + $version.Replace('\', '\\').Replace('"', '\"') + '"'
        if ($meta.Groups[2].Value -cne $wantMeta) {
            $json = $json.Substring(0, $meta.Groups[2].Index) + $wantMeta + $json.Substring($meta.Groups[2].Index + $meta.Groups[2].Length)
            $changes += "  ver  meta $($meta.Groups[2].Value) -> $wantMeta"
        }
    } else { Write-Host "  note no CurrentVersion in $(Split-Path -Leaf $VersionFrom); the export's version stays" -ForegroundColor DarkYellow }
}

if ($failed -gt 0) { throw "$name not written." }
if ($changes.Count -eq 0) { Write-Host "  ok   $name carries the current scripts" -ForegroundColor Green; exit 0 }
if ($Check) {
    foreach ($line in $changes) { Write-Host $line }
    Write-Host "  FAIL $name is behind; run tools\build-import.ps1" -ForegroundColor Red
    exit 1
}

# Read back before writing: the planned code and nothing else moved.
$after = Get-Code $json
if ($after.Code.Count -ne $parts.Code.Count) { throw 'nothing written: the rewrite changed the number of code sub-actions.' }
for ($i = 0; $i -lt $parts.Code.Count; $i++) {
    $expected = $parts.Code[$i].ByteCode
    if ($replace.ContainsKey($expected)) { $expected = $replace[$expected] }
    if ($after.Code[$i].ByteCode -cne $expected) { throw "nothing written: $($parts.Code[$i].Action) reads back with other code than planned." }
}
$encoded = ConvertTo-Export $json $export.Bom
$back = ConvertFrom-Export $encoded
if ($back.Json -cne $json) { throw 'nothing written: the packed export does not unpack to the rewritten JSON.' }
$tail = ''
if ($raw -match '(\r?\n)+$') { $tail = $Matches[0] }
[System.IO.File]::WriteAllText($Path, $encoded + $tail, [System.Text.Encoding]::ASCII)
foreach ($line in $changes) { Write-Host $line }
Write-Host "  ok   $name written" -ForegroundColor Green
exit 0
