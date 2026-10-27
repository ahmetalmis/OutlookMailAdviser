import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { translator, type Language, type Translate } from "./messages";

export const UI_LANGUAGE_KEY = "mail-adviser.ui-language";
export function readPreference(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}
export function savePreference(key: string, value: string): void {
  try { localStorage.setItem(key, value); } catch { /* Storage may be disabled by the host. */ }
}
export function initialLanguage(): Language {
  const saved = readPreference(UI_LANGUAGE_KEY);
  if (saved === "tr" || saved === "en") return saved;
  const outlookLanguage = typeof Office !== "undefined" ? Office.context?.displayLanguage : undefined;
  const language = outlookLanguage || navigator.language;
  return /^tr(?:-|$)/i.test(language) ? "tr" : "en";
}
interface LanguageContextValue {
  language: Language;
  setLanguage: (language: Language) => void;
  t: Translate;
  number: (value: number, decimals?: number) => string;
}
function formatNumber(language: Language, value: number, decimals?: number) {
  return value.toLocaleString(language === "tr" ? "tr-TR" : "en-US", decimals === undefined ? undefined : {
    minimumFractionDigits: decimals, maximumFractionDigits: decimals,
  });
}
const LanguageContext = createContext<LanguageContextValue>({
  language: "tr", setLanguage: () => {}, t: translator("tr"), number: (value, decimals) => formatNumber("tr", value, decimals),
});
export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, updateLanguage] = useState<Language>(initialLanguage);
  useEffect(() => { document.documentElement.lang = language; }, [language]);
  const value = useMemo<LanguageContextValue>(() => ({
    language,
    setLanguage: next => { savePreference(UI_LANGUAGE_KEY, next); updateLanguage(next); },
    t: translator(language),
    number: (value, decimals) => formatNumber(language, value, decimals),
  }), [language]);
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}
export function useLanguage() { return useContext(LanguageContext); }
