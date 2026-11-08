/**
 * Kleiner Markdown-Leser für die Hilfetexte (dieselbe Teilmenge liest die Windows-App): Überschriften mit optionalem
 * Anker `## Titel {#anker}`, Absätze, Aufzählungen (`-`) und nummerierte Schritte (`1.`), dazu **fett**, `Code` und
 * [Links](https://…), Verweise in die Hilfe `[Text](hilfe:thema#anker)` und Bilder in eigener Zeile `![Text](images/app/name.png)` bzw. `images/web/name.png`.
 * Kopfangaben stehen zwischen `---` (siehe `splitFrontMatter`). Mehr brauchen die Hilfetexte nicht, deshalb keine eigene Bibliothek.
 */
export type Span = {
  text: string;
  bold?: boolean;
  code?: boolean;
  href?: string;
};

export type Block =
  | { kind: "heading"; level: number; text: string; id: string }
  | { kind: "paragraph"; spans: Span[] }
  | { kind: "list"; ordered: boolean; items: Span[][] }
  | { kind: "image"; name: string; alt: string }
  | { kind: "callout"; type: CalloutType; spans: Span[] };

/** Hinweiskasten: `> Hinweis:` (neutral), `> Tipp:` (grün), `> Achtung:` (Warnung, nie rot; Design-Brief 5.9). */
export type CalloutType = "hint" | "tip" | "warning";

const calloutTypes: Record<string, CalloutType> = {
  Hinweis: "hint",
  Tipp: "tip",
  Achtung: "warning",
};

export const calloutLabels: Record<CalloutType, string> = {
  hint: "Hinweis",
  tip: "Tipp",
  warning: "Achtung",
};

export type HelpKind = "einstellung" | "ablauf" | "seite";

/** Kopfangaben eines Themas, siehe `web/src/help/README.md`. */
export interface HelpMeta {
  kind: HelpKind;
  area?: string;
  surfaces: string[];
  roles: string[];
  page?: string;
  keywords: string[];
}

export const emptyMeta: HelpMeta = {
  kind: "einstellung",
  surfaces: [],
  roles: [],
  keywords: [],
};

export function splitFrontMatter(source: string): {
  meta: HelpMeta;
  body: string;
} {
  const text = source.replace(/\r\n/g, "\n");
  const end = text.startsWith("---\n") ? text.indexOf("\n---", 3) : -1;
  if (end < 0) return { meta: emptyMeta, body: text };
  const values = new Map<string, string>();
  for (const line of text.slice(4, end).split("\n")) {
    const colon = line.indexOf(":");
    if (colon > 0)
      values.set(line.slice(0, colon).trim().toLowerCase(), line.slice(colon + 1).trim());
  }
  const list = (key: string) =>
    (values.get(key) ?? "")
      .split(",")
      .map((v) => v.trim())
      .filter(Boolean);
  const kind = values.get("art");
  const bodyStart = text.indexOf("\n", end + 1);
  return {
    meta: {
      kind: kind === "ablauf" || kind === "seite" ? kind : "einstellung",
      area: values.get("bereich") || undefined,
      surfaces: list("oberflaeche").map((v) => v.toLowerCase()),
      roles: list("rollen").map((v) => v.toLowerCase()),
      page: values.get("seite") || undefined,
      keywords: list("stichwoerter"),
    },
    body: bodyStart < 0 ? "" : text.slice(bodyStart + 1),
  };
}

/** Thema und Anker eines Verweises `hilfe:thema#anker`, sonst `null`. */
export function helpLink(
  href: string | undefined,
): { topic: string; section?: string } | null {
  if (!href?.startsWith("hilfe:")) return null;
  const [topic, section] = href.slice(6).split("#");
  return { topic, section: section || undefined };
}

export const plain = (spans: Span[]) => spans.map((s) => s.text).join("");

/** Klein und ohne Umlaute, damit „Räume“ und „raeume“ gleich verglichen werden. */
export function fold(text: string): string {
  return text
    .toLowerCase()
    .replace(/ä/g, "ae")
    .replace(/ö/g, "oe")
    .replace(/ü/g, "ue")
    .replace(/ß/g, "ss");
}

/** Anker aus einem Titel: klein, ohne Umlaute und Satzzeichen, Wörter mit Bindestrich. */
export function slug(text: string): string {
  return fold(text)
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function parseInline(text: string): Span[] {
  const spans: Span[] = [];
  const pattern = /\*\*(.+?)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)/g;
  let last = 0;
  for (const match of text.matchAll(pattern)) {
    if (match.index > last) spans.push({ text: text.slice(last, match.index) });
    if (match[1] !== undefined) spans.push({ text: match[1], bold: true });
    else if (match[2] !== undefined) spans.push({ text: match[2], code: true });
    else spans.push({ text: match[3], href: match[4] });
    last = match.index + match[0].length;
  }
  if (last < text.length) spans.push({ text: text.slice(last) });
  return spans;
}

export function parseMarkdown(source: string): Block[] {
  const blocks: Block[] = [];
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;
  let callout: { type: CalloutType; lines: string[] } | null = null;
  const flush = () => {
    if (callout)
      blocks.push({
        kind: "callout",
        type: callout.type,
        spans: parseInline(callout.lines.join(" ")),
      });
    callout = null;
    if (paragraph.length)
      blocks.push({
        kind: "paragraph",
        spans: parseInline(paragraph.join(" ")),
      });
    if (list)
      blocks.push({
        kind: "list",
        ordered: list.ordered,
        items: list.items.map(parseInline),
      });
    paragraph = [];
    list = null;
  };
  for (const raw of source.replace(/\r\n/g, "\n").split("\n")) {
    const line = raw.trim();
    const heading = /^(#{1,4})\s+(.*?)(?:\s*\{#([a-z0-9-]+)\})?$/.exec(line);
    const bullet = /^[-*]\s+(.*)$/.exec(line);
    const numbered = /^\d+\.\s+(.*)$/.exec(line);
    const image = /^!\[([^\]]*)\]\(images\/((?:app|web)\/[a-z0-9-]+)\.png\)$/.exec(line);
    const quote = /^>\s?(.*)$/.exec(line);
    const label = quote && /^(Hinweis|Tipp|Achtung):\s*(.*)$/.exec(quote[1]);
    if (!line) {
      flush();
    } else if (label) {
      flush();
      callout = { type: calloutTypes[label[1]], lines: [label[2]] };
    } else if (quote && callout) {
      callout.lines.push(quote[1]);
    } else if (image) {
      flush();
      blocks.push({ kind: "image", name: image[2], alt: image[1] });
    } else if (heading) {
      flush();
      blocks.push({
        kind: "heading",
        level: heading[1].length,
        text: heading[2],
        id: heading[3] ?? slug(heading[2]),
      });
    } else if (bullet || numbered) {
      const ordered = !!numbered;
      if (paragraph.length || (list && list.ordered !== ordered)) flush();
      list ??= { ordered, items: [] };
      list.items.push((bullet ?? numbered)![1]);
    } else if (list && raw.startsWith("  ")) {
      // Fortsetzungszeile eines Listenpunkts.
      list.items[list.items.length - 1] += ` ${line}`;
    } else {
      if (list) flush();
      paragraph.push(line);
    }
  }
  flush();
  return blocks;
}
