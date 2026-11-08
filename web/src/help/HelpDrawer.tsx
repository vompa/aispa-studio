import { useEffect, useRef, useState } from "react";
import { closeHelp, openHelp, useHelpRequest } from "./help";
import { calloutLabels, helpLink, type Block, type Span } from "./markdown";
import { HelpIndex } from "./HelpIndex";
import { OpenQuestions } from "./OpenQuestions";
import { TopicFoot, TopicHead } from "./HelpTopicParts";
import type { HelpContext } from "./search";
import {
  imageSize,
  imageUrl,
  loadTopics,
  sectionBlocks,
  type HelpTopic,
} from "./topics";

function Inline({ spans }: { spans: Span[] }) {
  return (
    <>
      {spans.map((s, i) => {
        const target = helpLink(s.href);
        return target ? (
          <a
            key={i}
            href={`#hilfe-${target.topic}`}
            onClick={(e) => {
              e.preventDefault();
              openHelp(target.topic, target.section);
            }}
          >
            {s.text}
          </a>
        ) : s.href ? (
          <a key={i} href={s.href} target="_blank" rel="noopener noreferrer">
            {s.text}
          </a>
        ) : s.code ? (
          <code key={i}>{s.text}</code>
        ) : s.bold ? (
          <strong key={i}>{s.text}</strong>
        ) : (
          <span key={i}>{s.text}</span>
        );
      })}
    </>
  );
}

const calloutClass = { hint: "", tip: " ok", warning: " warning" } as const;

/** Zeichnet Blöcke der Hilfe: Überschriften mit Anker, Absätze, Listen, Hinweiskästen und Bilder. */
export function HelpBlocks({
  blocks,
  target,
  top = 3,
  linkImages = true,
}: {
  blocks: Block[];
  target?: string;
  /** Ebene der Zwischenüberschriften: 3 in der Schublade, 2 auf der Hilfe-Seite (dort steht der Titel als h1). */
  top?: 2 | 3;
  /** Bilder als Verweis auf das Bild in voller Größe (in der Schublade); die Hilfe-Seite zeigt sie ohne Verweis. */
  linkImages?: boolean;
}) {
  return (
    <>
      {blocks.map((b, i) => {
        if (b.kind === "heading") {
          const Tag = `h${b.level <= 2 ? top : top + 1}` as "h2" | "h3" | "h4";
          return (
            <Tag
              key={i}
              id={`hilfe-${b.id}`}
              className={target === b.id ? "help-target" : undefined}
            >
              {b.text}
            </Tag>
          );
        }
        if (b.kind === "paragraph")
          return (
            <p key={i}>
              <Inline spans={b.spans} />
            </p>
          );
        if (b.kind === "callout")
          return (
            <div
              key={i}
              role="note"
              className={`notice help-callout${calloutClass[b.type]}`}
            >
              <strong>{calloutLabels[b.type]}:</strong> <Inline spans={b.spans} />
            </div>
          );
        if (b.kind === "image") {
          const url = imageUrl(b.name);
          const size = imageSize(b.name);
          return url ? (
            <figure key={i} className="help-image">
              {linkImages ? (
                <a href={url} target="_blank" rel="noopener noreferrer">
                  <img src={url} alt={b.alt} loading="lazy" {...size} />
                </a>
              ) : (
                <img src={url} alt={b.alt} loading="lazy" {...size} />
              )}
              <figcaption>{b.alt}</figcaption>
            </figure>
          ) : (
            <p key={i} className="help-image-missing muted">
              Bild folgt: {b.alt}
            </p>
          );
        }
        const List = b.ordered ? "ol" : "ul";
        return (
          <List key={i}>
            {b.items.map((item, j) => (
              <li key={j}>
                <Inline spans={item} />
              </li>
            ))}
          </List>
        );
      })}
    </>
  );
}

/**
 * Ein Thema oder ein Abschnitt der Hilfe mit Schritten und Bildern, zum Einbetten (z. B. in der KI-Seitenleiste als Antwort auf
 * „Wie mache ich …?“ oder als Erklärung zur aktuellen Seite), darunter „In der Hilfe öffnen“.
 */
