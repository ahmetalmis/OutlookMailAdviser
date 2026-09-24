import type { ContentProcessing } from "../types";

export function ContentProcessingNotice({ processing, wasTruncated }: {
  processing?: ContentProcessing | null;
  wasTruncated: boolean;
}) {
  if (!processing) {
    return wasTruncated ? <p className="warning">Kaynak içerik kısaltıldı.</p> : null;
  }
  const warning = processing.currentMessageTruncated ||
    (processing.parsingUncertain && wasTruncated);
  return <>
    {warning && <p className="warning">İçeriğin bir bölümü analize dahil edilemedi.</p>}
    <p className="metadata">
      Bağlam sadeleştirildi; önceki {processing.includedHistoryMessages} mesaj dahil edildi.
      {" "}{processing.includedCharacters.toLocaleString("tr-TR")} karakter.
      {processing.parsingUncertain ? " Yazışma sınırları kesin olarak ayrıştırılamadı." : ""}
    </p>
  </>;
}
