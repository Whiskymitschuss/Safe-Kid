#Requires -RunAsAdministrator
$ErrorActionPreference='Stop';if(-not(Get-Command tailscale -ErrorAction SilentlyContinue)){throw 'Tailscale ist nicht installiert oder nicht im PATH.'};tailscale status;tailscale serve --bg --https=443 http://127.0.0.1:8765;Write-Host 'HTTPS aktiv. Die von tailscale serve ausgegebene MagicDNS-Adresse auf dem Smartphone öffnen.'
