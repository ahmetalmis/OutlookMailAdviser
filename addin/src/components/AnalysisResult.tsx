import type { AnalyzeMailResponse } from "../types";
import { ContentProcessingNotice } from "./ContentProcessingNotice";

interface Props {
  result: AnalyzeMailResponse;
}

function label(value: string): string {
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

export function AnalysisResult({ result }: Props) {
  const warnings = result.warnings.filter(warning => !result.contentProcessing ||
    !["İçeriğin bir bölümü analize dahil edilemedi.",
      "Mail content exceeded the local processing limit and was truncated."].includes(warning));
  return (
    <section className="result" aria-live="polite">
      <div className="result-heading">
        <h2>Analiz sonucu</h2>
        <span className={`priority priority-${result.priority}`}>{label(result.priority)}</span>
      </div>

      <p className="summary">{result.summary}</p>

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

      {result.actions.length > 0 && (
        <div className="result-block">
          <h3>Aksiyonlar</h3>
          <ol className="actions">
            {result.actions.map((action, index) => (
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
