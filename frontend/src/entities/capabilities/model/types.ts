/**
 * Backend feature flags (GET /api/v1/capabilities).
 *
 * Always read these at runtime via `useCapabilities` and gate the relevant UI on
 * them — never hard-code the features as available. The backend can be given (or
 * have removed) the provider token independently of any frontend deploy, so the
 * answer can change without the SPA changing.
 */
export interface Capabilities {
  /** QR scanning + receipt fetching — off when the provider token is not configured. */
  receiptScanning: boolean
  /**
   * Import from the «Налоги ФЛ» Excel export. Always `true`: the import parses a
   * user-supplied file and never calls the provider, so it does not depend on the
   * provider token. Kept as its own flag so the UI can gate on it independently.
   */
  fnsImport: boolean
}
