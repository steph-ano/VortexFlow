/**
 * Defensive metric formatters used by views that render trend metrics.
 *
 * The backend can return a trend with `metrics: null` or with a partial
 * object when an upstream scraper fails to capture a value, even though
 * the TypeScript type marks these fields as required. We never want a
 * single missing field to crash a render of the dashboard, so every
 * formatter treats the input as `unknown` and falls back to a safe default.
 */

const DEFAULT_SENTIMENT = '0.00';
const DEFAULT_VOLUME = '0';

/**
 * Returns a fixed-point string for a sentiment value in `[-1, 1]`.
 * Out-of-range values are clamped, NaN/Infinity fall back to the default.
 */
export function formatSentiment(value: unknown, digits = 2): string {
  const n = Number(value);
  if (!Number.isFinite(n)) return DEFAULT_SENTIMENT;
  const clamped = Math.max(-1, Math.min(1, n));
  return clamped.toFixed(digits);
}

/**
 * Returns a localised integer string for a non-negative volume value.
 * Negative or non-finite inputs fall back to the default.
 */
export function formatVolume(value: unknown): string {
  const n = Number(value);
  if (!Number.isFinite(n) || n < 0) return DEFAULT_VOLUME;
  return Math.round(n).toLocaleString();
}
