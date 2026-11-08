import { useEffect, useMemo, useRef, useState } from "react";
import { installAppShortcuts } from "../appShortcuts";
import { HelpBlocks } from "./HelpDrawer";
import { HelpIndex } from "./HelpIndex";
import { TopicFoot, TopicHead, TopicToc } from "./HelpTopicParts";
import { openHelp, useHelpRequest } from "./help";
import { buildHelpUrl, readHelpUrl } from "./helpUrl";
import { orderedRoleEntries } from "./roleEntries";
import { loadTopics, sectionsOf, type HelpTopic } from "./topics";

/** Ab dieser Fensterbreite steht „Auf dieser Seite“ rechts neben dem Text, darunter als „Inhalt“ über dem Text. */
const wideQuery = "(min-width: 1180px)";

function useWide(): boolean {
  const [wide, setWide] = useState(
    () => window.matchMedia?.(wideQuery).matches ?? true,
  );
  useEffect(() => {
    const query = window.matchMedia?.(wideQuery);
    if (!query) return;
    const update = () => setWide(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  return wide;
}

/** Der Abschnitt, den man gerade liest: der letzte Abschnittstitel, der oben im Sichtfeld angekommen ist. */
function useActiveSection(
  topic: HelpTopic | undefined,
  section: string | undefined,
) {
  const key = `${topic?.id ?? ""}#${section ?? ""}`;
  const [seen, setSeen] = useState<{ key: string; id: string } | null>(null);
  const active = seen?.key === key ? seen.id : (section ?? null);
  useEffect(() => {
    if (!topic || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => {
        const hit = entries.filter((e) => e.isIntersecting).at(-1);
        if (hit) setSeen({ key, id: hit.target.id.replace(/^hilfe-/, "") });
      },
      { rootMargin: "0px 0px -70% 0px" },
    );
    for (const s of sectionsOf(topic)) {
      const el = document.getElementById(`hilfe-${s.id}`);
      if (el) observer.observe(el);
    }
    return () => observer.disconnect();
  }, [topic, key]);
  return active;
}

/**
 * Die Hilfe als eigene Seite (`/help/`): links Suche und Themen, rechts das Thema. Dieselbe Seite öffnet die Windows-App im
 * eingebetteten Browser (ADR 0079), aus dem Fragezeichen mit `?thema=…&abschnitt=…`. Der Stand steht in der Adresse, Zurück und
 * Vor gehen durch die besuchten Themen.
 */
export function HelpApp() {
  const request = useHelpRequest();
  const params = useMemo(() => readHelpUrl(window.location.search), []);
  const [topics, setTopics] = useState<HelpTopic[] | null>(null);
  const content = useRef<HTMLElement>(null);
  const synced = useRef(false);
  const wide = useWide();

  useEffect(() => void loadTopics().then(setTopics), []);
  // Eingebettet in der Windows-App meldet Strg+Leertaste „KI und Hilfe“ an die App.
  useEffect(() => installAppShortcuts(), []);

  // Adresse → Anzeige: beim Start und bei Zurück/Vor.
  useEffect(() => {
    openHelp(params.topic, params.section);
    const onPop = () => {
      const next = readHelpUrl(window.location.search);
      openHelp(next.topic, next.section);
    };
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
  }, [params]);

  // Anzeige → Adresse: das erste Ziel ersetzt den Eintrag (ohne `n`), jedes weitere kommt neu in den Verlauf.
  useEffect(() => {
    if (!request) return;
    const next = buildHelpUrl(request, params);
    if (next === window.location.search) {
      synced.current = true;
      return;
    }
    if (synced.current) window.history.pushState(null, "", next || "?");
    else window.history.replaceState(null, "", next || window.location.pathname);
    synced.current = true;
  }, [request, params]);

  const topic = request?.topic
    ? topics?.find((t) => t.id === request.topic)
    : undefined;

  const active = useActiveSection(topic, request?.section);

  useEffect(() => {
    document.title = `${topic ? `${topic.title} · ` : ""}Hilfe · AISPA Studio`;
  }, [topic]);

  // Zum Abschnitt rollen, sobald er gezeichnet ist; ohne Abschnitt an den Anfang des Themas.
  useEffect(() => {
    if (!topics) return;
    const target = request?.section
      ? document.getElementById(`hilfe-${request.section}`)
      : null;
    if (target) target.scrollIntoView?.({ block: "start" });
    else content.current?.scrollTo?.({ top: 0 });
  }, [request, topics]);

  const context = useMemo(
    () => ({ surface: params.surface ?? "verwaltung", role: params.role }),
    [params],
  );

  return (
    <div className="help-page">
      <nav className="help-page-nav" aria-label="Themen">
        <p className="help-page-title">Hilfe</p>
        {topics ? (
          <HelpIndex
            topics={topics}
            context={context}
            current={topic?.id ?? null}
            page
          />
        ) : (
          <p className="muted">Wird geladen …</p>
        )}
      </nav>
      <main className="help-body help-page-content" ref={content}>
        {!topics && <p className="muted">Wird geladen …</p>}
        {topics && topic && (
          <div className="help-page-grid">
            <article className="help-article">
              <TopicHead topic={topic} toc={false}>
                <h1>{topic.title}</h1>
              </TopicHead>
              {!wide && <TopicToc topic={topic} wide={false} active={active} />}
              <HelpBlocks
                blocks={topic.blocks}
                target={request?.section}
                top={2}
                linkImages={false}
              />
              <TopicFoot topic={topic} topics={topics} />
            </article>
            {wide && <TopicToc topic={topic} wide active={active} />}
          </div>
        )}
        {topics && !topic && request?.topic && (
          <>
            <h1>Thema nicht gefunden</h1>
            <p className="notice">
              Zu „{request.topic}“ gibt es noch keinen Hilfetext.
            </p>
            <p>
              <button
                type="button"
                className="ghost"
                onClick={() => openHelp(null)}
              >
                Übersicht öffnen
              </button>
            </p>
          </>
        )}
        {topics && !topic && !request?.topic && (
          <>
            <h1>Hilfe</h1>
            <p className="muted">
              Ein Thema in der Liste wählen oder oben suchen, z. B. „Termin
              verschieben“.
            </p>
            <h2>Erste Schritte</h2>
            {orderedRoleEntries(params.role).map((entry) => (
              <section key={entry.role} aria-label={entry.label}>
                <h3>{entry.label}</h3>
                <ul className="help-topics">
                  {entry.topics.flatMap((id) => {
                    const t = topics.find((x) => x.id === id);
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
              </section>
            ))}
          </>
        )}
      </main>
    </div>
  );
}
