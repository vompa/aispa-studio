import { HelpBlocks } from "./HelpDrawer";
import { sectionBlocks, type HelpTopic } from "./topics";

/**
 * Die Hilfe in der Leiste „KI und Hilfe“ der Windows-App (`ansicht=leiste`, ADR 0079, Nachtrag 17.11.2026): nur der Abschnitt oder das Thema,
 * ohne Kopf, Navigation und Verzeichnis, schmal für 360 px. Titel, Schließen und „Im Handbuch öffnen“ zeichnet die App über und unter dem Browser.
 */
export function HelpPanelView({
  topics,
  topic,
  section,
}: {
  topics: HelpTopic[] | null;
  topic: HelpTopic | undefined;
  section: string | undefined;
}) {
  if (!topics)
    return (
      <main className="help-body help-panel-view">
        <p className="muted">Wird geladen …</p>
      </main>
    );
  if (!topic)
    return (
      <main className="help-body help-panel-view">
        <p className="notice">Zu dieser Stelle gibt es noch keinen Hilfetext.</p>
      </main>
    );
  const part = section ? sectionBlocks(topic, section) : [];
  // Die Überschrift des Abschnitts steht schon im Kopf der Leiste; ohne Abschnitt (oder wenn es ihn nicht gibt) steht das ganze Thema da.
  const blocks = part.length > 0 ? part.slice(1) : topic.blocks;
  return (
    <main className="help-body help-panel-view" aria-label={topic.title}>
      <HelpBlocks blocks={blocks} top={3} linkImages={false} />
    </main>
  );
}
