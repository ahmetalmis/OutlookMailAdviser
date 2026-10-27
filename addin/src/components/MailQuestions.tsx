import { useLanguage } from "../i18n/LanguageContext";
import { errorText, UiError } from "../i18n/messages";
import { useEffect, useRef, useState } from "react";
import { askMailQuestion } from "../api/mailAnalysisApi";
import type { MailMessage, MailQuestionResponse } from "../types";

interface Props {
  message: MailMessage;
  language: string;
  onUseSources: (quotes: string[]) => void;
  useSourcesDisabled?: boolean;
}

export function MailQuestions({ message, language, onUseSources, useSourcesDisabled = false }: Props) {
  const { t, number } = useLanguage();
  const [question, setQuestion] = useState("");
  const [result, setResult] = useState<MailQuestionResponse | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const pending = useRef<AbortController | null>(null);

  useEffect(() => {
    pending.current?.abort();
    pending.current = null;
    setResult(null);
    setSelected([]);
    setError(null);
    setBusy(false);
    return () => { pending.current?.abort(); pending.current = null; };
  }, [message, language]);

  async function ask() {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    setBusy(true);
    setError(null);
    setResult(null);
    setSelected([]);
    try {
      const response = await askMailQuestion(message, question.trim(), language, controller.signal);
      if (pending.current === controller && !controller.signal.aborted) { setResult(response); }
    } catch (reason) {
      if (pending.current === controller && !controller.signal.aborted) {
        setError(reason ?? new UiError("questionFailed"));
      }
    } finally {
      if (pending.current === controller) { setBusy(false); pending.current = null; }
    }
  }

  function cancel() {
    pending.current?.abort();
    pending.current = null;
    setBusy(false);
    setError(new UiError("cancelled"));
  }

  const quotes = result?.sources.filter(source => selected.includes(source.id)).map(source => source.quote) ?? [];
  const size = quotes.reduce((total, quote) => total + quote.length, 0);
  return <section className="mail-questions">
    <h2>{t("askTitle")}</h2>
    <p>{t("askHelp")}</p>
    <label className="draft-field">
      <span>{t("question")}</span>
      <textarea value={question} onChange={event => setQuestion(event.target.value)}
        maxLength={1000} rows={3} disabled={busy}
        placeholder={t("questionExample")} />
    </label>
    <button type="button" onClick={() => void ask()} disabled={busy || !question.trim()}>
      {busy ? t("examining") : t("ask")}
    </button>
    {busy && <>
      <div className="progress" role="progressbar" aria-label={t("examiningLabel")}><span /></div>
      <p role="status">{t("examiningHelp")}</p>
      <button type="button" onClick={cancel}>{t("cancel")}</button>
    </>}
    {error != null && <p className="error" role="alert">{errorText(error, t)}</p>}
    {result && <div aria-live="polite">
      {result.errorCode && <p className="error" role="alert">{errorText({ code: result.errorCode }, t)}</p>}
      <p className="question-answer">{result.answer}</p>
      <small>{t("coverage", { completed: number(result.completedChunks), total: number(result.totalChunks) })}{result.isPartial ? ` · ${t("partial")}` : ""}</small>
      {result.warnings.map(warning => <p className="question-warning" key={warning}>{warning}</p>)}
      {result.sources.length > 0 && <>
        <h3>{t("sources")}</h3>
        <p>{t("selectSources")}</p>
        {result.sources.map((source, index) => <label className="question-source" key={source.id}>
          <input type="checkbox" checked={selected.includes(source.id)}
            onChange={event => setSelected(current => event.target.checked
              ? [...current, source.id] : current.filter(id => id !== source.id))} />
          <span><strong>{t("quote")} {number(index + 1)}</strong><blockquote>{source.quote}</blockquote></span>
        </label>)}
        <small>{t("sourceSize", { count: number(size) })}</small>
        {(size > 4000 || quotes.length > 20) && <p role="alert">{t("sourceLimit")}</p>}
        <button type="button" disabled={useSourcesDisabled || quotes.length === 0 || size > 4000 || quotes.length > 20}
          onClick={() => onUseSources(quotes)}>{t("useSources")}</button>
      </>}
    </div>}
  </section>;
}
