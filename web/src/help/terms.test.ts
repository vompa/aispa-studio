import { describe, expect, it } from "vitest";

// Ein Begriff „Mitarbeitende“ in Hilfe und Web-Oberfläche und kurze Hinweise (Slice 79, Ä101, Ä102;
// docs/gestaltung/texte.md T12 und T22). Die Kopfangaben der Hilfetexte (Suchwörter) bleiben ungeprüft:
// dort dürfen die alten Wörter stehen, damit die Suche sie findet.

const helpTexts = import.meta.glob<string>("./content/*.md", { query: "?raw", import: "default", eager: true });
const sources = import.meta.glob<string>(["../**/*.tsx", "!../**/*.test.tsx"], { query: "?raw", import: "default", eager: true });

/** Alte Sammelbegriffe; „einen Therapeuten“ ist der Gästewunsch und bleibt erlaubt, „MA“ als akademischer Titel auch. */
const oldTerms: RegExp[] = [
  /\bMA\b(?!,? ?(?:PhD|MBA))/,
  /Personen ohne Dienst/,
  /(?<!einen )\bTherapeuten\b/,
  /\bMitarbeiter\b(?!:in)/,
];

function body(markdown: string): string[] {
  const lines = markdown.split(/\r?\n/);
  const start = lines[0] === "---" ? lines.indexOf("---", 1) + 1 : 0;
  return lines.slice(start);
}

function stripFencedCode(lines: string[]): string[] {
  let inCode = false;
  return lines.filter((line) => {
    if (line.startsWith("```")) {
      inCode = !inCode;
      return false;
    }
    return !inCode;
  });
}

describe("Begriffe: Mitarbeitende", () => {
  it("Given die Hilfetexte When nach MA, Personen ohne Dienst und Therapeuten gesucht wird Then gibt es keinen Treffer", () => {
    const hits: string[] = [];
    for (const [path, markdown] of Object.entries(helpTexts)) {
      for (const line of stripFencedCode(body(markdown))) {
        for (const term of oldTerms) {
          if (term.test(line)) hits.push(`${path}: ${term} in „${line.slice(0, 120)}“`);
        }
      }
    }
    expect(hits).toEqual([]);
  });

  it("Given die Texte der Web-Oberfläche When nach MA, Personen ohne Dienst und Therapeuten gesucht wird Then gibt es keinen Treffer", () => {
    const hits: string[] = [];
    for (const [path, source] of Object.entries(sources)) {
      for (const literal of source.match(/"[^"\n]*"|`[^`\n]*`/g) ?? []) {
        if (!/\s/.test(literal)) continue;
        for (const term of oldTerms) {
          if (term.test(literal)) hits.push(`${path}: ${term} in ${literal.slice(0, 120)}`);
        }
      }
    }
    expect(hits).toEqual([]);
  });
});

describe("Hinweise (title) der Web-Oberfläche", () => {
  it("Given ein title-Hinweis When seine Länge geprüft wird Then hat er höchstens einen Satz und höchstens zwei Kommas", () => {
    const problems: string[] = [];
    for (const [path, source] of Object.entries(sources)) {
      for (const match of source.matchAll(/<(?:button|a|span|div|img|input|label)\b[^<>]*?\btitle="([^"]*)"/g)) {
        const tip = match[1].replace(/z\. ?B\.|bzw\.|ca\./g, "");
        if ((tip.match(/[.!?](?=\s|$)/g) ?? []).length > 1) problems.push(`${path}: mehr als ein Satz „${match[1]}“`);
        if ((tip.match(/,/g) ?? []).length > 2) problems.push(`${path}: zählt auf „${match[1]}“`);
      }
    }
    expect(problems).toEqual([]);
  });
});
