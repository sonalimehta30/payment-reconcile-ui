import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { MatchRunResponse, PaymentMatchRecord } from '../models/payment-matching.models';

@Injectable({
  providedIn: 'root',
})
export class PaymentMatchingService {
  private readonly http = inject(HttpClient);

  private get apiUrl(): string {
    const overrideUrl = typeof window !== 'undefined' ? (window as any)['API_BASE_URL'] : undefined;

    if (typeof overrideUrl === 'string' && overrideUrl.trim().length > 0) {
      return overrideUrl.replace(/\/$/, '') + '/api/match';
    }

    if (typeof window !== 'undefined' && window.location.hostname) {
      const origin = `${window.location.protocol}//${window.location.hostname}:5146`;
      return `${origin}/api/match`;
    }

    return 'http://localhost:5000/api/match';
  }

  async runMatch(systemFile: File, providerFile: File): Promise<MatchRunResponse> {
    const formData = new FormData();
    formData.append('systemFile', systemFile, systemFile.name);
    formData.append('providerFile', providerFile, providerFile.name);

    return firstValueFrom(this.http.post<MatchRunResponse>(this.apiUrl, formData));
  }

  resolveRecord(
    records: PaymentMatchRecord[],
    recordId: string,
    resolutionSide: 'System' | 'Provider',
  ): PaymentMatchRecord[] {
    return records.map((record) =>
      record.id === recordId
        ? {
            ...record,
            resolved: true,
            resolutionSide,
          }
        : record,
    );
  }
}
