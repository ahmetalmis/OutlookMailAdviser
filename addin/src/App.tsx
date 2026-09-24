import { useCallback, useEffect, useState } from "react";
import { analyzeMail, draftMail, getApiStatus, MailAnalysisApiError } from "./api/mailAnalysisApi";
import { AnalysisResult } from "./components/AnalysisResult";
import {
  createAnalysisRequest,
  readCurrentMessage,
  registerItemChanged,
  type OutputLanguage,
} from "./office/outlookItemReader";
import type {
  AnalyzeMailResponse,
  ApiStatus,
  DraftMailResponse,
  DraftTone,
  MailMessage,
} from "./types";

const OUTPUT_LANGUAGE_KEY = "mail-adviser.output-language";

export default function App() {
  const [message, setMessage] = useState<MailMessage | null>(null);
  const [status, setStatus] = useState<ApiStatus>({ state: "unhealthy", detail: "Kontrol ediliyor…" });
  const [result, setResult] = useState<AnalyzeMailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [drafting, setDrafting] = useState(false);
  const [draftTone, setDraftTone] = useState<DraftTone>("professional");
  const [draftToneDetails, setDraftToneDetails] = useState("");
  const [draftInstructions, setDraftInstructions] = useState("");
  const [draftResult, setDraftResult] = useState<DraftMailResponse | null>(null);
  const [draftError, setDraftError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [outputLanguage, setOutputLanguage] = useState<OutputLanguage>(() =>
    localStorage.getItem(OUTPUT_LANGUAGE_KEY) === "en" ? "en" : "tr",
  );

  const loadItem = useCallback(async () => {
    setResult(null);
    setDraftResult(null);
    setDraftError(null);
    setError(null);
    try {
      setMessage(await readCurrentMessage());
    } catch (readError) {
      setMessage(null);
      setError(readError instanceof Error ? readError.message : "E-posta okunamadı.");
    }
  }, []);

  useEffect(() => {
    void loadItem();
    void getApiStatus().then(setStatus);
    registerItemChanged(() => void loadItem());
  }, [loadItem]);

  async function handleAnalyze() {
    setLoading(true);
    setError(null);
    setResult(null);
    setDraftResult(null);
    setDraftError(null);
    try {
      const request = await createAnalysisRequest(outputLanguage);
      setMessage(request.message);
      setResult(await analyzeMail(request));
      setStatus(await getApiStatus());
    } catch (analysisError) {
      if (analysisError instanceof MailAnalysisApiError) {
        const code = analysisError.code ? ` (${analysisError.code})` : "";
        setError(`${analysisError.message}${code}`);
      } else {
        setError(analysisError instanceof Error ? analysisError.message : "Analiz tamamlanamadı.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleDraft() {
    const instructions = draftInstructions.trim();
    if (!instructions) {
      setDraftError("Hazırlanacak yanıtın kısa bir özetini veya talimatını yazın.");
      return;
    }

    setDrafting(true);
    setDraftError(null);
    setDraftResult(null);
    setCopied(false);
    try {
      const context = await createAnalysisRequest(outputLanguage);
      const draft = await draftMail({
        clientRequestId: context.clientRequestId,
        preferredLanguage: context.preferredLanguage,
        message: context.message,
        tone: draftTone,
        toneDetails: draftToneDetails.trim() || undefined,
        instructions,
      });
      setDraftResult(draft);
    } catch (draftingError) {
      if (draftingError instanceof MailAnalysisApiError) {
        const code = draftingError.code ? ` (${draftingError.code})` : "";
        setDraftError(`${draftingError.message}${code}`);
      } else {
        setDraftError(draftingError instanceof Error ? draftingError.message : "Taslak hazırlanamadı.");
      }
    } finally {
      setDrafting(false);
    }
  }

  async function handleCopyDraft() {
    if (!draftResult) return;
    try {
      await navigator.clipboard.writeText(`Konu: ${draftResult.subject}\n\n${draftResult.body}`);
      setCopied(true);
    } catch {
      setDraftError("Taslak panoya kopyalanamadı. Metni seçerek manuel kopyalayabilirsiniz.");
    }
  }

  function handleLanguageChange(language: OutputLanguage) {
    setOutputLanguage(language);
    localStorage.setItem(OUTPUT_LANGUAGE_KEY, language);
    setResult(null);
  }

  return (
    <main>
      <header className="app-header">
        <div className="logo">MA</div>
        <div>
          <h1>Mail Adviser</h1>
          <p>Açık e-postayı yapay zekâ ile analiz edin.</p>
        </div>
      </header>

      <div className={`status ${status.state}`}>
        <span className="status-dot" aria-hidden="true" />
        <span>
          {status.state === "healthy"
            ? `API hazır${status.provider ? ` · ${status.provider}` : ""}${status.model ? ` / ${status.model}` : ""}`
            : `API hazır değil${status.detail ? ` · ${status.detail}` : ""}`}
        </span>
      </div>

      {message && (
        <section className="mail-card">
          <span className="eyebrow">Açık e-posta</span>
          <h2>{message.subject}</h2>
          <p>{message.from.name || message.from.address}</p>
          {message.from.name && <small>{message.from.address}</small>}
        </section>
      )}

      <label className="language-field">
        <span>Analiz sonucu dili</span>
        <select
          value={outputLanguage}
          onChange={(event) => handleLanguageChange(event.target.value as OutputLanguage)}
          disabled={loading}
        >
          <option value="tr">Türkçe</option>
          <option value="en">English</option>
        </select>
      </label>

      <button
        className="analyze-button"
        type="button"
        onClick={handleAnalyze}
        disabled={!message || loading}
      >
        {loading ? "Analiz ediliyor…" : "Bu e-postayı analiz et"}
      </button>

      {loading && <div className="progress" role="progressbar"><span /></div>}
      {error && <div className="error" role="alert">{error}</div>}
      {result && <AnalysisResult result={result} />}

      {result && (
        <section className="draft-composer">
          <div>
            <span className="eyebrow">Yanıt taslağı</span>
            <h2>Mail hazırla</h2>
            <p>Bu e-postaya verilecek yanıtın tonunu ve ana fikrini belirtin.</p>
          </div>

          <label className="draft-field">
            <span>Ton</span>
            <select
              value={draftTone}
              onChange={(event) => setDraftTone(event.target.value as DraftTone)}
              disabled={drafting}
            >
              <option value="professional">Profesyonel</option>
              <option value="friendly">Samimi</option>
              <option value="concise">Kısa ve net</option>
              <option value="persuasive">İkna edici</option>
              <option value="empathetic">Empatik</option>
            </select>
          </label>

          <label className="draft-field">
            <span>Ton detayı <em>(opsiyonel)</em></span>
            <input
              type="text"
              value={draftToneDetails}
              onChange={(event) => setDraftToneDetails(event.target.value)}
              maxLength={500}
              disabled={drafting}
              placeholder="Örn. Hafif sert, net ve uyarıcı olsun."
            />
            <small>{draftToneDetails.length}/500</small>
          </label>

          <label className="draft-field">
            <span>Açıklama / yanıt özeti</span>
            <textarea
              value={draftInstructions}
              onChange={(event) => setDraftInstructions(event.target.value)}
              maxLength={2000}
              rows={5}
              disabled={drafting}
              placeholder="Örn. Kurulumun tamamlandığını belirt, test için yarın 14:00'ü öner ve teşekkür et."
            />
            <small>{draftInstructions.length}/2000</small>
          </label>

          <button
            className="draft-button"
            type="button"
            onClick={handleDraft}
            disabled={drafting || !draftInstructions.trim()}
          >
            {drafting ? "Taslak hazırlanıyor…" : "Yanıt taslağı oluştur"}
          </button>

          {drafting && <div className="progress" role="progressbar"><span /></div>}
          {draftError && <div className="error" role="alert">{draftError}</div>}

          {draftResult && (
            <div className="draft-result" aria-live="polite">
              <div className="draft-result-heading">
                <h3>Hazırlanan taslak</h3>
                <button type="button" onClick={handleCopyDraft}>{copied ? "Kopyalandı" : "Kopyala"}</button>
              </div>
              <span className="draft-subject-label">Konu</span>
              <strong className="draft-subject">{draftResult.subject}</strong>
              <div className="draft-body">{draftResult.body}</div>
              <p className="metadata">
                {draftResult.model} · {(draftResult.durationMilliseconds / 1000).toFixed(1)} sn
                {draftResult.wasTruncated ? " · Kaynak içerik kısaltıldı" : ""}
              </p>
            </div>
          )}
        </section>
      )}

      <footer>E-posta içeriği yalnızca yapılandırdığınız API ve AI sağlayıcısına gönderilir.</footer>
    </main>
  );
}
