import { useLanguage } from "../i18n/LanguageContext";
import { tr, en, type Translate } from "../i18n/messages";
import type { AnalyzeMailResponse } from "../types";
import { ContentProcessingNotice } from "./ContentProcessingNotice";

interface Props {
  result: AnalyzeMailResponse;
}

function label(value: string, t: Translate): string {
  const labels: Record<string, string> = {
    high: t("high"),
    medium: t("medium"),
    low: t("low"),
    positive: t("positive"),
    neutral: t("neutral"),
    negative: t("negative"),
  };
  return labels[value.toLowerCase()] ?? value;
}

export function AnalysisResult({ result }: Props) {
  const { t, number } = useLanguage();
  const warnings = result.warnings.filter(warning => !result.contentProcessing ||
    ![tr.contentOmitted, en.contentOmitted,
      "Mail content exceeded the local processing limit and was truncated."].includes(warning));
  return (
    <section className="result" aria-live="polite">
      <div className="result-heading">
        <h2>{t("analysisResult")}</h2>
        <span className={`priority priority-${result.priority}`}>{label(result.priority, t)}</span>
      </div>

      <p className="summary">{result.summary}</p>

      {result.actionRequiredFromCurrentUser && (
        <div className="personal-action-alert" role="status">
          <span aria-hidden="true">!</span>
          <strong>{t("personalAction")}</strong>
        </div>
      )}

      <div className="facts">
        <div><span>{t("action")}</span><strong>{result.actionRequired ? t("required") : t("notRequired")}</strong></div>
        <div><span>{t("sentiment")}</span><strong>{label(result.sentiment, t)}</strong></div>
      </div>

      {result.actions.length > 0 && (
        <div className="result-block">
          <h3>{t("actions")}</h3>
          <ol className="actions">
            {result.actions.map((action, index) => (
              <li
                key={`${action.description}-${index}`}
                className={action.assignedToCurrentUser ? "action-mine" : undefined}
              >
                <div className="action-title">
                  <strong>{action.description}</strong>
                  {action.assignedToCurrentUser && (
                    <span className="action-mine-badge">{t("assignedToYou")}</span>
                  )}
                </div>
                <span className="action-meta">
                  {[action.owner, action.dueDate].filter(Boolean).join(" · ") || t("ownerUnknown")}
                </span>
              </li>
            ))}
          </ol>
        </div>
      )}

      {warnings.length > 0 && (
        <div className="warning">
          <strong>{t("warnings")}</strong>
          <ul>{warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>
        </div>
      )}

      <ContentProcessingNotice processing={result.contentProcessing} wasTruncated={result.wasTruncated} />
      <p className="metadata">
        {result.model} · {number(result.durationMilliseconds / 1000, 1)} {t("seconds")}
      </p>
    </section>
  );
}
