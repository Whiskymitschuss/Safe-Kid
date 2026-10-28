# Manueller Testplan auf echtem Windows 11

Diese Prüfungen mit einem entbehrlichen lokalen Testkonto durchführen. Vorher Wiederherstellungspunkt und separates Eltern-Administratorkonto testen.

## Einrichtung und Berechtigungen

- Setup ohne Administratorrechte starten: muss scheitern/Erhöhung verlangen.
- Administratorkonto als Kind wählen: muss abgelehnt werden.
- Microsoft-/Domänenkonto wählen: muss abgelehnt werden.
- Lokales Standardkonto wählen: Installation muss gelingen.
- Vom nicht erlaubten LAN (`192.168.x.x:8765`) zugreifen: HTTP 403 oder Firewall-Block.
- Über Tailscale-HTTPS zugreifen: Anmeldeseite erscheint.

## Authentifizierung

- Passwort unter 12 Zeichen bei Ersteinrichtung: Ablehnung.
- Fünf falsche Anmeldungen von derselben IP: sechster Versuch erhält HTTP 429.
- API ohne Cookie: HTTP 401.
- Änderungsbefehl ohne CSRF-Token: HTTP 400.
- Cookie im Browser prüfen: HttpOnly, SameSite=Strict; unter HTTPS Secure.

## Sperren/Freigeben

- Als Kind anmelden, Datei öffnen, „Sperren“ drücken: Warnung erscheint, Konto wird deaktiviert, Sitzung nach Frist abgemeldet.
- Kinderpasswort am Anmeldebildschirm eingeben: Anmeldung muss wegen deaktiviertem Konto verweigert werden.
- Elternkonto anmelden: muss weiterhin funktionieren.
- „Freigeben“ drücken: Kind kann sich wieder anmelden.
- Während aktiver Sperre neu starten: Kinderkonto bleibt gesperrt; Elternkonto funktioniert.

## Offline und Fehler

- Sperren, dann Router/Tailscale trennen: Sperre bleibt aktiv.
- Telefon offline und Befehl drücken: keine Erfolgsmeldung.
- Verbindung wiederherstellen: Dashboard zeigt tatsächlichen Status.
- Dienst während Sperre beenden und starten: Status wird erneut erzwungen.
- Datenbank testweise schreibgeschützt machen: Befehl darf keinen Erfolg melden.

## Notfall und Deinstallation

- Dienst stoppen und Notfallfreigabe als Standardbenutzer ausführen: muss verweigert werden.
- Als Eltern-Administrator ausführen: Kind wird freigegeben.
- Deinstallation bei aktiver Sperre: Kind wird zuerst freigegeben, Dienst und Firewallregel verschwinden; Elternkonto bleibt unverändert.

Ergebnisse mit Windows-Version, Uhrzeit, Benutzerart und relevanten Ereignisanzeige-Einträgen dokumentieren.
