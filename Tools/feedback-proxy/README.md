# Feedback-Proxy

Das Feedback-Fenster im Spiel schickt nicht mehr direkt an Discord, sondern an
diesen Cloudflare Worker. Der Discord-Webhook liegt nur noch beim Worker und
steckt damit nicht mehr im Build.

## Einrichten (einmalig, kostenloser Cloudflare-Account reicht)

1. **Discord-Webhook prüfen.** Ist schon ein Build mit der Discord-URL in
   `webhook.txt` an Tester rausgegangen, gilt der Webhook als verbrannt: in
   Discord unter *Kanal-Einstellungen → Integrationen → Webhooks* den alten
   **löschen** und einen neuen erstellen. Am besten in einem Kanal, den nur
   du siehst.
2. Im Ordner `Tools/feedback-proxy`:
   ```
   npx wrangler login
   npx wrangler secret put DISCORD_WEBHOOK     (neue Webhook-URL einfügen)
   npx wrangler deploy
   ```
   `deploy` gibt die Adresse aus, z. B.
   `https://biscuit-feedback.<dein-name>.workers.dev`
3. Diese Adresse (nicht die Discord-URL!) als einzige Zeile in
   `Assets/Resources/Feedback/webhook.txt` schreiben.
4. Im Spiel eine Testnachricht schicken.

Steht in `webhook.txt` noch eine Discord-URL, funktioniert das Senden nur im
Editor. Ein Build bricht mit einer Fehlermeldung ab (`SteamBuildCheck`).

## Was der Worker absichert

- Drosselung pro IP (2/min) und insgesamt (20/min), einstellbar in `wrangler.toml`
- Die Discord-Nachricht wird neu gebaut: fester Name, keine Pings, nur die
  bekannten Felder, alles gekürzt
- Webhook umbenennen oder löschen geht über den Worker nicht

Spam mit normalen Feedback-Nachrichten bleibt im Rahmen der Drosselung
möglich. Wird es zu viel: Limits senken oder im Worker zusätzlich ein
Steam-Session-Ticket prüfen.
