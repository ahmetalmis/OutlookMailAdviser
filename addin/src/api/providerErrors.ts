const messages: Record<string, string> = {
  openai_authentication_failed: "OpenAI API anahtarı/token kabul edilmedi. Hizmette tanımlı anahtarı güncelleyin.",
  openai_access_denied: "OpenAI API anahtarının bu modele veya kaynağa erişim izni yok. Proje ve anahtar izinlerini kontrol edin.",
  openai_quota_exceeded: "OpenAI kotası veya API kredisi tükendi. API faturalandırmasını ve proje limitlerini kontrol edin.",
  openai_rate_limited: "OpenAI istek/token hız sınırına ulaşıldı. Bir süre bekleyip yeniden deneyin.",
  openai_context_limit: "E-posta ve soru modelin giriş token sınırını aşıyor. İçeriği kısaltıp yeniden deneyin.",
  openai_output_limit: "Yanıt, çıkış token sınırında kesildi. Hizmetin MaxOutputTokens ayarını artırın veya daha kısa bir yanıt isteyin.",
  openai_unavailable: "OpenAI bağlantısı kurulamadı. İnternet, proxy ve sağlayıcı adresini kontrol edin.",
  ollama_unavailable: "Yerel Ollama hizmetine ulaşılamıyor. Ollama'nın çalıştığını kontrol edin.",
  model_timeout: "AI sağlayıcısı zamanında yanıt vermedi. Bir süre sonra yeniden deneyin.",
  model_not_found: "Yapılandırılmış model bulunamadı veya erişilemiyor. Model adını ve erişim izinlerini kontrol edin.",
  model_provider_error: "AI sağlayıcısı isteği tamamlayamadı. Hizmetin sağlayıcı ayarlarını kontrol edin.",
  invalid_model_response: "AI yanıtı beklenen biçimde değil. Yeniden deneyin.",
};

export function providerErrorMessage(code: string | undefined, fallback: string): string {
  return code && Object.hasOwn(messages, code) ? messages[code] : fallback;
}
