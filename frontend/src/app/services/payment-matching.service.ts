import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
      // hardcoded port 5146(as per my device) for backend API -
      // in a real app this would be handled via environment variables or a config file
      const origin = `${window.location.protocol}//${window.location.hostname}:5146`;
      return `${origin}/api/match`;
    }

    return 'http://localhost:5000/api/match';
  }

  runMatch(systemFile: File, providerFile: File): Observable<MatchRunResponse> {
    const formData = new FormData();
    formData.append('systemFile', systemFile, systemFile.name);
    formData.append('providerFile', providerFile, providerFile.name);

    return this.http.post<MatchRunResponse>(`${this.apiUrl}/process`, formData);
  }

  getMatches(sessionId?: string, filter?: string): Observable<PaymentMatchRecord[]> {
    const params: { [key: string]: string } = {};
    if (sessionId) params['sessionId'] = sessionId;
    if (filter) params['filter'] = filter;
    const options = Object.keys(params).length > 0 ? { params } : {};
    return this.http.get<PaymentMatchRecord[]>(`${this.apiUrl}/getMatches`, options);
  }

  resolveMatch(recordId: string, resolutionSide: 'System' | 'Provider'): Observable<PaymentMatchRecord> {
    return this.http.post<PaymentMatchRecord>(`${this.apiUrl}/resolve`, {
      recordId,
      resolutionSide,
    });
  }
}
