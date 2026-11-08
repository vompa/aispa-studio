import { describe, expect, it } from "vitest";

// Prüft die Hilfetexte gegen die Textregeln in docs/gestaltung/texte.md.
// Nicht geprüft werden die Kopfangaben, Codeblöcke, Inline-Code, Bildverweise sowie wörtliche
// Zitate („…“) und fett gesetzte Oberflächenbeschriftungen: Dort steht, was die App oder der Gast wörtlich sagt.

const contents = import.meta.glob<string>("./content/*.md", {
  query: "?raw",
  import: "default",
  eager: true,
});

/** Abkürzungen und Eigennamen, die in Versalien stehen dürfen. */
const allowedUppercase = new Set([
  "ADR",
  "AISPA",
  "ANSI",
  "API",
  "ASCII",
  "BIC",
  "BIOS",
  "CSV",
  "DATEV",
  "DKIM",
  "DSGVO",
  "EAN",
  "FINANZONLINE",
  "HTML",
  "HTTPS",
  "IBAN",
  "ICS",
  "IMAP",
  "JJJJ",
  "JSON",
  "KOST",
  "LODAS",
  "MWST",
  "NTCS",
  "OPI",
  "PDF",
  "PIN",
  "RKSV",
  "SEPA",
  "SKR",
  "SMS",
  "SMTP",
  "STARTTLS",
  "TSE",
  "UID",
  "URL",
  "USB",
  "VIES",
  "WLAN",
  "ZVT",
]);

interface Rule {
  name: string;
  pattern: RegExp;
}

const rules: Rule[] = [
  { name: "kein „bitte“", pattern: /\bbitte\b/i },
  { name: "kein „klicken Sie hier“", pattern: /\bklicken\s+sie\s+hier\b/i },
  {
    name: "keine Du-Form",
    pattern: /\b(du|dein|deine|deinem|deinen|deiner|deines|dir|dich)\b/i,
  },
  {
    name: "keine Richtungswörter (oben links, unten rechts …)",
    pattern:
      /\b(oben\s+links|oben\s+rechts|unten\s+links|unten\s+rechts|links\s+oben|rechts\s+oben|links\s+unten|rechts\s+unten)\b/i,
  },
  { name: "kein Ausrufezeichen", pattern: /!/ },
];

const uppercaseRule = "keine Versalien zur Betonung";

function stripFrontMatter(lines: string[]): string[] {
  if (lines[0] !== "---") return lines;
  const end = lines.indexOf("---", 1);
  return lines.map((line, index) => (index <= end ? "" : line));
}

function maskLiterals(line: string): string {
  return line
    .replace(/!\[[^\]]*\]\([^)]*\)/g, "")
    .replace(/\]\([^)]*\)/g, "]")
    .replace(/`[^`]*`/g, "")
    .replace(/„[^“]*“/g, "")
    .replace(/\*\*[^*]+\*\*/g, "");
}

function findViolations(file: string, text: string): string[] {
  const found: string[] = [];
  let inCode = false;
  stripFrontMatter(text.split(/\r?\n/)).forEach((line, index) => {
    if (line.trim().startsWith("```")) {
      inCode = !inCode;
      return;
    }
    if (inCode) return;
    const masked = maskLiterals(line);
    for (const rule of rules) {
      if (rule.pattern.test(masked))
        found.push(`${file}:${index + 1}: ${rule.name}: ${line.trim()}`);
    }
    for (const word of masked.match(/[A-ZÄÖÜ]{4,}/g) ?? []) {
      if (!allowedUppercase.has(word))
        found.push(
          `${file}:${index + 1}: ${uppercaseRule} (${word}): ${line.trim()}`,
        );
    }
  });
  return found;
}

/** Abläufe im Muster aus `web/src/help/README.md` („Muster für Abläufe“): Kopfangabe `aufbau: schritte`. */
const patternTopics = [
  "kalender-termin-buchen",
  "kalender-termin-verschieben",
  "kalender-termin-stornieren",
  "kalender-status-setzen",
  "kalender-freie-zeit-suchen",
  "kalender-paar-und-spa-tag",
  "kassa-termin-kassieren",
  "kassa-beleg-stornieren",
  "kassa-tagesabschluss",
  "kassa-gutschein-verkaufen",
];

const patternSections = [
  "Voraussetzungen",
  "Schritte",
  "Was danach",
  "Wenn etwas schiefgeht",
];

const hasPatternMarker = (text: string) =>
  /^aufbau:\s*schritte\s*$/m.test(text.split(/\r?\n---/)[0]);

