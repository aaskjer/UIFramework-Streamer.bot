# Compiles the pieces a project pastes into Streamer.bot the way Streamer.bot compiles an action: CPHInlineBase as the
# base class, Streamer.bot's injected usings, only the references every action gets.
#   - stub/Stub.cs, the generic settings action, as it is;
#   - examples/DemoGame/DemoGame.cs, the example game with the host block (stub/HostSchema.cs) inside; its block
#     must be the template's (tools/update-host-block.ps1 -Check), and its schema is written to
#     examples/DemoGame/schema.json for the preview and the docs.
# Needs a Streamer.bot install for its assemblies and Roslyn, and a RoslynHost front-end, which the game projects
# build with their tools\build-roslyn-host.ps1 (pass the project folder in -RoslynHostTools).
#
#   tools\check-stub.ps1
param(
    [string] $StreamerBotPath = 'C:\Users\Administrator\Downloads\Streamer.bot-x64-1.0.7',
    [string] $RoslynHostTools = 'C:\Users\Administrator\Downloads\RhymeTime\tools'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP 'uiframework-stub-check'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$hostExe = @(& (Join-Path $RoslynHostTools 'build-roslyn-host.ps1') -StreamerBotPath $StreamerBotPath)[-1]
if (-not (Test-Path -LiteralPath $hostExe)) { throw "RoslynHost.exe not built: $hostExe" }

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
# The stub compiles with the references every Streamer.bot action has; its dialogs are Win32 message boxes.
$stubRefs = @($refs)
$utf8 = New-Object Text.UTF8Encoding $false
$failed = 0

function Compile([string] $label, [string] $source, [string] $dll, [string[]] $references) {
    $patchedPath = Join-Path $out ([IO.Path]::GetFileNameWithoutExtension($dll) + '.patched.cs')
    [IO.File]::WriteAllText($patchedPath, (Get-PatchedSource ([IO.File]::ReadAllText($source, [Text.Encoding]::UTF8))), $utf8)
    $output = & $hostExe (Join-Path $out $dll) '-t:library' @references $patchedPath
    if ($LASTEXITCODE -eq 0) { Write-Host "  ok   $label" -ForegroundColor Green; return $true }
    Write-Host "  FAIL $label" -ForegroundColor Red
    $output | Where-Object { $_ -match 'error' } | Select-Object -First 10 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
    return $false
}

# 1. The generic stub, the same file for every project.
if (-not (Compile 'stub/Stub.cs compiles as a Streamer.bot action (generic, nothing embedded)' (Join-Path $root 'stub\Stub.cs') 'Stub.dll' $stubRefs)) { $failed++ }

# 2. The example game: the host block inside it must be the template's, and it must compile as an action.
$demo = Join-Path $root 'examples\DemoGame\DemoGame.cs'
& (Join-Path $PSScriptRoot 'update-host-block.ps1') -File $demo -Check
if ($LASTEXITCODE -ne 0) { $failed++ }
if (Compile 'examples/DemoGame/DemoGame.cs compiles as a Streamer.bot action (host block inside)' $demo 'Demo.dll' $refs) {
    # Its schema, as the game would answer describe with, for the preview (-schema) and the docs' screenshots.
    try {
        $null = [Reflection.Assembly]::LoadFrom((Join-Path $StreamerBotPath 'Newtonsoft.Json.dll'))
        $null = [Reflection.Assembly]::LoadFrom((Join-Path $StreamerBotPath 'Streamer.bot.Plugin.Interface.dll'))
        $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $out 'Demo.dll'))
        $json = $assembly.GetType('UiSchema').GetMethod('ToJson', [type[]]@([string], [string], [string])).Invoke($null, @('demo', 'v1.0.0', 'Demo Game/DemoGame-Settings.json'))
        $pretty = [Newtonsoft.Json.Linq.JObject]::Parse($json).ToString()
        $schemaPath = Join-Path $root 'examples\DemoGame\schema.json'
        $before = if (Test-Path -LiteralPath $schemaPath) { [IO.File]::ReadAllText($schemaPath, [Text.Encoding]::UTF8) } else { '' }
        if ($before.Replace("`r`n", "`n") -ne ($pretty.Replace("`r`n", "`n") + "`n")) {
            [IO.File]::WriteAllText($schemaPath, $pretty.Replace("`r`n", "`n") + "`n", $utf8)
            Write-Host '  ok   examples/DemoGame/schema.json written from the game' -ForegroundColor Green
        } else { Write-Host '  ok   examples/DemoGame/schema.json is current' -ForegroundColor Green }
    }
    catch { Write-Host "  FAIL the demo game's schema could not be read: $($_.Exception.Message)" -ForegroundColor Red; $failed++ }
} else { $failed++ }

# The demo's export must carry the scripts as they are.
if (Test-Path -LiteralPath (Join-Path $root 'examples\DemoGame\DemoGame.sb')) {
    & (Join-Path $PSScriptRoot 'build-import.ps1') -Check
    if ($LASTEXITCODE -ne 0) { $failed++ }
}

if ($failed -gt 0) { exit 1 }
exit 0
