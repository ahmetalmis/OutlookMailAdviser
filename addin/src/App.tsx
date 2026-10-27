import { ActionNotes } from "./components/ActionNotes";
import type { NewActionNote } from "./types";
import { LanguageProvider, useLanguage, readPreference, savePreference } from "./i18n/LanguageContext";
import { errorText, UiError } from "./i18n/messages";
import { useCallback, useEffect, useRef, useState } from "react";
import { analyzeMail, draftMail, getApiStatus } from "./api/mailAnalysisApi";
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
  return <LanguageProvider><MailAdviser /></LanguageProvider>;
}

function MailAdviser() {
  const { language, setLanguage, t, number } = useLanguage();
  const [notesOpen, setNotesOpen] = useState(false);
  const [noteSelection, setNoteSelection] = useState<NewActionNote | null>(null);
  const clearNoteSelection = useCallback(() => setNoteSelection(null), []);
  const itemVersion = useRef(0);
  const statusRequest = useRef<AbortController | null>(null);
  const [draftOpened, setDraftOpened] = useState(false);
  const [sourceQuotes, setSourceQuotes] = useState<string[]>([]);
  const [sourceBody, setSourceBody] = useState<string | null>(null);
  const [message, setMessage] = useState<MailMessage | null>(null);
  const [status, setStatus] = useState<ApiStatus>({ state: "unhealthy", code: "checking" });
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [result, setResult] = useState<AnalyzeMailResponse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(false);
  const [drafting, setDrafting] = useState(false);
  const [draftTone, setDraftTone] = useState<DraftTone>("professional");
  const [draftToneDetails, setDraftToneDetails] = useState("");
  const [draftMode, setDraftMode] = useState<DraftMode>("reply");
  const [targetAudience, setTargetAudience] = useState("");
  const [considerations, setConsiderations] = useState("");
  const [draftInstructions, setDraftInstructions] = useState("");
  const [draftResult, setDraftResult] = useState<DraftMailResponse | null>(null);
  const [draftError, setDraftError] = useState<unknown>(null);
  const [copied, setCopied] = useState(false);
  const [outputLanguage, setOutputLanguage] = useState<OutputLanguage>(() =>
    readPreference(OUTPUT_LANGUAGE_KEY) === "en" ? "en" : "tr",
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
      setError(readError ?? new UiError("readFailed"));
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
      setError(analysisError ?? new UiError("analysisFailed"));
    } finally {
      if (version === itemVersion.current) setLoading(false);
    }
  }

  async function handleDraft() {
    const version = itemVersion.current;
    const instructions = draftInstructions.trim();
    if (!instructions) {
      setDraftError(new UiError("instructionsRequired"));
      return;
    }

    if (!["reply", "forward"].includes(draftMode)) {
      setDraftError(new UiError("invalidDraftMode"));
      return;
    }
    if (draftMode === "forward" && !targetAudience.trim()) {
      setDraftError(new UiError("audienceRequired"));
      return;
    }
    if (targetAudience.trim().length > 500 || considerations.trim().length > 2000) {
      setDraftError(new UiError("draftLimits"));
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
        throw new UiError("staleSources");
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
      setDraftError(draftingError ?? new UiError("draftFailed"));
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
      setDraftError(new UiError("copyFailed"));
    }
  }

  function handleLanguageChange(language: OutputLanguage) {
    setOutputLanguage(language);
    savePreference(OUTPUT_LANGUAGE_KEY, language);
    setResult(null);
  }

  return (
    <main>
      <header className="app-header">
        <div className="logo">MA</div>
        <div>
          <h1>Mail Adviser</h1>
          <p>{t("intro")}</p>
        </div>
      </header>

      <label className="language-field">
        <span>{t("uiLanguage")}</span>
        <select value={language} onChange={event => setLanguage(event.target.value as OutputLanguage)}>
          <option value="tr">Türkçe</option>
          <option value="en">English</option>
        </select>
      </label>

      <ActionNotes open={notesOpen} onOpenChange={setNotesOpen} selection={noteSelection} onSelectionClear={clearNoteSelection} />
      <div className="mail-workspace" hidden={notesOpen}>
      <div className={`status ${status.state}`} role="status" aria-live="polite">
        <span className="status-dot" aria-hidden="true" />
        <span className="status-message">
          {status.state === "healthy"
            ? `${t("apiReady")}${status.provider ? ` · ${status.provider}` : ""}${status.model ? ` / ${status.model}` : ""}`
            : `${t("apiNotReady")} · ${errorText({ code: status.code }, t, "provider_not_ready")}`}
        </span>
        <button className="status-retry" type="button" onClick={() => void refreshStatus()} disabled={checkingStatus}>
          {checkingStatus ? t("checking") : t("retry")}
        </button>
      </div>

      {message && (
        <section className="mail-card">
          <span className="eyebrow">{t("currentEmail")}</span>
          <h2>{message.subject || t("noSubject")}</h2>
          <p>{message.from.name || message.from.address}</p>
          {message.from.name && <small>{message.from.address}</small>}
        </section>
      )}



      <label className="language-field">
        <span>{t("outputLanguage")}</span>
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
        {loading ? t("analyzing") : t("analyze")}
      </button>

      {loading && <div className="progress" role="progressbar" aria-label={t("analyzing")}><span /></div>}
      {error != null && <div className="error" role="alert">{errorText(error, t)}</div>}
      {result && <AnalysisResult result={result} onTrackAction={action => {
        setNoteSelection({ id: crypto.randomUUID(), mailSubject: message?.subject || "", description: action.description,
          owner: action.owner, dueDate: action.dueDate && /^\d{4}-\d{2}-\d{2}$/.test(action.dueDate) ? action.dueDate : null });
        setNotesOpen(true);
      }} />}

      {message && <MailQuestions message={message} language={outputLanguage} useSourcesDisabled={drafting || loading} onUseSources={quotes => {
        setDraftOpened(true);
        setSourceQuotes(quotes);
        setSourceBody(message.body);
        setDraftResult(null);
        if (!draftInstructions.trim()) setDraftInstructions(t("defaultInstructions"));
      }} />}

      {(result || draftOpened) && (
        <section className="draft-composer">
          <div>
            <span className="eyebrow">{t("draftEyebrow")}</span>
            <h2>{t("compose")}</h2>
            <p>{t("composeHelp")}</p>
          </div>

          {sourceQuotes.length > 0 && <div className="selected-sources" role="status">
            <p>{t("selectedSources", { count: number(sourceQuotes.length) })}</p>
            <details><summary>{t("showQuotes")}</summary>
              {sourceQuotes.map((quote, index) => <blockquote key={index}>{quote}</blockquote>)}
            </details>
            <button type="button" disabled={drafting} onClick={() => { setSourceQuotes([]); setSourceBody(null); }}>{t("removeQuotes")}</button>
          </div>}
          <label className="draft-field">
            <span>{t("draftMode")}</span>
            <select value={draftMode} disabled={drafting} onChange={event => {
              setDraftMode(event.target.value as DraftMode);
              setDraftResult(null);
              setDraftError(null);
              setCopied(false);
            }}>
              <option value="reply">{t("reply")}</option>
              <option value="forward">{t("forward")}</option>
            </select>
          </label>
          {draftMode === "forward" && <label className="draft-field">
            <span>{t("audience")}</span>
            <textarea value={targetAudience} onChange={event => setTargetAudience(event.target.value)}
              required maxLength={500} rows={2} disabled={drafting}
              placeholder={t("audienceExample")} />
            <small>{number(targetAudience.length)}/{number(500)}</small>
          </label>}
          <label className="draft-field">
            <span>{t("tone")}</span>
            <select
              value={draftTone}
              onChange={(event) => setDraftTone(event.target.value as DraftTone)}
              disabled={drafting}
            >
              <option value="professional">{t("professional")}</option>
              <option value="friendly">{t("friendly")}</option>
              <option value="concise">{t("concise")}</option>
              <option value="persuasive">{t("persuasive")}</option>
              <option value="empathetic">{t("empathetic")}</option>
            </select>
          </label>

          <label className="draft-field">
            <span>{t("toneDetails")} <em>{t("optional")}</em></span>
            <input
              type="text"
              value={draftToneDetails}
              onChange={(event) => setDraftToneDetails(event.target.value)}
              maxLength={500}
              disabled={drafting}
              placeholder={t("toneExample")}
            />
            <small>{number(draftToneDetails.length)}/{number(500)}</small>
          </label>

          <label className="draft-field">
            <span>{t("instructions")}</span>
            <textarea
              value={draftInstructions}
              onChange={(event) => setDraftInstructions(event.target.value)}
              maxLength={2000}
              rows={5}
              disabled={drafting}
              placeholder={t("instructionsExample")}
            />
            <small>{number(draftInstructions.length)}/{number(2000)}</small>
          </label>

          <label className="draft-field">
            <span>{t("considerations")} <em>{t("optional")}</em></span>
            <textarea value={considerations} onChange={event => setConsiderations(event.target.value)}
              maxLength={2000} rows={3} disabled={drafting}
              placeholder={t("considerationsExample")} />
            <small>{number(considerations.length)}/{number(2000)}</small>
          </label>

          <button
            className="draft-button"
            type="button"
            onClick={handleDraft}
            disabled={drafting || loading || !draftInstructions.trim() || (draftMode === "forward" && !targetAudience.trim())}
          >
            {drafting ? t("drafting") : t("createDraft")}
          </button>

          {drafting && <div className="progress" role="progressbar" aria-label={t("drafting")}><span /></div>}
          {draftError != null && <div className="error" role="alert">{errorText(draftError, t)}</div>}

          {draftResult && (
            <div className="draft-result" aria-live="polite">
              <div className="draft-result-heading">
                <h3>{t("preparedDraft")}</h3>
                <button type="button" onClick={handleCopyDraft}>{copied ? t("copied") : t("copy")}</button>
              </div>
              <span className="draft-subject-label">{t("subject")}</span>
              <strong className="draft-subject">{draftResult.subject}</strong>
              <div className="draft-body">{draftResult.body}</div>
              <ContentProcessingNotice processing={draftResult.contentProcessing} wasTruncated={draftResult.wasTruncated} />
              <p className="metadata">
                {draftResult.model} · {number(draftResult.durationMilliseconds / 1000, 1)} {t("seconds")}
              </p>
            </div>
          )}
        </section>
      )}

      <footer>{t("privacy")}</footer>
      </div>
    </main>
  );
}