export function HelpSection({
  topic,
  section,
}: {
  topic: string;
  section?: string | null;
}) {
  const [topics, setTopics] = useState<HelpTopic[] | null>(null);
  useEffect(() => void loadTopics().then(setTopics), []);
  const found = topics?.find((t) => t.id === topic);
  if (!found) return null;
  const blocks =
    section && sectionBlocks(found, section).length
      ? sectionBlocks(found, section)
      : found.blocks;
  return (
    <div className="help-body help-section">
      <h3>{found.title}</h3>
      <HelpBlocks blocks={blocks} />
      <button
        className="ghost"
        onClick={() => openHelp(found.id, section ?? undefined)}
      >
        In der Hilfe öffnen
      </button>
    </div>
  );
}

/**
 * Hilfe als Seitenleiste: Suche und Übersicht aller Themen oder ein Thema, auf Wunsch gleich beim gesuchten Abschnitt.
 * `canReviewQuestions`: Inhaber, Manager und Standortleitung sehen in der Übersicht die offenen Fragen an die Hilfe (Slice 56).
 */
export function HelpDrawer({
  context,
  canReviewQuestions = false,
}: {
  context?: HelpContext;
  canReviewQuestions?: boolean;
}) {
  const request = useHelpRequest();
  const [topics, setTopics] = useState<HelpTopic[] | null>(null);
  const body = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (request && !topics) void loadTopics().then(setTopics);
  }, [request, topics]);

  useEffect(() => {
    if (!request || !topics) return;
    const target = request.section
      ? document.getElementById(`hilfe-${request.section}`)
      : null;
    if (target) target.scrollIntoView?.({ block: "start" });
    else body.current?.scrollTo?.({ top: 0 });
  }, [request, topics]);

  // Fokus beim Öffnen auf die Überschrift, beim Schließen zurück zum Auslöser (meist ein Fragezeichen). Öffnet ein
  // Auslöser außerhalb der Leiste sie neu, wird er der neue; Verweise in der Leiste behalten den alten.
  const drawer = useRef<HTMLElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const opener = useRef<HTMLElement | null>(null);
  useEffect(() => {
    if (!request) {
      const back = opener.current;
      opener.current = null;
      if (back?.isConnected) back.focus();
      return;
    }
    const active = document.activeElement;
    if (active instanceof HTMLElement && !drawer.current?.contains(active)) {
      opener.current = active;
      heading.current?.focus();
    }
  }, [request]);

  if (!request) return null;
  const topic = request.topic
    ? topics?.find((t) => t.id === request.topic)
    : undefined;

  return (
    <aside
      className="help-drawer"
      aria-label="Hilfe"
      ref={drawer}
      onKeyDown={(e) => {
        // Nur Esc, das niemand in der Leiste schon verbraucht hat; ein Dialog darüber behält sein Esc.
        if (e.key === "Escape" && !e.defaultPrevented) closeHelp();
      }}
    >
      <header>
        <h2 ref={heading} tabIndex={-1}>
          {topic?.title ?? "Hilfe"}
        </h2>
        <span className="spacer" />
        {topic && (
          <button className="ghost" onClick={() => openHelp(null)}>
            Alle Themen
          </button>
        )}
        <button
          className="ghost"
          onClick={closeHelp}
          aria-label="Hilfe schließen"
        >
          Schließen
        </button>
      </header>
      <div className="help-body" ref={body}>
        {!topics && <p className="muted">Wird geladen …</p>}
        {topics && !topic && (
          <>
            {request.topic && (
              <p className="notice">
                Zu „{request.topic}“ gibt es noch keinen Hilfetext.
              </p>
            )}
            <HelpIndex
              topics={topics}
              context={{ surface: "verwaltung", ...context }}
            />
            {canReviewQuestions && <OpenQuestions topics={topics} />}
          </>
        )}
        {topic && topics && (
          <>
            <TopicHead topic={topic} />
            <HelpBlocks blocks={topic.blocks} target={request.section} />
            <TopicFoot topic={topic} topics={topics} />
          </>
        )}
      </div>
    </aside>
  );
}
