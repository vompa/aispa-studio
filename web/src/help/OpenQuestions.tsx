import { useState } from "react";
import {
  listUnanswered,
  removeUnanswered,
  type UnansweredQuestion,
} from "../assistant/unanswered";
import { HelpButton } from "./HelpButton";
import type { HelpTopic } from "./topics";

const date = new Intl.DateTimeFormat("de-AT", { dateStyle: "medium" });

/** Seite in Worten: der Titel ihres Seitenthemas („seite-kassa“), sonst die Kennung. */
function pageName(page: string | null, topics: HelpTopic[]) {
  if (!page) return null;
  return topics.find((t) => t.id === `seite-${page}`)?.title ?? page;
}

/**
 * Offene Fragen an die Hilfe (Slice 56): was der Assistent in diesem Browser nicht beantworten konnte, für Inhaber, Manager und
 * Standortleitung. „Erledigt“ löscht den Eintrag; nach 90 Tagen fällt er von selbst weg.
 */
export function OpenQuestions({ topics }: { topics: HelpTopic[] }) {
  const [questions, setQuestions] = useState(() => listUnanswered());

  function done(entry: UnansweredQuestion) {
    removeUnanswered(entry);
    setQuestions(listUnanswered());
  }

  return (
    <section className="open-questions" aria-labelledby="open-questions-title">
      <div className="open-questions-head">
        <h3 id="open-questions-title">Offene Fragen an die Hilfe</h3>
        <HelpButton
          topic="ki-assistent-fragen"
          section="offene-fragen"
          label="Offene Fragen an die Hilfe"
        />
      </div>
      <p className="muted">
        Fragen, auf die der Assistent in diesem Browser nichts gefunden hat, ohne
        Gästenamen und Kontaktdaten. Sie bleiben 90 Tage.
      </p>
      {questions.length === 0 ? (
        <p className="muted">Keine offenen Fragen.</p>
      ) : (
        <ul aria-label="Offene Fragen an die Hilfe">
          {questions.map((q) => {
            const page = pageName(q.page, topics);
            return (
              <li key={`${q.page}|${q.question}`}>
                <span className="open-questions-text">
                  {q.question}
                  <span className="open-questions-meta muted">
                    {[
                      page,
                      date.format(new Date(q.at)),
                      q.count > 1 ? `${q.count}-mal gefragt` : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </span>
                <button
                  type="button"
                  className="small"
                  aria-label={`Erledigt: ${q.question}`}
                  onClick={() => done(q)}
                >
                  Erledigt
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
