# Copies the UIFramework host block a game's action file carries to where its copies live: another action of the
# game that carries the same block (the stats or scoreboard action), and the framework's own copy,
# hosts\<host>\HostBlock.cs. The block goes over whole, Window and Table included: after a new setting or a refreshed
# block in the game, this is the one step that brings the copies along.
#
#   tools\copy-host-block.ps1 -File "C:\...\Hangman Game\Hangman-Game.cs" -Into "C:\...\Hangman-Scoreboard.cs" -HostName hangman
#   tools\copy-host-block.ps1 -File ... -Into ... -HostName ... -Check     # writes nothing; fails when a copy differs
#
# update-host-block.ps1 takes the same -Into and -HostName and runs this after it.
param(
    [Parameter(Mandatory = $true)] [string] $File,
    [string[]] $Into = @(),
    [string] $HostName,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$open = '// --- UIFramework host block'
$close = '// --- end UIFramework host block ---'

function Read-Text([string] $path) { return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) }
function Write-Text([string] $path, [string] $text) {
    # A file that started with a byte-order mark keeps it.
    $bom = $false
    if (Test-Path -LiteralPath $path) { $b = [IO.File]::ReadAllBytes($path); $bom = $b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF }
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding $bom))
}
# Where the block starts (at the start of its marker's line) and ends (after the end marker), and its indentation.
function Find-Block([string] $text, [string] $path) {
    $s = $text.IndexOf($open); $e = $text.IndexOf($close)
    if ($s -lt 0 -or $e -le $s) { throw "No UIFramework host block (its two markers) in $path" }
    $lineStart = $text.LastIndexOf("`n", $s) + 1
    return @{ Start = $lineStart; End = $e + $close.Length; Indent = $text.Substring($lineStart, $s - $lineStart) }
}

$source = (Read-Text $File).Replace("`r`n", "`n")
$at = Find-Block $source $File
# The block as lines without the indentation it has in the game.
$lines = $source.Substring($at.Start, $at.End - $at.Start).Split("`n") | ForEach-Object { if ($_.StartsWith($at.Indent)) { $_.Substring($at.Indent.Length) } else { $_ } }

$failed = 0
function Put([string] $path, [string] $want, [string] $what) {
    $have = if (Test-Path -LiteralPath $path) { Read-Text $path } else { $null }
    if ($have -ceq $want) { Write-Host "  ok   $what is the block $([IO.Path]::GetFileName($File)) carries"; return }
    if ($Check) { Write-Host "  FAIL $what is not the block $([IO.Path]::GetFileName($File)) carries; run tools\copy-host-block.ps1" -ForegroundColor Red; $script:failed++; return }
    Write-Text $path $want
    Write-Host "  new  $what written from $([IO.Path]::GetFileName($File))"
}

foreach ($target in $Into) {
    $text = Read-Text $target
    $crlf = $text.Contains("`r`n")
    $unix = $text.Replace("`r`n", "`n")
    $t = Find-Block $unix $target
    $block = ($lines | ForEach-Object { if ($_.Length -gt 0) { $t.Indent + $_ } else { $_ } }) -join "`n"
    $result = $unix.Substring(0, $t.Start) + $block + $unix.Substring($t.End)
    if ($crlf) { $result = $result.Replace("`n", "`r`n") }
    Put $target $result $target
}

if ($HostName) {
    $copy = Join-Path $root "hosts\$HostName\HostBlock.cs"
    # The copy keeps the indentation the block has in the game, its line ends and whether it ends on one.
    $block = ($lines | ForEach-Object { if ($_.Length -gt 0) { $at.Indent + $_ } else { $_ } }) -join "`n"
    $old = if (Test-Path -LiteralPath $copy) { Read-Text $copy } else { '' }
    $tail = if ($old.EndsWith("`n")) { "`n" } else { '' }
    $want = $block.TrimEnd() + $tail
    if ($old.Contains("`r`n")) { $want = $want.Replace("`n", "`r`n") }
    Put $copy $want "hosts\$HostName\HostBlock.cs"
}

if ($failed -gt 0) { exit 1 }
exit 0
