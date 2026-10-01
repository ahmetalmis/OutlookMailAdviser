import { useCallback, useEffect, useRef, useState } from "react";
import { analyzeMail, draftMail, getApiStatus, MailAnalysisApiError } from "./api/mailAnalysisApi";
import { MailQuestions } from "./components/MailQuestions";
import { AnalysisResult } from "./components/AnalysisResult";
import { ContentProcessingNotice } from "./components/ContentProcessingNotice";
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
  DraftMode,
  MailMessage,
} from "./types";

const OUTPUT_LANGUAGE_KEY = "mail-adviser.output-language";

export default function App() {
  const itemVersion = useRef(0);
  const statusRequest = useRef<AbortController | null>(null);
  const [draftOpened, setDraftOpened] = useState(false);
  const [sourceQuotes, setSourceQuotes] = useState<string[]>([]);
  const [sourceBody, setSourceBody] = useState<string | null>(null);
  const [message, setMessage] = useState<MailMessage | null>(null);
  const [status, setStatus] = useState<ApiStatus>({ state: "unhealthy", detail: "Kontrol ediliyor…" });
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [result, setResult] = useState<AnalyzeMailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [drafting, setDrafting] = useState(false);
  const [draftTone, setDraftTone] = useState<DraftTone>("professional");
  const [draftToneDetails, setDraftToneDetails] = useState("");
  const [draftMode, setDraftMode] = useState<DraftMode>("reply");
  const [targetAudience, setTargetAudience] = useState("");
  const [considerations, setConsiderations] = useState("");
  const [draftInstructions, setDraftInstructions] = useState("");
  const [draftResult, setDraftResult] = useState<DraftMailResponse | null>(null);
  const [draftError, setDraftError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [outputLanguage, setOutputLanguage] = useState<OutputLanguage>(() =>
    localStorage.getItem(OUTPUT_LANGUAGE_KEY) === "en" ? "en" : "tr",
  );

  const loadItem = useCallback(async () => {
    const version = ++itemVersion.current;
    setMessage(null);
    setDraftMode("reply");
    setTargetAudience("");
    setConsiderations("");
    setDraftInstructions("");
    setCopied(false);
    setSourceQuotes([]);
    setSourceBody(null);
    setDraftOpened(false);
    setDrafting(false);
    setLoading(false);
    setResult(null);
    setDraftResult(null);
    setDraftError(null);
    setError(null);
    try {
      const current = await readCurrentMessage();
      if (version === itemVersion.current) { setMessage(current); }
    } catch (readError) {
      if (version !== itemVersion.current) return;
      setMessage(null);
      setError(readError instanceof Error ? readError.message : "E-posta okunamadı.");
    }
  }, []);

  const refreshStatus = useCallback(async () => {
    statusRequest.current?.abort();
    const controller = new AbortController();
    statusRequest.current = controller;
    setCheckingStatus(true);
    try {
      const current = await getApiStatus(controller.signal);
      if (!controller.signal.aborted) setStatus(current);
    } finally {
      if (!controller.signal.aborted) setCheckingStatus(false);
    }
  }, []);

  useEffect(() => {
    void loadItem();
    void refreshStatus();
    registerItemChanged(() => void loadItem());
    return () => statusRequest.current?.abort();
  }, [loadItem, refreshStatus]);

  async function handleAnalyze() {
    const version = itemVersion.current;
    setLoading(true);
    setError(null);
    setResult(null);
    setDraftResult(null);
    setDraftError(null);
    try {
      const request = await createAnalysisRequest(outputLanguage);
      if (version !== itemVersion.current) return;
      setMessage(request.message);
      setSourceQuotes([]);
      setSourceBody(null);
      const analysis = await analyzeMail(request);
      if (version !== itemVersion.current) return;
      setResult(analysis);
      void refreshStatus();
    } catch (analysisError) {
      if (version !== itemVersion.current) return;
      if (analysisError instanceof MailAnalysisApiError) {
        const code = analysisError.code ? ` (${analysisError.code})` : "";
        setError(`${analysisError.message}${code}`);
      } else {
        setError(analysisError instanceof Error ? analysisError.message : "Analiz tamamlanamadı.");
      }
    } finally {
      if (version === itemVersion.current) setLoading(false);
    }
  }

  async function handleDraft() {
    const version = itemVersion.current;
    const instructions = draftInstructions.trim();
    if (!instructions) {
      setDraftError("Hazırlanacak mailin amacını veya ana mesajını yazın.");
      return;
    }

    if (!["reply", "forward"].includes(draftMode)) {
      setDraftError("Geçerli bir yazışma türü seçin.");
      return;
    }
    if (draftMode === "forward" && !targetAudience.trim()) {
      setDraftError("Mailin kime yazılacağını belirtin.");
      return;
    }
    if (targetAudience.trim().length > 500 || considerations.trim().length > 2000) {
      setDraftError("Muhatap 500, dikkat edilecek hususlar 2000 karakteri aşamaz.");
      return;
    }
    setDrafting(true);
    setDraftError(null);
    setDraftResult(null);
    setCopied(false);
    try {
      const context = await createAnalysisRequest(outputLanguage);
      if (version !== itemVersion.current) return;
      if (sourceQuotes.length > 0 && context.message.body !== sourceBody) {
        setSourceQuotes([]);
        setSourceBody(null);
        throw new Error("E-posta içeriği değişti. Kaynakları yeniden seçin.");
      }
      const draft = await draftMail({
        clientRequestId: context.clientRequestId,
        preferredLanguage: context.preferredLanguage,
        message: context.message,
        tone: draftTone,
        draftMode,
        targetAudience: draftMode === "forward" ? targetAudience.trim() : undefined,
        considerations: considerations.trim() || undefined,
        toneDetails: draftToneDetails.trim() || undefined,
        instructions,
        sourceQuotes,
      });
      if (version === itemVersion.current) setDraftResult(draft);
    } catch (draftingError) {
      if (version !== itemVersion.current) return;
      if (draftingError instanceof MailAnalysisApiError) {
        const code = draftingError.code ? ` (${draftingError.code})` : "";
        setDraftError(`${draftingError.message}${code}`);
      } else {
        setDraftError(draftingError instanceof Error ? draftingError.message : "Taslak hazırlanamadı.");
      }
    } finally {
      if (version === itemVersion.current) setDrafting(false);
    }
  }

  async function handleCopyDraft() {
    if (!draftResult) return;
    try {
      await navigator.clipboard.writeText(draftResult.body);
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

      <div className={`status ${status.state}`} role="status" aria-live="polite">
        <span className="status-dot" aria-hidden="true" />
        <span className="status-message">
          {status.state === "healthy"
            ? `API hazır${status.provider ? ` · ${status.provider}` : ""}${status.model ? ` / ${status.model}` : ""}`
            : `API hazır değil${status.detail ? ` · ${status.detail}` : ""}`}
        </span>
        <button className="status-retry" type="button" onClick={() => void refreshStatus()} disabled={checkingStatus}>
          {checkingStatus ? "Kontrol ediliyor…" : "Yeniden kontrol et"}
        </button>
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
          disabled={loading || drafting}
        >
          <option value="tr">Türkçe</option>
          <option value="en">English</option>
        </select>
      </label>

      <button
        className="analyze-button"
        type="button"
        onClick={handleAnalyze}
        disabled={!message || loading || drafting}
      >
        {loading ? "Analiz ediliyor…" : "Bu e-postayı analiz et"}
      </button>

      {loading && <div className="progress" role="progressbar"><span /></div>}
      {error && <div className="error" role="alert">{error}</div>}
      {result && <AnalysisResult result={result} />}

      {message && <MailQuestions message={message} language={outputLanguage} useSourcesDisabled={drafting || loading} onUseSources={quotes => {
        setDraftOpened(true);
        setSourceQuotes(quotes);
        setSourceBody(message.body);
        setDraftResult(null);
        if (!draftInstructions.trim()) setDraftInstructions("Seçtiğim kaynak bilgilerini dikkate alarak mail taslağı hazırla.");
      }} />}

      {(result || draftOpened) && (
        <section className="draft-composer">
          <div>
            <span className="eyebrow">Mail taslağı</span>
            <h2>Mail hazırla</h2>
            <p>Mailin muhatabını, tonunu ve ana fikrini belirtin.</p>
          </div>

          {sourceQuotes.length > 0 && <div className="selected-sources" role="status">
            <p>{sourceQuotes.length} kaynak alıntısı taslakta kullanılacak.</p>
            <details><summary>Seçilen alıntıları göster</summary>
              {sourceQuotes.map((quote, index) => <blockquote key={index}>{quote}</blockquote>)}
            </details>
            <button type="button" disabled={drafting} onClick={() => { setSourceQuotes([]); setSourceBody(null); }}>Kaynakları kaldır</button>
          </div>}
          <label className="draft-field">
            <span>Yazışma türü</span>
            <select value={draftMode} disabled={drafting} onChange={event => {
              setDraftMode(event.target.value as DraftMode);
              setDraftResult(null);
              setDraftError(null);
              setCopied(false);
            }}>
              <option value="reply">Gönderene yanıt</option>
              <option value="forward">Başka kişilere ilet</option>
            </select>
          </label>
          {draftMode === "forward" && <label className="draft-field">
            <span>Kime yazılacak?</span>
            <textarea value={targetAudience} onChange={event => setTargetAudience(event.target.value)}
              required maxLength={500} rows={2} disabled={drafting}
              placeholder="Örn. Grup müdürüm ve operasyon yöneticisi." />
            <small>{targetAudience.length}/500</small>
          </label>}
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
            <span>Mailin amacı / ana mesajı</span>
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

          <label className="draft-field">
            <span>Dikkat edilecek hususlar <em>(opsiyonel)</em></span>
            <textarea value={considerations} onChange={event => setConsiderations(event.target.value)}
              maxLength={2000} rows={3} disabled={drafting}
              placeholder="Örn. Kişisel suçlama yapma, tarih taahhüdü verme, yönetimden somut aksiyon iste." />
            <small>{considerations.length}/2000</small>
          </label>

          <button
            className="draft-button"
            type="button"
            onClick={handleDraft}
            disabled={drafting || loading || !draftInstructions.trim() || (draftMode === "forward" && !targetAudience.trim())}
          >
            {drafting ? "Taslak hazırlanıyor…" : "Taslak oluştur"}
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
              <ContentProcessingNotice processing={draftResult.contentProcessing} wasTruncated={draftResult.wasTruncated} />
              <p className="metadata">
                {draftResult.model} · {(draftResult.durationMilliseconds / 1000).toFixed(1)} sn
              </p>
            </div>
          )}
        </section>
      )}

      <footer>E-posta içeriği yalnızca yapılandırdığınız API ve AI sağlayıcısına gönderilir.</footer>
    </main>
  );
}
