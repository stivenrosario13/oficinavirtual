type OperationalError = {
  action: string;
  detail: string;
  reference?: string;
};

const recentlyReported = new Map<string, number>();
const COOLDOWN_MS = 30_000;

/** Reports a failed operation without delaying or changing the user's flow. */
export function reportOperationalError(error: OperationalError): void {
  if (typeof window === "undefined" || !navigator.onLine) return;
  const fingerprint = `${error.action}|${error.reference || ""}|${error.detail}`.slice(0, 700);
  const previous = recentlyReported.get(fingerprint) || 0;
  if (Date.now() - previous < COOLDOWN_MS) return;
  recentlyReported.set(fingerprint, Date.now());
  if (recentlyReported.size > 100) recentlyReported.clear();

  void globalThis.fetch("/api/operational-errors", {
    method: "POST",
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      action: error.action.slice(0, 80),
      detail: error.detail.slice(0, 1600),
      reference: error.reference?.slice(0, 240) || "",
    }),
  }).catch(() => undefined);
}
