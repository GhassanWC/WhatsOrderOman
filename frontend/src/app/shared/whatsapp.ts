/**
 * Builds a WhatsApp "click to chat" URL.
 *
 * Deliberately uses api.whatsapp.com instead of the shorter wa.me: the wa.me
 * domain fails to resolve on several Omani networks (DNS_PROBE_FINISHED_NXDOMAIN),
 * while api.whatsapp.com resolves everywhere and opens the exact same chat —
 * the WhatsApp app on mobile, WhatsApp Web/Desktop otherwise.
 *
 * The number is reduced to digits, so stored formats like "+968 9290 3631"
 * or "+96892903631" all produce a valid link.
 */
export function whatsAppLink(phone: string | null | undefined, text?: string | null): string {
  const digits = (phone ?? '').replace(/\D/g, '');
  const url = `https://api.whatsapp.com/send?phone=${digits}`;
  return text ? `${url}&text=${encodeURIComponent(text)}` : url;
}
