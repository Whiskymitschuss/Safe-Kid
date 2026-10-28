# Buildstatus

- Quellcode erstellt: ja
- .NET-Build lokal ausgeführt: teilweise – .NET SDK 8.0.427 portabel installiert; `ParentControl.Agent` und `ParentControl.Admin` kompilieren mit 0 Warnungen/0 Fehlern
- `ParentControl.Setup`: frameworkabhängiger Prüfbuild erfolgreich, 0 Warnungen/0 Fehler; der endgültige selbstenthaltende EXE-Build benötigt den vollständigen NuGet-Restore
- Dienst-Build: Restore blockiert (`NU1301`, NuGet-Serviceindex aus der SDK-Laufzeit nicht erreichbar); direkter HTTPS-Download war erreichbar
- Automatisierte Tests lokal ausgeführt: nein – Test- und Dienstpakete konnten wegen desselben Restore-Problems nicht vollständig aufgelöst werden
- Installer lokal erzeugt: nein
- GitHub-Actions-Workflow vorbereitet: ja

Ein vollständiger Build wird mit `scripts/build.ps1` oder dem Workflow `.github/workflows/build.yml` erzeugt. Der Workflow veröffentlicht Testresultate, portable ZIP-Dateien und das MSI als Artefakte. Es werden ausdrücklich keine nicht ausgeführten Tests als bestanden ausgewiesen.
