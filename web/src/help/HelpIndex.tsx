import { useMemo, useState } from "react";
import { openHelp } from "./help";
import {
  highlight,
  queryWords,
  searchHelp,
  type HelpContext,
} from "./search";
import { firstStepIds, orderedRoleEntries } from "./roleEntries";
import { areaOf, groupByArea, roleName, type HelpTopic } from "./topics";

const kindOrder = { ablauf: 0, seite: 1, einstellung: 2 } as const;
const maxHits = 12;

/** Die Zahl der Treffer als Satz für die höfliche Statuszeile; bei der Obergrenze heißt es „besten“, weil es mehr geben kann. */
function hitsText(count: number) {
  if (count === 0) return "Nichts gefunden. Anders formulieren oder die KI fragen.";
  if (count === 1) return "Ein Treffer";
  return count >= maxHits ? `Die ${maxHits} besten Treffer` : `${count} Treffer`;
}

/** Text mit hervorgehobenen Suchwörtern (`<mark>`). */
function Marked({ text, words }: { text: string; words: string[] }) {
  return (
    <>
      {highlight(text, words).map((part, i) =>
        part.mark ? <mark key={i}>{part.text}</mark> : part.text,
      )}
    </>
  );
}

/** Übersicht: Abläufe nach Bereich, dann Seiten, dann Einstellungen. */
function groups(topics: HelpTopic[]): [string, HelpTopic[]][] {
  const map = new Map<string, HelpTopic[]>();
  const sorted = [...topics].sort(
    (a, b) => kindOrder[a.meta.kind] - kindOrder[b.meta.kind],
  );
  for (const t of sorted) {
    const name =
      t.meta.kind === "ablauf"
        ? `Abläufe: ${t.meta.area ?? "Allgemein"}`
        : t.meta.kind === "seite"
          ? "Seiten"
          : "Einstellungen";
    map.set(name, [...(map.get(name) ?? []), t]);
  }
  return [...map.entries()];
}

/** Eine einklappbare Gruppe der Navigation auf der Hilfe-Seite; offen, wenn der Nutzer sie geöffnet hat oder das aktuelle Thema darin steht. */
function TopicGroup({
  name,
  list,
  current,
  open,
  onToggle,
}: {
  name: string;
  list: HelpTopic[];
  current?: string | null;
  open: boolean | undefined;
  onToggle: (open: boolean) => void;
}) {
  return (
    <details
      className="help-group"
      open={open ?? list.some((t) => t.id === current)}
      onToggle={(e) => onToggle(e.currentTarget.open)}
    >
      <summary>{name}</summary>
      <ul className="help-topics">
        {list.map((t) => (
          <li key={t.id}>
            <button
              className="ghost"
              data-topic={t.id}
              aria-current={t.id === current ? "page" : undefined}
              onClick={() => openHelp(t.id)}
            >
              {t.title}
            </button>
          </li>
        ))}
      </ul>
    </details>
  );
}

const firstStepsName = "Erste Schritte";

/**
 * Suche und Übersicht aller Themen, gemeinsam für die Schublade in der Verwaltung und die Hilfe-Seite (`/help/`).
 * `current` ist das geöffnete Thema; der Eintrag trägt dann `aria-current`.
 */
