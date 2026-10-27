import { useLanguage } from "../i18n/LanguageContext";
import type { ContentProcessing } from "../types";

export function ContentProcessingNotice({ processing, wasTruncated }: {
  processing?: ContentProcessing | null;
  wasTruncated: boolean;
}) {
  const { t, number } = useLanguage();
  if (!processing) {
    return wasTruncated ? <p className="warning">{t("sourceTruncated")}</p> : null;
  }
  const warning = processing.currentMessageTruncated ||
    (processing.parsingUncertain && wasTruncated);
  return <>
    {warning && <p className="warning">{t("contentOmitted")}</p>}
    <p className="metadata">
      {t("contextInfo", { count: number(processing.includedHistoryMessages) })}
      {" "}{t("characters", { count: number(processing.includedCharacters) })}
      {processing.parsingUncertain ? ` ${t("parsingUncertain")}` : ""}
    </p>
  </>;
}
