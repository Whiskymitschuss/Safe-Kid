[CmdletBinding()]param([string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$artifacts=Join-Path $root 'artifacts'
$payload=Join-Path $root 'src\ParentControl.Setup\Payload'
$temporary=Join-Path $root 'artifacts\payload-temp'
$dotnetCommand=Get-Command dotnet -ErrorAction SilentlyContinue
if($dotnetCommand){
  $dotnet=$dotnetCommand.Source
}else{
  $localSdk=Join-Path $root '.tools\dotnet'
  $dotnet=Join-Path $localSdk 'dotnet.exe'
  if(-not(Test-Path $dotnet)){
    Write-Host 'Das .NET 8 SDK wird einmalig lokal heruntergeladen...'
    $installer=Join-Path $env:TEMP 'parentcontrol-dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel 8.0 -InstallDir $localSdk -NoPath
    if($LASTEXITCODE -ne 0 -or -not(Test-Path $dotnet)){throw 'Das .NET 8 SDK konnte nicht installiert werden. Internetverbindung und TLS-Einstellungen prüfen.'}
  }
}
$env:DOTNET_CLI_HOME=Join-Path $root '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Write-Host "Verwendetes .NET SDK: $dotnet"
& $dotnet restore (Join-Path $root 'ParentControl.Remote.sln')
if($LASTEXITCODE -ne 0){throw 'NuGet-Wiederherstellung fehlgeschlagen.'}
& $dotnet test (Join-Path $root 'ParentControl.Remote.sln') -c $Configuration --no-restore --logger "trx;LogFileName=tests.trx"
if($LASTEXITCODE -ne 0){throw 'Tests fehlgeschlagen; es wird kein Installer erzeugt.'}
if(Test-Path $payload){Remove-Item -LiteralPath $payload -Recurse -Force}
if(Test-Path $temporary){Remove-Item -LiteralPath $temporary -Recurse -Force}
New-Item $payload,$temporary -ItemType Directory -Force|Out-Null
foreach($project in 'ParentControl.Service','ParentControl.Admin'){
  $destination=Join-Path $temporary $project
  & $dotnet publish (Join-Path $root "src\$project\$project.csproj") -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $destination
  if($LASTEXITCODE -ne 0){throw "Veröffentlichung von $project fehlgeschlagen."}
  Copy-Item (Join-Path $destination '*') $payload -Recurse -Force
}
$setupOut=Join-Path $artifacts 'OneClick'
& $dotnet publish (Join-Path $root 'src\ParentControl.Setup\ParentControl.Setup.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $setupOut
if($LASTEXITCODE -ne 0){throw 'Der One-Click-Installer konnte nicht erzeugt werden.'}
Write-Host "One-Click-Installer: $setupOut\ParentControl-Remote-Setup.exe"