export function HelpIndex({
  topics,
  context,
  current,
  page = false,
}: {
  topics: HelpTopic[];
  context?: HelpContext;
  current?: string | null;
  /** Hilfe-Seite: Themen nach Bereich in einklappbaren Gruppen, mit Rollenfilter; sonst die Übersicht der Schublade. */
  page?: boolean;
}) {
  const [query, setQuery] = useState("");
  const [ownRoleOnly, setOwnRoleOnly] = useState(true);
  const [toggled, setToggled] = useState<Record<string, boolean>>({});
  const role = context?.role;
  const shown = useMemo(
    () =>
      ownRoleOnly && role
        ? topics.filter((t) => t.meta.roles.includes(role))
        : topics,
    [topics, ownRoleOnly, role],
  );
  const hits = useMemo(
    () => (query.trim() ? searchHelp(topics, query, context, maxHits) : null),
    [topics, query, context],
  );

  // T-028: „Erste Schritte“ als eigene Gruppe; ihre Themen stehen nicht noch einmal in ihrem Bereich.
  const [firstSteps, rest] = useMemo(() => {
    const byShown = new Map(shown.map((t) => [t.id, t]));
    const steps = firstStepIds(ownRoleOnly ? role : undefined)
      .map((id) => byShown.get(id))
      .filter((t): t is HelpTopic => t !== undefined);
    const ids = new Set(steps.map((t) => t.id));
    return [steps, shown.filter((t) => !ids.has(t.id))] as const;
  }, [shown, ownRoleOnly, role]);

  const words = useMemo(() => queryWords(query), [query]);
  const entries = useMemo(() => orderedRoleEntries(context?.role), [context?.role]);
  const byId = useMemo(() => new Map(topics.map((t) => [t.id, t])), [topics]);
  const areas = useMemo(
    () => [...new Set(topics.map(areaOf))].sort((a, b) => a.localeCompare(b, "de")),
    [topics],
  );

  return (
    <div className="help-index">
      <input
        type="search"
        className="help-search"
        placeholder="Suchen, z. B. Termin verschieben"
        aria-label="Hilfe durchsuchen"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      {/* Steht schon vor der ersten Suche im Dokument, sonst sagt ein Screenreader die erste Zahl nicht an. */}
      <p className="muted" role="status" aria-live="polite">
        {hits && hitsText(hits.length)}
      </p>
      {hits ? (
        hits.length ? (
          <ul className="help-topics">
            {hits.map((h) => (
              <li key={`${h.topic.id}#${h.section ?? ""}`}>
                <button
                  className="ghost"
                  onClick={() => openHelp(h.topic.id, h.section)}
                >
                  <span className="help-hit-area">{h.area}</span>
                  <strong>
                    <Marked text={h.title} words={words} />
                  </strong>
                  <span className="muted">
                    {" "}
                    <Marked text={h.excerpt} words={words} />
                  </span>
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <div className="help-alternatives">
            <p className="muted">{`Bereiche: ${areas.join(", ")}.`}</p>
            <button className="ghost" onClick={() => setQuery("")}>
              Alle Themen anzeigen
            </button>
          </div>
        )
      ) : page ? (
        <>
          {context?.role && (
            <button
              type="button"
              className="help-role-chip"
              aria-pressed={ownRoleOnly}
              onClick={() => setOwnRoleOnly(!ownRoleOnly)}
            >
              {ownRoleOnly ? `Meine Rolle: ${roleName(context.role)}` : "Alle"}
            </button>
          )}
          {[
            ...(firstSteps.length ? [[firstStepsName, firstSteps] as [string, HelpTopic[]]] : []),
            ...groupByArea(rest).map(
              ([area, list]) =>
                [area, [...list].sort((a, b) => kindOrder[a.meta.kind] - kindOrder[b.meta.kind])] as [string, HelpTopic[]],
            ),
          ].map(([name, list]) => (
            <TopicGroup
              key={name}
              name={name}
              list={list}
              current={current}
              open={toggled[name]}
              onToggle={(open) =>
                setToggled((prev) => (prev[name] === open ? prev : { ...prev, [name]: open }))
              }
            />
          ))}
          {/* Mit index.html, damit der Link auch im lokalen Ordner der Windows-App (Einzelplatz) aufgeht. */}
          <p>
            <a href="/ueber/index.html">
              Über AISPA Studio: eine Konzeptstudie zur KI-nativen Softwareentwicklung, mit Betriebsarten und angebundenen Diensten
            </a>
          </p>
        </>
      ) : (
        <>
          <p className="muted">
            Abläufe Schritt für Schritt, jede Seite und jede Einstellung; das
            Fragezeichen neben einer Einstellung öffnet sie direkt.
          </p>
          <section className="help-entry" aria-labelledby="help-entry-title">
            <h3 id="help-entry-title">Erste Schritte</h3>
            <p className="muted">Die ersten Abläufe je Rolle.</p>
            {entries.map((entry) => (
              <div key={entry.role} role="group" aria-labelledby={`help-entry-${entry.role}`}>
                <h4 id={`help-entry-${entry.role}`}>{entry.label}</h4>
                <ul className="help-topics">
                  {entry.topics.flatMap((id) => {
                    const t = byId.get(id);
                    return t
                      ? [
                          <li key={id}>
                            <button className="ghost" onClick={() => openHelp(id)}>
                              {t.title}
                            </button>
                          </li>,
                        ]
                      : [];
                  })}
                </ul>
              </div>
            ))}
          </section>
          {groups(topics).map(([name, list]) => (
            <section key={name}>
              <h3>{name}</h3>
              <ul className="help-topics">
                {list.map((t) => (
                  <li key={t.id}>
                    <button
                      className="ghost"
                      aria-current={t.id === current ? "page" : undefined}
                      onClick={() => openHelp(t.id)}
                    >
                      {t.title}
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </>
      )}
    </div>
  );
}
