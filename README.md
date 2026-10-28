# ParentControl Remote

Kostenlose, selbst gehostete Elternsteuerung für einen Windows-11-PC. Der Windows-Dienst stellt ein deutsches Smartphone-Dashboard bereit; der Zugriff erfolgt ausschließlich über das private Tailscale-Netz.

> Sicherheitsgrenze: Die harte Kontosperre unterstützt absichtlich nur **lokale Windows-Standardkonten**. Microsoft-, Entra- und Domänenkonten werden vom Setup abgelehnt, weil deren interaktive Anmeldung nicht zuverlässig mit derselben lokalen Schnittstelle steuerbar ist.

## Schnellstart für Entwickler

Voraussetzungen: Windows 11, .NET 8 SDK, WiX Toolset 4 und Administratorrechte.

```powershell
.\scripts\build.ps1
.\scripts\install-dev.ps1 -ChildUser kind
```

## One-Click-Installer erzeugen

```powershell
.\scripts\build-oneclick.ps1
```

Danach `artifacts\OneClick\ParentControl-Remote-Setup.exe` auf dem Kinder-PC starten. Der Assistent fordert automatisch Administratorrechte an, zeigt nur lokale Standardkonten an, installiert Dienst und Firewallregel und öffnet anschließend die Elternkonto-Einrichtung.

Danach lokal `https://localhost:8765` öffnen. Beim ersten Aufruf wird das Elternkonto eingerichtet. Für den Fernzugriff Tailscale installieren und `scripts/enable-tailscale-https.ps1` als Administrator ausführen.

Die vollständige Installation, Sicherheitsbeschreibung, Tailscale-Konfiguration, Bedienung und manuelle Tests stehen unter [docs](docs/INSTALLATION-DE.md).

## Funktionsweise der Sperre

1. Der Befehl wird authentifiziert, gegen CSRF geschützt und in SQLite dauerhaft gespeichert.
2. Der Sitzungsagent zeigt eine Speicherwarnung an.
3. Der Dienst setzt für das ausgewählte lokale Konto per dokumentierter `NetUserSetInfo`-API das Flag `UF_ACCOUNTDISABLE`.
4. Nach der Warnfrist wird nur die aktive Sitzung dieses Kontos per `WTSLogoffSession` beendet.
5. Der Dienst erzwingt den Zustand nach Neustart und bei Fehlern erneut.
6. Beim Freigeben entfernt der Dienst ausschließlich das von ihm gesetzte Disable-Flag.

Das Eltern-Administratorkonto wird nie verändert. Vor der Einrichtung muss ein separates, getestetes Administratorkonto vorhanden sein.

## Repository

- `src/ParentControl.Service` – Windows-Dienst, ASP.NET Core API, PWA, SQLite
- `src/ParentControl.Agent` – sichtbarer Sitzungsagent
- `src/ParentControl.Admin` – lokale Notfallfreigabe
- `tests/ParentControl.Tests` – automatisierte Kern- und API-Tests
- `installer` – WiX-Installer
- `scripts` – Build, Installation und Tailscale Serve
- `.github/workflows/build.yml` – reproduzierbarer Windows-Build mit Artefakten

## Buildstatus dieser Übergabe

Ein portables .NET SDK wurde zur Prüfung nachinstalliert. Sitzungsagent und Notfalltool wurden erfolgreich kompiliert; der Dienst-Restore scheiterte in der Sandbox am NuGet-TLS-Zugriff. Deshalb wurden keine vollständigen Binärdateien erfunden. GitHub Actions und die lokalen Buildskripte erzeugen ZIP und MSI nach einem vollständigen Build; Details siehe `BUILD-STATUS.md`.
