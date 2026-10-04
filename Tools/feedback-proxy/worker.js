// Feedback-Proxy fuer Little Biscuit.
//
// Das Spiel schickt seine Rueckmeldung hierher statt direkt an Discord. Der
// Discord-Webhook liegt nur hier als Secret (DISCORD_WEBHOOK) - im Spiel steht
// er nicht mehr, also kann ihn auch niemand aus dem Build ziehen.
//
// Was der Worker tut:
//   * nur POST mit JSON, hoechstens 16 KB
//   * drosseln: pro IP und insgesamt (Rate-Limit-Bindings in wrangler.toml)
//   * die Discord-Nachricht NEU bauen - nur Titel, Text und die bekannten
//     Felder werden uebernommen, gekuerzt; Name/Avatar/Pings sind fest.
//     Wer die Proxy-URL kennt, kann also hoechstens eine normale
//     Feedback-Nachricht schicken, nicht @everyone pingen oder den Webhook
//     umbenennen.

const MAX_BODY = 16 * 1024;
const MAX_MESSAGE = 1000;
const MAX_FIELD = 1024;

// Genau die Felder, die FeedbackReport.BuildJson schickt.
const FIELDS = ["Von", "Version", "Sprache", "Szene", "Laufzeit", "Bildschirm", "System", "Hardware", "Unity"];

const BUG = { title: "\u{1F41E} Bug-Report", color: 0xd4566c };
const FEEDBACK = { title: "\u{1F4AC} Feedback", color: 0x6fb7e0 };

export default {
  async fetch(request, env) {
    if (request.method !== "POST") return text("Method not allowed", 405);
    if (!env.DISCORD_WEBHOOK) return text("Not configured", 500);

    const ip = request.headers.get("CF-Connecting-IP") || "unknown";
    if (env.PER_IP && !(await env.PER_IP.limit({ key: ip })).success) return text("Too many requests", 429);
    if (env.GLOBAL && !(await env.GLOBAL.limit({ key: "all" })).success) return text("Too many requests", 429);

    const raw = await request.text();
    if (raw.length > MAX_BODY) return text("Too large", 413);

    let data;
    try { data = JSON.parse(raw); } catch { return text("Bad request", 400); }

    const embed = Array.isArray(data?.embeds) ? data.embeds[0] : null;
    if (!embed || typeof embed.description !== "string" || !embed.description.trim()) {
      return text("Bad request", 400);
    }

    const kind = typeof embed.title === "string" && embed.title.includes("Bug") ? BUG : FEEDBACK;

    const fields = [];
    if (Array.isArray(embed.fields)) {
      for (const f of embed.fields) {
        if (!f || !FIELDS.includes(f.name) || typeof f.value !== "string") continue;
        if (fields.some(x => x.name === f.name)) continue;
        fields.push({ name: f.name, value: clip(f.value, MAX_FIELD) || "-", inline: f.inline === true });
      }
    }

    const payload = {
      username: "Little Biscuit Feedback",
      allowed_mentions: { parse: [] },
      embeds: [{
        title: kind.title,
        description: clip(embed.description, MAX_MESSAGE),
        color: kind.color,
        timestamp: new Date().toISOString(),
        fields,
      }],
    };

    const res = await fetch(env.DISCORD_WEBHOOK, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    // Discord-Fehlertexte nicht durchreichen - die verraten nichts Nuetzliches.
    return res.ok ? text("ok", 200) : text("Upstream error", 502);
  },
};

function clip(s, max) {
  s = String(s).trim();
  return s.length <= max ? s : s.slice(0, max - 1) + "…";
}

function text(body, status) {
  return new Response(body, { status, headers: { "Content-Type": "text/plain" } });
}
