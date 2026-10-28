# Installation unter Windows 11

## Vorbedingungen

1. Der One-Click-Assistent legt zuerst ein neues lokales Elternkonto an, macht es zum Administrator und verifiziert dessen Passwort.
2. Erst nach dieser Prüfung wird das ausgewählte lokale Kinderkonto aus der Administratorgruppe entfernt. Microsoft-, Entra- und Domänenkonten werden nicht unterstützt.
3. Elterntelefon und Kinder-PC werden im selben privaten Tailscale-Netz angemeldet. Aktivieren Sie in der Tailscale-Administration MFA für Ihr Konto und entfernen Sie unbekannte Geräte.

## Build und Installation

Für den vollständigen Assistenten:

```powershell
.\scripts\build-oneclick.ps1
.\artifacts\OneClick\ParentControl-Remote-Setup.exe
```

Im Assistenten Kinderkonto, Namen und Passwort des neuen Eltern-Administratorkontos auswählen. Die EXE fordert die benötigten Administratorrechte automatisch an.

Kontoreihenfolge des Assistenten:

1. Neues lokales Elternkonto anlegen.
2. Elternkonto zur integrierten Administratorgruppe hinzufügen.
3. Eine echte Windows-Anmeldung mit dem angegebenen Passwort verifizieren.
4. Erst dann das Kinderkonto aus der Administratorgruppe entfernen.
5. Dienst, Firewallregel und optionale Tailscale-HTTPS-Weiterleitung einrichten.

Schlägt Schritt 2 oder 3 fehl, wird das neu angelegte Konto zurückgerollt und das Kinderkonto nicht verändert. Das Elternpasswort wird weder gespeichert noch an einen Unterprozess übergeben.

Alternativ für Entwickler:

Als Administrator in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build.ps1
.\scripts\install-dev.ps1 -ChildUser "kind"
```

Das Skript verweigert Administratorkonten und verändert keine Kontorechte. Anschließend auf dem PC `http://127.0.0.1:8765` öffnen und das Elternkonto mit einem einzigartigen Passwort von mindestens 12 Zeichen anlegen.

## Tailscale und HTTPS für iPhone/PWA

1. [Tailscale](https://tailscale.com/download/windows) auf PC und Smartphone installieren und mit demselben privaten Tailnet verbinden.
2. MagicDNS und HTTPS im Tailscale-Adminbereich aktivieren.
3. Als Administrator `.\scripts\enable-tailscale-https.ps1` starten.
4. Die ausgegebene `https://…ts.net`-Adresse auf dem Telefon öffnen.
5. iPhone: Teilen → „Zum Home-Bildschirm“. Android/Chrome: Menü → „App installieren“.

`tailscale serve` terminiert TLS und leitet nur innerhalb des Tailnets an `127.0.0.1:8765` weiter. Die Firewall-Regel erlaubt Port 8765 ausschließlich aus `100.64.0.0/10`; der Anwendungsfilter akzeptiert zusätzlich nur Tailscale und Loopback. Wenn das Tailnet eigene IPv6-Adressen verwendet, sollte bevorzugt ausschließlich Tailscale Serve/443 benutzt und die direkte Port-8765-Regel entfernt werden.

## Dienstbetrieb

Der Dienst `ParentControlRemote` startet automatisch, auch ohne Benutzeranmeldung. Windows startet ihn bei Fehlern dreimal neu. SQLite liegt unter `%ProgramData%\ParentControlRemote\parentcontrol.db`; der Sperrzustand wird vor Ausführung eines Befehls dauerhaft geschrieben und nach einem Neustart erneut erzwungen.

## Lokale Notfallfreigabe

Als Eltern-Administrator:

```powershell
Stop-Service ParentControlRemote
& "$env:ProgramFiles\ParentControl Remote\ParentControl.Admin.exe" emergency-unlock kind
```

Danach Ursache prüfen und erst dann den Dienst wieder starten. Das Tool kann nur erhöht ausgeführt werden.

## Deinstallation

```powershell
.\scripts\uninstall.ps1 -ChildUser kind
```

Mit `-RemoveData` wird zusätzlich die lokale Datenbank entfernt. Vor Entfernung wird das Kinderkonto freigegeben. Der Elternzugang und bestehende Windows-Konten werden nicht gelöscht.

## Bekannte Grenzen

- Windows Home/Pro bietet keine universelle unterstützte API, die beliebige Cloud-/Domänenidentitäten selektiv und revisionssicher von interaktiver Anmeldung ausschließt. Deshalb ist die harte Sperre auf lokale Konten begrenzt.
- Ein Benutzer mit Administratorrechten kann Dienste und Kontostatus verändern. Das Kinderkonto darf daher kein Administrator sein.
- „Bildschirmzeit“ wird derzeit nicht angezeigt, weil eine verlässliche, datenschutzfreundliche Messung über Sitzungswechsel hinweg noch nicht implementiert ist. Die API liefert hierfür `0`, die UI behauptet keinen Wert.
- Optionales Passkey/TOTP ist in dieser Version nicht implementiert; starkes Elternpasswort plus Tailscale-Geräteauthentifizierung sind Pflicht.
- Der Sitzungsagent ist als Komponente enthalten; die zuverlässige Systemwarnung erfolgt derzeit direkt über die dokumentierte WTS-Nachrichtenfunktion des Dienstes.
