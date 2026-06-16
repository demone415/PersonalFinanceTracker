/** Backend feature flags (GET /api/v1/capabilities). */
export interface Capabilities {
  /** QR scanning + receipt fetching — off when the provider token is not configured. */
  receiptScanning: boolean
  /** Import from the «Налоги ФЛ» export — grouped with receipt scanning. */
  fnsImport: boolean
}
