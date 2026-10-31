# Compiles stub/Stub.cs (with a host's spec embedded) and stub/HostSchema.cs the way Streamer.bot compiles an
# action: CPHInlineBase as the base class, Streamer.bot's injected usings, only the references every action gets.
# Needs a Streamer.bot install for its assemblies and Roslyn, and a RoslynHost front-end, which the game projects
# build with their tools\build-roslyn-host.ps1 (pass the project folder in -RoslynHostTools).
#
#   tools\check-stub.ps1 -HostName blackjack
param(
    [string] $HostName = 'blackjack',
    [string] $StreamerBotPath = 'C:\Users\Administrator\Downloads\Streamer.bot-x64-1.0.7',
    [string] $RoslynHostTools = 'C:\Users\Administrator\Downloads\RhymeTime\tools'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP 'uiframework-stub-check'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$hostExe = @(& (Join-Path $RoslynHostTools 'build-roslyn-host.ps1') -StreamerBotPath $StreamerBotPath)[-1]
if (-not (Test-Path -LiteralPath $hostExe)) { throw "RoslynHost.exe not built: $hostExe" }

& (Join-Path $PSScriptRoot 'embed-spec.ps1') -HostName $HostName -Out (Join-Path $out 'Stub.cs')

function Get-PatchedSource([string] $code) {
    $injected = @(
        'using Streamer.bot.Plugin.Interface;',
        'using Streamer.bot.Plugin.Interface.Enums;',
        'using Streamer.bot.Plugin.Interface.Model;',
        'using Streamer.bot.Common.Events;'
    ) -join [Environment]::NewLine
    $patched = $code -replace '(?m)^(\s*public\s+class\s+CPHInline)\s*$', '$1 : Streamer.bot.Plugin.Interface.CPHInlineBase'
    $class = [regex]::Match($patched, '(?ms)^public\s+class\s+CPHInline\b.*?^\}')
    if ($class.Success) {
        $body = $class.Value
        foreach ($name in 'Execute', 'Init', 'Dispose') {
            $body = ([regex]('(?m)^(    )(?=\S)((?:\w+\s+)*?)(\S+\s+' + $name + '\s*\()')).Replace($body, '$1$2override $3', 1)
        }
        $patched = $patched.Substring(0, $class.Index) + $body + $patched.Substring($class.Index + $class.Length)
    }
    return $injected + [Environment]::NewLine + $patched
}

$refs = @('Streamer.bot.Plugin.Interface.dll', 'Streamer.bot.Common.dll', 'Newtonsoft.Json.dll', 'Twitch.Common.dll') |
    ForEach-Object { '-r:' + (Join-Path $StreamerBotPath $_) }
$refs += '-r:' + [object].Assembly.Location
$refs += '-r:' + [System.Linq.Enumerable].Assembly.Location
$utf8 = New-Object Text.UTF8Encoding $false
$failed = 0

$stubSrc = Join-Path $out 'Stub.patched.cs'
[IO.File]::WriteAllText($stubSrc, (Get-PatchedSource ([IO.File]::ReadAllText((Join-Path $out 'Stub.cs'), [Text.Encoding]::UTF8))), $utf8)
$output = & $hostExe (Join-Path $out 'Stub.dll') '-t:library' @refs $stubSrc
if ($LASTEXITCODE -eq 0) { Write-Host "  ok   stub/Stub.cs compiles as a Streamer.bot action ($HostName spec embedded)" -ForegroundColor Green }
else { Write-Host '  FAIL stub/Stub.cs:' -ForegroundColor Red; $output | Where-Object { $_ -match 'error' } | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }; $failed++ }

# HostSchema.cs sits beside a game's CPHInline; a minimal one stands in for it here.
$schemaSrc = Join-Path $out 'HostSchema.patched.cs'
$hostCode = [IO.File]::ReadAllText((Join-Path $root 'stub\HostSchema.cs'), [Text.Encoding]::UTF8) + @'

public class CPHInline
{
    public bool Execute()
    {
        if (args.ContainsKey("uiframework.describe"))
        {
            CPH.SetGlobalVar("uiframework.schema.demo", UiSchema.ToJson("demo", "v1.0.0", "demo/settings.json"), false);
            return true;
        }
        var s = UiSettings.Load("demo.json");
        return s.Bool("useBot") || s.Int("globalCooldown") >= 0;
    }
}
'@
[IO.File]::WriteAllText($schemaSrc, (Get-PatchedSource $hostCode), $utf8)
$output = & $hostExe (Join-Path $out 'HostSchema.dll') '-t:library' @refs $schemaSrc
if ($LASTEXITCODE -eq 0) { Write-Host '  ok   stub/HostSchema.cs compiles inside a Streamer.bot action' -ForegroundColor Green }
else { Write-Host '  FAIL stub/HostSchema.cs:' -ForegroundColor Red; $output | Where-Object { $_ -match 'error' } | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }; $failed++ }

if ($failed -gt 0) { exit 1 }
exit 0
