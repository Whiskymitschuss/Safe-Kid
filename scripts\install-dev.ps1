[CmdletBinding(SupportsShouldProcess)]param([Parameter(Mandatory)][ValidatePattern('^[^\\@]+$')][string]$ChildUser)
#Requires -RunAsAdministrator
$ErrorActionPreference='Stop';$root=Split-Path -Parent $PSScriptRoot;$target="$env:ProgramFiles\ParentControl Remote";$data="$env:ProgramData\ParentControlRemote"
$account=Get-LocalUser -Name $ChildUser -ErrorAction Stop;if($account.PrincipalSource -ne 'Local'){throw 'Nur lokale Konten werden unterstützt.'};$admins=Get-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-544').Name;if($admins.Name -match "\\$([regex]::Escape($ChildUser))$"){throw 'Das Kinderkonto ist Administrator. Rechte werden nicht automatisch geändert.'}
New-Item $target,$data -ItemType Directory -Force|Out-Null;Copy-Item "$root\artifacts\ParentControl.Service\*" $target -Recurse -Force;Copy-Item "$root\artifacts\ParentControl.Admin\*" $target -Recurse -Force
$config=Get-Content "$target\appsettings.json" -Raw|ConvertFrom-Json;$config.ParentControl.ChildUser=$ChildUser;$config|ConvertTo-Json -Depth 8|Set-Content "$target\appsettings.json" -Encoding utf8
if(Get-Service ParentControlRemote -ErrorAction SilentlyContinue){Stop-Service ParentControlRemote -Force;sc.exe delete ParentControlRemote|Out-Null};sc.exe create ParentControlRemote binPath= "`"$target\ParentControl.Service.exe`"" start= auto DisplayName= "ParentControl Remote"|Out-Null;sc.exe failure ParentControlRemote reset= 86400 actions= restart/5000/restart/15000/restart/60000|Out-Null
New-NetFirewallRule -DisplayName 'ParentControl Remote (Tailscale)' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8765 -RemoteAddress 100.64.0.0/10 -Profile Any -ErrorAction SilentlyContinue|Out-Null
Start-Service ParentControlRemote;Write-Host 'Installiert. Lokal öffnen: http://127.0.0.1:8765'