/** Verstöße gegen das Muster: Kurzsatz, feste Abschnitte, nummerierte Schritte mit genau einem Bild samt Alternativtext, Verweise. */
function findPatternViolations(id: string, text: string): string[] {
  const found: string[] = [];
  const lines = stripFrontMatter(text.split(/\r?\n/));
  if (!hasPatternMarker(text))
    found.push(`${id}: Kopfangabe „aufbau: schritte“ fehlt`);
  const title = lines.findIndex((line) => line.startsWith("# "));
  const first = lines.slice(title + 1).find((line) => line.trim() !== "");
  if (title < 0 || !first || /^(#|!\[|[-*]\s|\d+\.\s)/.test(first.trim()))
    found.push(`${id}: Kurzsatz direkt nach dem Titel fehlt`);

  const sections = lines
    .filter((line) => line.startsWith("## "))
    .map((line) => line.slice(3).replace(/\s*\{#[a-z0-9-]+\}\s*$/, "").trim());
  if (patternSections.some((name, i) => sections[i] !== name))
    found.push(
      `${id}: erste Abschnitte sind „${sections.slice(0, 4).join("“, „")}“ statt „${patternSections.join("“, „")}“`,
    );

  const start = lines.findIndex((line) => /^## Schritte\b/.test(line));
  const rest = lines.slice(start + 1);
  const end = rest.findIndex((line) => line.startsWith("## "));
  const steps: string[][] = [];
  for (const line of start < 0 ? [] : rest.slice(0, end < 0 ? undefined : end)) {
    if (line.startsWith("### ")) steps.push([line]);
    else steps.at(-1)?.push(line);
  }
  if (steps.length < 2) found.push(`${id}: weniger als zwei Schritte`);
  steps.forEach((step, i) => {
    if (!step[0].startsWith(`### ${i + 1}. `))
      found.push(`${id}: Schritt ${i + 1} ist nicht „### ${i + 1}. Titel“: ${step[0]}`);
    const images = step.filter((line) => /^!\[/.test(line.trim()));
    if (images.length !== 1)
      found.push(`${id}: Schritt ${i + 1} hat ${images.length} Bilder statt eines`);
    else if (!/^!\[[^\]]{8,}\]\(images\/app\/[a-z0-9-]+\.png\)$/.test(images[0].trim()))
      found.push(`${id}: Schritt ${i + 1} hat kein Bild mit Alternativtext: ${images[0]}`);
  });

  if (!/\]\(hilfe:[a-z0-9-]+/.test(text))
    found.push(`${id}: kein Verweis auf ein verwandtes Thema (hilfe:…)`);
  return found;
}

describe("Muster für Abläufe", () => {
  const valid = [
    "---",
    "art: ablauf",
    "aufbau: schritte",
    "---",
    "# Probe",
    "",
    "Ein Satz.",
    "",
    "## Voraussetzungen",
    "- Etwas.",
    "## Schritte",
    "### 1. Eins",
    "Text.",
    "![Das Ziel ist markiert](images/app/probe-1.png)",
    "### 2. Zwei",
    "![Das Ziel ist markiert](images/app/probe-2.png)",
    "## Was danach",
    "Siehe [Kassa](hilfe:kassa).",
    "## Wenn etwas schiefgeht",
    "- Nichts.",
  ].join("\n");

  it("findet die Verstöße, die das Muster beschreibt", () => {
    expect(findPatternViolations("probe", valid)).toEqual([]);
    const withoutImage = valid.replace(
      "![Das Ziel ist markiert](images/app/probe-2.png)",
      "",
    );
    expect(findPatternViolations("probe", withoutImage)).toEqual([
      "probe: Schritt 2 hat 0 Bilder statt eines",
    ]);
    const shortAlt = valid.replaceAll("Das Ziel ist markiert", "Bild");
    expect(findPatternViolations("probe", shortAlt).length).toBe(2);
    const missingSection = valid.replace("## Was danach", "## Danach");
    expect(findPatternViolations("probe", missingSection).length).toBe(1);
    expect(
      findPatternViolations("probe", valid.replace("aufbau: schritte\n", "")).length,
    ).toBe(1);
  });

  it("Given die zehn umgestellten Abläufe When textRules.test.ts läuft Then hat jeder die Abschnitte des Musters und ein Bild je Schritt mit Alternativtext", () => {
    const violations = patternTopics.flatMap((id) => {
      const text = contents[`./content/${id}.md`];
      return text === undefined
        ? [`${id}: Thema fehlt`]
        : findPatternViolations(id, text);
    });
    expect(violations).toEqual([]);
  });

  it("hält jedes Thema mit „aufbau: schritte“ an das Muster", () => {
    const violations = Object.entries(contents)
      .filter(([, text]) => hasPatternMarker(text))
      .flatMap(([path, text]) =>
        findPatternViolations(path.replace("./content/", "").replace(".md", ""), text),
      );
    expect(violations).toEqual([]);
  });
});

describe("Textregeln der Hilfe", () => {
  it("findet die Verstöße, die die Regeln beschreiben", () => {
    const sample = [
      "---",
      "stichwoerter: du, bitte",
      "---",
      "Bitte klicken Sie hier!",
      "Das gilt für dich. Links oben und rechts unten.",
      "Das ist WICHTIG und der PDF-Export.",
      "Erlaubt: „Bitte 15 Minuten früher kommen!“ und **Wer bist du?**.",
    ].join("\n");
    const found = findViolations("probe.md", sample);
    expect(found.filter((entry) => entry.includes(":4:")).length).toBe(3);
    expect(found.filter((entry) => entry.includes(":5:")).length).toBe(2);
    expect(found.filter((entry) => entry.includes(":6:")).length).toBe(1);
    expect(found.filter((entry) => entry.includes(":7:")).length).toBe(0);
  });

  it("hält alle Hilfetexte an die Regeln aus docs/gestaltung/texte.md", () => {
    const violations = Object.entries(contents).flatMap(([path, text]) =>
      findViolations(path.replace("./content/", "web/src/help/content/"), text),
    );
    expect(violations).toEqual([]);
  });
});
