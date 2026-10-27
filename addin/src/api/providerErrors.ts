import { errorText, translator, type Language } from "../i18n/messages";

export function providerErrorMessage(code: string | undefined, language: Language = "tr"): string {
  return errorText({ code }, translator(language));
}
