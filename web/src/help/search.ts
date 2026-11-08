import { fold, plain } from "./markdown";
import { areaOf, type HelpTopic } from "./topics";

/** Wer fragt und von wo: Oberfläche (`windows-app`, `verwaltung`, `handy`), Rolle und aktuelle Seite. */
export interface HelpContext {
  surface?: string;
  role?: string;
  page?: string;
}

export interface HelpHit {
  topic: HelpTopic;
  section?: string;
  title: string;
  /** Bereich des Themas für die Trefferliste. */
  area: string;
  excerpt: string;
  score: number;
}

const minScore = 4;

const stopWords = new Set(
  (
    "wie mache machen macht man ich kann koennen kannst einen eine einer ein der die das den dem des und oder mit fuer im in am an " +
    "auf zu zum zur was wo wann wer bitte hilfe anleitung geht gehe mich mir wir sie es ist sind soll sollte muss moechte will hier " +
    "dort da denn jetzt noch schon auch nicht kein keine von vom bei nach funktioniert finde finden erklaer erklaere zeig zeige how " +
    "do to the can where what is my a of for on it this that i"
  ).split(" "),
);

/** Rolle des Zugangs (`StaffRole`) als Kennung der Kopfangabe `rollen`. */
export const roleKey = (role: string | undefined) =>
  ({
    Owner: "inhaber",
    GeneralManager: "manager",
    Manager: "leitung",
    Backoffice: "backoffice",
    Reception: "rezeption",
    Therapist: "therapeut",
  })[role ?? ""];

export const words = (text: string) =>
  (fold(text).match(/[a-z0-9]+/g) ?? []).filter((w) => w.length >= 2);

/** Grobe Grundform: übliche deutsche Endungen ab, aber nie kürzer als vier Buchstaben. */
export function stem(word: string): string {
  for (const ending of ["ungen", "ung", "en", "er", "es", "e", "n", "s", "t"])
    if (word.length - ending.length >= 4 && word.endsWith(ending))
      return word.slice(0, -ending.length);
  return word;
}

function best(word: string, candidates: string[]): number {
  let result = 0;
  for (const c of candidates) {
    const value =
      c === word
        ? 1
        : stem(c) === stem(word) && stem(word).length >= 4
          ? 0.8
          : (word.length >= 5 && c.includes(stem(word))) ||
              (c.length >= 5 && word.includes(stem(c)))
            ? 0.6
            : 0;
    if (value > result) {
      result = value;
      if (result >= 1) break;
    }
  }
  return result;
}

interface Part {
  id?: string;
  title: string;
  heading: string[];
  body: string[];
  excerpt: string;
}

function parts(topic: HelpTopic): Part[] {
  const result: Part[] = [
    { title: topic.title, heading: [], body: [], excerpt: "" },
  ];
  for (const block of topic.blocks) {
    const current = result[result.length - 1];
    if (block.kind === "heading") {
      result.push({
        id: block.id,
        title: block.text,
        heading: words(block.text),
        body: [],
        excerpt: "",
      });
    } else if (block.kind === "paragraph") {
      const text = plain(block.spans);
      current.body.push(...words(text));
      current.excerpt ||= text;
    } else if (block.kind === "list") {
      for (const item of block.items) current.body.push(...words(plain(item)));
      current.excerpt ||= block.items[0] ? plain(block.items[0]) : "";
    } else if (block.kind === "callout") {
      const text = plain(block.spans);
      current.body.push(...words(text));
      current.excerpt ||= text;
    } else {
      current.body.push(...words(block.alt));
    }
  }
  return result;
}

function score(
  topic: HelpTopic,
  query: string[],
  context: HelpContext,
): HelpHit | null {
  const title = words(topic.title);
  const keywords = topic.meta.keywords.flatMap(words);
  const sections = parts(topic);
  const headings = sections.flatMap((s) => s.heading);
  const body = sections.flatMap((s) => s.body);

  let value = 0;
  let matched = 0;
  for (const word of query) {
    const b = Math.max(
      6 * best(word, title),
      5 * best(word, keywords),
      3 * best(word, headings),
      best(word, body),
    );
    value += b;
    if (b > 0) matched++;
  }
  if (matched === 0) return null;

  value *= 0.5 + (0.5 * matched) / query.length;
  value *=
    topic.meta.kind === "ablauf" ? 1.3 : topic.meta.kind === "seite" ? 1 : 0.9;
  if (
    context.role &&
    (topic.meta.roles.length === 0 || topic.meta.roles.includes(context.role))
  )
    value *= 1.15;
  if (context.surface && topic.meta.surfaces.includes(context.surface))
    value *= 1.15;
  if (context.page && topic.meta.page?.toLowerCase() === context.page.toLowerCase())
    value *= 1.2;

  // Der Abschnitt, dessen Überschrift die Frage trifft; sonst das ganze Thema.
  const section = sections
    .filter((s) => s.id)
    .map((s) => ({
      s,
      value: query.reduce(
        (sum, w) => sum + 3 * best(w, s.heading) + best(w, s.body),
        0,
      ),
      heading: query.reduce((sum, w) => sum + best(w, s.heading), 0),
    }))
    .filter((x) => x.heading > 0)
    .sort((a, b) => b.value - a.value)[0]?.s;
  return {
    topic,
    section: section?.id,
    title: section ? `${topic.title}: ${section.title}` : topic.title,
    area: areaOf(topic),
    excerpt: section?.excerpt || topic.summary,
    score: value,
  };
}

/** Die Wörter einer Suchanfrage ohne Füllwörter („wie mache ich …“), klein und ohne Umlaute. */
export const queryWords = (query: string): string[] => [
  ...new Set(words(query).filter((w) => !stopWords.has(w))),
];

export interface HighlightPart {
  text: string;
  mark: boolean;
}

/** Teilt `text` in Stücke und markiert die Wörter, die den Wortstamm eines Suchworts enthalten (Umlaute egal). */
export function highlight(text: string, query: string[]): HighlightPart[] {
  const stems = query.map((q) => stem(q));
  const parts: HighlightPart[] = [];
  text = text.normalize("NFC");
  const push = (value: string, mark: boolean) => {
    const last = parts[parts.length - 1];
    if (last && last.mark === mark) last.text += value;
    else parts.push({ text: value, mark });
  };
  let end = 0;
  for (const m of text.matchAll(/[\p{L}\p{N}]+/gu)) {
    if (m.index > end) push(text.slice(end, m.index), false);
    const token = fold(m[0]);
    push(
      m[0],
      stems.some((s, i) =>
        s.length >= 3 ? token.startsWith(s) : token === query[i],
      ),
    );
    end = m.index + m[0].length;
  }
  if (end < text.length) push(text.slice(end), false);
  return parts;
}

/**
 * Suche im Handbuch ohne Server und ohne KI, gleiche Rangfolge wie `AispaStudio.Help.HelpSearch` (Windows-App und
 * Assistent): Titel vor Stichwörtern vor Überschriften vor Text, Abläufe vor Einstellungen, passende Rolle, Oberfläche
 * und Seite zuerst.
 */
export function searchHelp(
  topics: HelpTopic[],
  query: string,
  context: HelpContext = {},
  max = 5,
): HelpHit[] {
  const q = queryWords(query);
  if (q.length === 0) return [];
  return topics
    .map((t) => score(t, q, context))
    .filter((h): h is HelpHit => !!h && h.score >= minScore)
    .sort((a, b) => b.score - a.score || a.title.localeCompare(b.title))
    .slice(0, max);
}
