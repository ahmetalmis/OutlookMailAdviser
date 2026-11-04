import type { AnalyzeMailResponse, MailMessage } from "../types";
import { ContentProcessingNotice } from "./ContentProcessingNotice";

interface Props {
  result: AnalyzeMailResponse;
  message?: MailMessage | null;
}

function OriginalMail({ message }: { message: MailMessage }) {
  const recipients = [...(message.to ?? []), ...(message.cc ?? [])]
    .map((person) => person.name || person.address)
    .join(", ");
  return (
    <div className="original-mail">
      <h3>Ana mail</h3>
      <p>{message.from?.name || message.from?.address}</p>
      {recipients && <small>Kime: {recipients}</small>}
      {message.bodyFormat === "html" ? (
        <div className="original-mail-body" dangerouslySetInnerHTML={{ __html: mailPreviewHtml(message.body) }} />
      ) : (
        <pre>{message.body}</pre>
      )}
    </div>
  );
}

function mailPreviewHtml(body: string): string {
  const inner = body.match(/<body[^>]*>([\s\S]*)<\/body>/i)?.[1] ?? body;
  return inner
    .replace(/<script[\s\S]*?<\/script>/gi, "")
    .replace(/<style[\s\S]*?<\/style>/gi, "")
    .replace(/<\/?(?:iframe|object|embed|base|link|meta)\b[^>]*>/gi, "")
    .replace(/\son\w+\s*=\s*(['"]).*?\1/gi, "");
}

function label(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }
  const labels: Record<string, string> = {
    high: "Yüksek",
    medium: "Orta",
    low: "Düşük",
    positive: "Olumlu",
    neutral: "Nötr",
    negative: "Olumsuz",
  };
  return labels[value.toLowerCase()] ?? value;
}

export function AnalysisResult({ result, message }: Props) {
  const warnings = (result.warnings ?? []).filter(warning => !result.contentProcessing ||
    !["İçeriğin bir bölümü analize dahil edilemedi.",
      "Mail content exceeded the local processing limit and was truncated."].includes(warning));
  return (
    <section className="result" aria-live="polite">
      <div className="result-heading">
        <h2>Analiz sonucu</h2>
        <span className={`priority priority-${result.priority}`}>{label(result.priority)}</span>
      </div>

      <p className="summary">{result.summary}</p>
      {message?.body && <OriginalMail message={message} />}

      {result.actionRequiredFromCurrentUser && (
        <div className="personal-action-alert" role="status">
          <span aria-hidden="true">!</span>
          <strong>Senden aksiyon bekleniyor</strong>
        </div>
      )}

      <div className="facts">
        <div><span>Aksiyon</span><strong>{result.actionRequired ? "Gerekli" : "Gerekli değil"}</strong></div>
        <div><span>Duygu</span><strong>{label(result.sentiment)}</strong></div>
      </div>

      {(result.actions ?? []).length > 0 && (
        <div className="result-block">
          <h3>Aksiyonlar</h3>
          <ol className="actions">
            {(result.actions ?? []).map((action, index) => (
              <li
                key={`${action.description}-${index}`}
                className={action.assignedToCurrentUser ? "action-mine" : undefined}
              >
                <div className="action-title">
                  <strong>{action.description}</strong>
                  {action.assignedToCurrentUser && (
                    <span className="action-mine-badge">Senden bekleniyor</span>
                  )}
                </div>
                <span className="action-meta">
                  {[action.owner, action.dueDate].filter(Boolean).join(" · ") || "Sahip/tarih belirtilmemiş"}
                </span>
              </li>
            ))}
          </ol>
        </div>
      )}

      {warnings.length > 0 && (
        <div className="warning">
          <strong>Uyarılar</strong>
          <ul>{warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>
        </div>
      )}

      <ContentProcessingNotice processing={result.contentProcessing} wasTruncated={result.wasTruncated} />
      <p className="metadata">
        {result.model} · {(result.durationMilliseconds / 1000).toFixed(1)} sn
      </p>
    </section>
  );
}
