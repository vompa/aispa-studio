import {
  emptyMeta,
  helpLink,
  parseMarkdown,
  plain,
  splitFrontMatter,
  type Block,
  type HelpMeta,
} from "./markdown";
import imageSizes from "virtual:help-image-sizes";

/** Ein Hilfethema: eine Datei unter `content/`, der Dateiname ist die Kennung, die erste Überschrift der Titel. */
export interface HelpTopic {
  id: string;
  title: string;
  blocks: Block[];
  meta: HelpMeta;
  /** Erster Absatz vor der ersten Zwischenüberschrift. */
  summary: string;
}

// Erst beim Öffnen der Hilfe geladen; die Texte liegen im Build und brauchen keinen Server und kein Internet.
const files = import.meta.glob<string>("./content/*.md", {
  query: "?raw",
  import: "default",
});

// Nur die Adressen; die Bilder selbst lädt der Browser erst, wenn ein Thema sie zeigt.
const images = import.meta.glob<string>("./images/{app,web}/*.png", {
  query: "?url",
  import: "default",
  eager: true,
});

/** Maße der Bilder, zur Bauzeit aus den PNG-Köpfen gelesen (`vite.config.ts`). */
export const imageSize = (name: string): { width: number; height: number } | undefined => {
  const size = imageSizes[name];
  return size && { width: size[0], height: size[1] };
};

/** Adresse eines Bildes der Hilfe (`app/<name>` oder `web/<name>`), `undefined` wenn es (noch) nicht erzeugt ist. */
export const imageUrl = (name: string): string | undefined =>
  images[`./images/${name}.png`];

export function toTopic(id: string, source: string): HelpTopic {
  const { meta, body } = splitFrontMatter(source);
  const blocks = parseMarkdown(body);
  const first = blocks[0];
  const hasTitle = first?.kind === "heading" && first.level === 1;
  const rest = hasTitle ? blocks.slice(1) : blocks;
  const intro = rest.find(
    (b, i) =>
      b.kind === "paragraph" &&
      rest.slice(0, i).every((x) => x.kind !== "heading"),
  );
  return {
    id,
    title: hasTitle ? first.text : id,
    blocks: rest,
    meta: meta ?? emptyMeta,
    summary: intro?.kind === "paragraph" ? plain(intro.spans) : "",
  };
}

/** Überschrift des Abschnitts und alles bis zur nächsten Überschrift gleicher oder höherer Ebene. */
export function sectionBlocks(topic: HelpTopic, id: string): Block[] {
  const start = topic.blocks.findIndex(
    (b) => b.kind === "heading" && b.id === id,
  );
  if (start < 0) return [];
  const head = topic.blocks[start];
  const level = head.kind === "heading" ? head.level : 0;
  const end = topic.blocks.findIndex(
    (b, i) => i > start && b.kind === "heading" && b.level <= level,
  );
  return topic.blocks.slice(start, end < 0 ? undefined : end);
}

let loaded: Promise<HelpTopic[]> | null = null;

export function loadTopics(): Promise<HelpTopic[]> {
  loaded ??= Promise.all(
    Object.entries(files).map(async ([path, load]) =>
      toTopic(path.replace(/^.*\/|\.md$/g, ""), await load()),
    ),
  ).then((topics) =>
    topics.sort((a, b) => a.title.localeCompare(b.title, "de")),
  );
  return loaded;
}

/** Bereich für Brotkrumen und Trefferliste: die Kopfangabe `bereich`, sonst die Art des Themas wie in der Übersicht. */
export function areaOf(topic: HelpTopic): string {
  if (topic.meta.area) return topic.meta.area;
  return topic.meta.kind === "seite"
    ? "Seiten"
    : topic.meta.kind === "einstellung"
      ? "Einstellungen"
      : "Allgemein";
}

/** Zwischenüberschriften der ersten Ebene (`h2` auf der Hilfe-Seite) für „Auf dieser Seite“. */
export function sectionsOf(topic: HelpTopic): { id: string; text: string }[] {
  return topic.blocks.flatMap((b) =>
    b.kind === "heading" && b.level === 2 ? [{ id: b.id, text: b.text }] : [],
  );
}

const roleNames: Record<string, string> = {
  rezeption: "Rezeption",
  therapeut: "Therapie",
  leitung: "Leitung",
  inhaber: "Inhaberin",
  manager: "Management",
  backoffice: "Backoffice",
};

/** Name einer Rolle für Kennzeichen und Filter. */
export const roleName = (role: string): string => roleNames[role] ?? role;

/** Rollen aus der Kopfangabe `rollen` als Namen für das Kennzeichen „Für: …“. */
export const roleLabels = (topic: HelpTopic): string[] =>
  topic.meta.roles.map(roleName);

const leadingAreas = ["Kalender", "Kassa", "Kunden"];

/** Themen nach Bereich für die Navigation: Kalender, Kassa, Kunden zuerst, die übrigen Bereiche alphabetisch; jedes Thema einmal. */
export function groupByArea(topics: HelpTopic[]): [string, HelpTopic[]][] {
  const map = new Map<string, HelpTopic[]>();
  for (const t of topics) map.set(areaOf(t), [...(map.get(areaOf(t)) ?? []), t]);
  const rank = (name: string) => {
    const i = leadingAreas.indexOf(name);
    return i < 0 ? leadingAreas.length : i;
  };
  return [...map.entries()].sort(
    ([a], [b]) => rank(a) - rank(b) || a.localeCompare(b, "de"),
  );
}

/** Themen, auf die der Text mit `hilfe:`-Links verweist, in Textreihenfolge, einmal je Thema, ohne das Thema selbst. */
export function relatedTopics(topic: HelpTopic, all: HelpTopic[]): HelpTopic[] {
  const ids: string[] = [];
  const take = (spans: { href?: string }[]) => {
    for (const span of spans) {
      const id = helpLink(span.href)?.topic;
      if (id && id !== topic.id && !ids.includes(id)) ids.push(id);
    }
  };
  for (const b of topic.blocks) {
    if (b.kind === "paragraph" || b.kind === "callout") take(b.spans);
    else if (b.kind === "list") b.items.forEach(take);
  }
  return ids.flatMap((id) => all.find((t) => t.id === id) ?? []);
}
