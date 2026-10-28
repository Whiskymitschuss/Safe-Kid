[CmdletBinding()]param([string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$out=Join-Path $root 'artifacts'
dotnet restore (Join-Path $root 'ParentControl.Remote.sln') --locked-mode:$false
dotnet test (Join-Path $root 'ParentControl.Remote.sln') -c $Configuration --no-restore --logger "trx;LogFileName=tests.trx"
foreach($project in 'ParentControl.Service','ParentControl.Agent','ParentControl.Admin'){
  dotnet publish (Join-Path $root "src\$project\$project.csproj") -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $out $project)
}
Compress-Archive -Path (Join-Path $out '*') -DestinationPath (Join-Path $out 'ParentControl-Remote-portable.zip') -Force
if(Get-Command wix -ErrorAction SilentlyContinue){wix build (Join-Path $root 'installer\Package.wxs') -arch x64 -d SourceDir=$out -o (Join-Path $out 'ParentControl-Remote.msi')}
