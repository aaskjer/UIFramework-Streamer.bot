# Refreshes the UIFramework host block in a game's action file from stub/HostSchema.cs, keeping what the game put
# into it: its Window declaration and its settings table. The block is found by its markers
# ("// --- UIFramework host block" to "// --- end UIFramework host block ---"), at whatever indentation it sits.
#
#   tools\update-host-block.ps1 -File "C:\...\Blackjack Game\BlackjackGame.cs"
#   tools\update-host-block.ps1 -File ... -Check      # writes nothing; fails when the file's block is behind
#
# A block from before the window declaration gets the template's "Window = null" line; set it after.
param(
    [Parameter(Mandatory = $true)] [string] $File,
    [switch] $Check,
    [string] $Template
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Template) { $Template = Join-Path $root 'stub\HostSchema.cs' }
$utf8 = New-Object Text.UTF8Encoding $false
$nl = "`n"

function Split-Block([string[]] $lines) {
    # The block's head (everything before the window comment), its region (window and table) and its tail.
    $window = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*// ---- The window:' })
    $table = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*// ---- The table:' })
    if ($table -lt 0) { throw 'The block has no "// ---- The table:" comment.' }
    $start = if ($window -ge 0) { $window } else { $table }
    $end = -1
    for ($i = $table; $i -lt $lines.Length; $i++) { if ($lines[$i] -match '^\s*\};\s*$') { $end = $i; break } }
    if ($end -lt 0) { throw 'The table has no closing "};".' }
    return @{ Head = $lines[0..($start - 1)]; Region = $lines[$start..$end]; Tail = $lines[($end + 1)..($lines.Length - 1)]; HasWindow = ($window -ge 0); WindowAt = $window; TableAt = $table }
}

$templateText = [IO.File]::ReadAllText($Template, [Text.Encoding]::UTF8).Replace("`r`n", "`n").TrimEnd("`n")
$templateLines = $templateText.Split("`n")
$fileText = [IO.File]::ReadAllText($File, [Text.Encoding]::UTF8)
$crlf = $fileText.Contains("`r`n")
$fileLines = $fileText.Replace("`r`n", "`n").Split("`n")

$first = [Array]::FindIndex($fileLines, [Predicate[string]] { param($l) $l -match '^\s*// --- UIFramework host block' })
$last = [Array]::FindIndex($fileLines, [Predicate[string]] { param($l) $l -match '^\s*// --- end UIFramework host block ---' })
if ($first -lt 0 -or $last -lt 0 -or $last -le $first) { throw "No UIFramework host block (its two markers) in $File" }
$indent = [regex]::Match($fileLines[$first], '^\s*').Value

$blockLines = $fileLines[$first..$last] | ForEach-Object { if ($_.StartsWith($indent)) { $_.Substring($indent.Length) } else { $_ } }
$have = Split-Block $blockLines
$want = Split-Block $templateLines

# The template's head and tail around the file's own window and table; a file without a window gets the
# template's window lines in front of its table.
$region = @()
if (-not $have.HasWindow) { $region += $want.Region[0..($want.TableAt - $want.WindowAt - 1)] }
$region += $have.Region
$newBlock = @($want.Head) + $region + @($want.Tail)
$newBlockText = (($newBlock | ForEach-Object { if ($_.Length -gt 0) { $indent + $_ } else { $_ } }) -join $nl)
$oldBlockText = ($fileLines[$first..$last] -join $nl)

if ($newBlockText -eq $oldBlockText) {
    Write-Host "  ok   $File carries the current host block"
    exit 0
}
if ($Check) {
    Write-Host "  FAIL $File carries an older host block; run tools\update-host-block.ps1 -File on it" -ForegroundColor Red
    exit 1
}
$before = if ($first -gt 0) { ($fileLines[0..($first - 1)] -join $nl) + $nl } else { '' }
$after = if ($last -lt $fileLines.Length - 1) { $nl + ($fileLines[($last + 1)..($fileLines.Length - 1)] -join $nl) } else { '' }
$result = $before + $newBlockText + $after
if ($crlf) { $result = $result.Replace("`n", "`r`n") }
[IO.File]::WriteAllText($File, $result, $utf8)
Write-Host "  updated the host block in $File"
exit 0
