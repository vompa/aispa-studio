import type { ReactNode } from "react";
import { openHelp } from "./help";
import { HelpFeedback } from "./HelpFeedback";
import {
  areaOf,
  relatedTopics,
  roleLabels,
  sectionsOf,
  type HelpTopic,
} from "./topics";

/**
 * Kopf eines Themas: Brotkrumen „Hilfe › Bereich › Thema“, dazwischen der Titel (`children`), darunter das Rollenkennzeichen
 * und „Auf dieser Seite“ (nur mit mindestens einer Zwischenüberschrift). Gemeinsam für die Hilfe-Seite und die Schublade.
 */
export function TopicHead({
  topic,
  children,
  toc = true,
}: {
  topic: HelpTopic;
  children?: ReactNode;
  /** `false`: kein Verzeichnis im Kopf (die Hilfe-Seite zeigt es mit `TopicToc` neben oder über dem Text). */
  toc?: boolean;
}) {
  const sections = toc ? sectionsOf(topic) : [];
  const roles = roleLabels(topic);
  return (
    <>
      <nav className="help-crumbs" aria-label="Brotkrumen">
        <ol>
          <li>
            <button className="ghost" onClick={() => openHelp(null)}>
              Hilfe
            </button>
          </li>
          <li>{areaOf(topic)}</li>
          <li aria-current="page">{topic.title}</li>
        </ol>
      </nav>
      {children}
      {roles.length > 0 && (
        <p className="help-roles muted">{`Für: ${roles.join(", ")}`}</p>
      )}
      {sections.length > 0 && (
        <nav className="help-toc" aria-label="Auf dieser Seite">
          <p className="help-toc-title">Auf dieser Seite</p>
          <ul>
            {sections.map((s) => (
              <li key={s.id}>
                <a
                  href={`#hilfe-${s.id}`}
                  onClick={(e) => {
                    e.preventDefault();
                    openHelp(topic.id, s.id);
                  }}
                >
                  {s.text}
                </a>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </>
  );
}

/**
 * „Auf dieser Seite“ der Hilfe-Seite: breit eine schmale mitlaufende Liste rechts (`aside`), schmal ein zugeklapptes „Inhalt“
 * über dem Text. `active` ist der Abschnitt, der gerade gelesen wird.
 */
export function TopicToc({
  topic,
  wide,
  active,
}: {
  topic: HelpTopic;
  wide: boolean;
  active?: string | null;
}) {
  const sections = sectionsOf(topic);
  if (sections.length === 0) return null;
  const list = (
    <nav className="help-toc" aria-label="Auf dieser Seite">
      {wide && <p className="help-toc-title">Auf dieser Seite</p>}
      <ul>
        {sections.map((s) => (
          <li key={s.id}>
            <a
              href={`#hilfe-${s.id}`}
              aria-current={s.id === active ? "location" : undefined}
              onClick={(e) => {
                e.preventDefault();
                openHelp(topic.id, s.id);
              }}
            >
              {s.text}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
  return wide ? (
    <aside className="help-toc-side">{list}</aside>
  ) : (
    <details className="help-toc-fold">
      <summary>Inhalt</summary>
      {list}
    </details>
  );
}

/** Fuß eines Themas: „Verwandte Themen“ aus den `hilfe:`-Links (nur wenn es welche gibt) und „Zurück zur Übersicht“. */
export function TopicFoot({
  topic,
  topics,
}: {
  topic: HelpTopic;
  topics: HelpTopic[];
}) {
  const related = relatedTopics(topic, topics);
  return (
    <footer className="help-foot">
      <HelpFeedback key={topic.id} topic={topic.id} />
      {related.length > 0 && (
        <nav aria-label="Verwandte Themen">
          <p className="help-foot-title">Verwandte Themen</p>
          <ul>
            {related.map((t) => (
              <li key={t.id}>
                <button className="ghost" onClick={() => openHelp(t.id)}>
                  {t.title}
                </button>
              </li>
            ))}
          </ul>
        </nav>
      )}
      <button className="ghost" onClick={() => openHelp(null)}>
        Zurück zur Übersicht
      </button>
    </footer>
  );
}
