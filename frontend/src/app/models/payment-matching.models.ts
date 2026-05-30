export type PaymentStatus = 'Matched' | 'OnlySystem' | 'OnlyProvider' | 'AmountMismatch';
export type ResolutionSide = 'System' | 'Provider' | null;

export interface PaymentCsvRow {
  orderId: string;
  amount: number;
  currency: string;
}

export interface PaymentMatchRecord {
  id: string;
  orderId: string;
  currency: string;
  systemAmount: number | null;
  providerAmount: number | null;
  status: PaymentStatus;
  resolved: boolean;
  resolutionSide: ResolutionSide;
}

export interface MatchSummary {
  total: number;
  matched: number;
  onlySystem: number;
  onlyProvider: number;
  amountMismatch: number;
}

export interface MatchRunResponse {
  sessionId: string;
  summary: MatchSummary;
  records: PaymentMatchRecord[];
}

export interface ResolutionUpdateRequest {
  recordId: string;
  resolutionSide: 'System' | 'Provider';
}

export type MatchFilter = 'all' | 'resolved' | 'unresolved';
