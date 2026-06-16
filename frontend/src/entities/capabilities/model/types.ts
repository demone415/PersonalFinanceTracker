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
   * Import from the «Налоги ФЛ» export. Gated on the same provider-token flag as
   * `receiptScanning` by product decision (the whole "receipts" area is shown as
   * unavailable together), even though the import itself does not call the
   * provider. Treat it as its own flag in the UI so it can be decoupled later.
   */
  fnsImport: boolean
}
