/**
 * Shares a canonical URL via the native Web Share sheet when available, falling
 * back to copying the link. WhatsApp appears as one destination in the native
 * sheet — sharing is deliberately not tied to any single app.
 */
export async function shareOrCopy(
  title: string,
  path: string,
): Promise<'shared' | 'copied' | 'failed'> {
  const url = new URL(path, location.origin).toString();

  if (typeof navigator.share === 'function') {
    try {
      await navigator.share({ title, url });
      return 'shared';
    } catch (err) {
      // User dismissed the sheet — not an error worth surfacing.
      if ((err as DOMException)?.name === 'AbortError') return 'shared';
    }
  }

  try {
    await navigator.clipboard.writeText(url);
    return 'copied';
  } catch {
    return 'failed';
  }
}
