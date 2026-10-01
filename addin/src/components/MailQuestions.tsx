import { useEffect, useRef, useState } from "react";
import { askMailQuestion } from "../api/mailAnalysisApi";
import { providerErrorMessage } from "../api/providerErrors";
import type { MailMessage, MailQuestionResponse } from "../types";

interface Props {
  message: MailMessage;
  language: string;
  onUseSources: (quotes: string[]) => void;
  useSourcesDisabled?: boolean;
}

export function MailQuestions({ message, language, onUseSources, useSourcesDisabled = false }: Props) {
  const [question, setQuestion] = useState("");
  const [result, setResult] = useState<MailQuestionResponse | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
        setError(reason instanceof Error ? reason.message : "Soru yanıtlanamadı.");
      }
    } finally {
      if (pending.current === controller) { setBusy(false); pending.current = null; }
    }
  }

  function cancel() {
    pending.current?.abort();
    pending.current = null;
    setBusy(false);
    setError("İnceleme iptal edildi.");
  }

  const quotes = result?.sources.filter(source => selected.includes(source.id)).map(source => source.quote) ?? [];
  const size = quotes.reduce((total, quote) => total + quote.length, 0);
  return <section className="mail-questions">
    <h2>Bu yazışmaya sor</h2>
    <p>Yalnızca açık e-postanın gövdesi ve içindeki geçmiş yazışmalar incelenir.</p>
    <label className="draft-field">
      <span>Sorun</span>
      <textarea value={question} onChange={event => setQuestion(event.target.value)}
        maxLength={1000} rows={3} disabled={busy}
        placeholder="Örn. Teslim tarihiyle ilgili daha önce ne söylenmiş?" />
    </label>
    <button type="button" onClick={() => void ask()} disabled={busy || !question.trim()}>
      {busy ? "Yazışma inceleniyor…" : "Sor"}
    </button>
    {busy && <>
      <div className="progress" role="progressbar" aria-label="Yazışma inceleniyor"><span /></div>
      <p role="status">Geçmişin tamamı bölümler halinde inceleniyor. Uzun yazışmalar zaman alabilir.</p>
      <button type="button" onClick={cancel}>İptal et</button>
    </>}
    {error && <p className="error" role="alert">{error}</p>}
    {result && <div aria-live="polite">
      {result.errorCode && <p className="error" role="alert">{providerErrorMessage(result.errorCode, "AI sağlayıcısı isteği tamamlayamadı.")}</p>}
      <p className="question-answer">{result.answer}</p>
      <small>{result.completedChunks}/{result.totalChunks} bölüm incelendi{result.isPartial ? " · Kısmi sonuç" : ""}</small>
      {result.warnings.map(warning => <p className="question-warning" key={warning}>{warning}</p>)}
      {result.sources.length > 0 && <>
        <h3>Kaynak alıntıları</h3>
        <p>Taslakta kullanmak istediğin alıntıları seç.</p>
        {result.sources.map((source, index) => <label className="question-source" key={source.id}>
          <input type="checkbox" checked={selected.includes(source.id)}
            onChange={event => setSelected(current => event.target.checked
              ? [...current, source.id] : current.filter(id => id !== source.id))} />
          <span><strong>Alıntı {index + 1}</strong><blockquote>{source.quote}</blockquote></span>
        </label>)}
        <small>Seçilen kaynaklar: {size}/4000 karakter</small>
        {(size > 4000 || quotes.length > 20) && <p role="alert">En fazla 20 alıntı ve 4000 karakter seçebilirsin.</p>}
        <button type="button" disabled={useSourcesDisabled || quotes.length === 0 || size > 4000 || quotes.length > 20}
          onClick={() => onUseSources(quotes)}>Taslakta kullan</button>
      </>}
    </div>}
  </section>;
}
