import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import {
  FormsModule,
  ReactiveFormsModule,
  AbstractControl,
  FormControl,
  FormGroup,
  ValidatorFn,
  ValidationErrors,
} from '@angular/forms';

import { ReusableTableComponent, TableAction, TableColumn } from '../reusable-table/reusable-table';
import {
  MatchFilter,
  MatchSummary,
  PaymentMatchRecord,
} from '../../models/payment-matching.models';
import { PaymentMatchingService } from '../../services/payment-matching.service';

@Component({
  selector: 'app-payment-matching',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ReusableTableComponent],
  templateUrl: './payment-matching.html',
  styleUrls: ['./payment-matching.scss'],
})
export class PaymentMatchingComponent {
  private readonly paymentMatchingService = inject(PaymentMatchingService);

  readonly title = signal('Payments Matching');
  readonly records = signal<PaymentMatchRecord[]>([]);
  readonly summary = signal<MatchSummary | null>(null);
  readonly currentSessionId = signal<string | null>(null);
  readonly selectedFilter = signal<MatchFilter>('all');
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  form: FormGroup = new FormGroup({
    systemFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
    providerFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
  });
  systemFileControl!: FormControl<File | null>;
  providerFileControl!: FormControl<File | null>;
  filterControl: FormControl<MatchFilter | null> = new FormControl<MatchFilter>('unresolved');

  ngOnInit(): void {
    this.systemFileControl = this.form.get('systemFile') as FormControl<File | null>;
    this.providerFileControl = this.form.get('providerFile') as FormControl<File | null>;
    this.filterControl.valueChanges.subscribe((value) => {
      if (value) this.onFilterChange(value as MatchFilter);
    });

    // Restore session id if previously stored
    try {
      const stored = localStorage.getItem('reconSessionId');
      if (stored) this.currentSessionId.set(stored);
    } catch {
      // ignore storage errors
    }
  }

  // Backend returns pre-filtered records; do not apply client-side filtering.
  readonly filteredRecords = computed(() => this.records());

  readonly tableColumns: TableColumn<PaymentMatchRecord>[] = [
    {
      key: 'orderId',
      label: 'Order ID',
      value: (row) => row.orderId,
      className: 'fw-semibold',
    },
    {
      key: 'currency',
      label: 'Currency',
      value: (row) => row.currency,
    },
    {
      key: 'status',
      label: 'Status',
      value: (row) => this.formatStatus(row.status),
    },
    {
      key: 'systemAmount',
      label: 'System Amount',
      value: (row) => this.formatAmount(row.systemAmount),
    },
    {
      key: 'providerAmount',
      label: 'Provider Amount',
      value: (row) => this.formatAmount(row.providerAmount),
    },
    {
      key: 'resolved',
      label: 'Resolved',
      hasTooltip: true,
      tooltip: 'Only applicable for unmatched records. Displays `NA` when there is no mismatch in the record details.',
      value: (row) => this.formatResolved(row),
    },
    {
      key: 'resolutionSide',
      label: 'Resolution Side',
      value: (row) => row.resolutionSide ?? '—',
    },
  ];

  readonly tableActions = computed<TableAction<PaymentMatchRecord>[]>(() => [
    {
      id: 'accept-system',
      label: 'Accept System',
      variant: 'outline-primary',
      visible: (row) => row.status !== 'Matched' && !row.resolved,
      disabled: (row) => row.systemAmount === null,
      onClick: (row) => this.acceptResolution(row, 'System'),
    },
    {
      id: 'accept-provider',
      label: 'Accept Provider',
      variant: 'outline-success',
      visible: (row) => row.status !== 'Matched' && !row.resolved,
      disabled: (row) => row.providerAmount === null,
      onClick: (row) => this.acceptResolution(row, 'Provider'),
    },
  ]);

  onSystemFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.systemFileControl.markAsTouched();
    this.systemFileControl.setErrors(null);

    if (file && !this.isCsvFile(file)) {
      this.systemFileControl.setValue(null);
      this.systemFileControl.setErrors({ unsupportedFileType: true });
      this.errorMessage.set('Unsupported file type. Please upload a .csv file.');
      return;
    }

    this.systemFileControl.setValue(file);
    this.resetMessages();
  }

  onProviderFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.providerFileControl.markAsTouched();
    this.providerFileControl.setErrors(null);

    if (file && !this.isCsvFile(file)) {
      this.providerFileControl.setValue(null);
      this.providerFileControl.setErrors({ unsupportedFileType: true });
      this.errorMessage.set('Unsupported file type. Please upload a .csv file.');
      return;
    }

    this.providerFileControl.setValue(file);
    this.resetMessages();
  }

  onRunMatch(): void {
    const systemFile = this.systemFileControl.value;
    const providerFile = this.providerFileControl.value;

    if (!systemFile || !providerFile) {
      this.errorMessage.set('Please upload both the System CSV and Provider CSV files.');
      return;
    }

    if (this.form.invalid) {
      this.errorMessage.set('Unsupported file type. Please upload valid .csv files.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.paymentMatchingService.runMatch(systemFile, providerFile).subscribe({
      next: (response) => {
        this.summary.set(response.summary);
        this.records.set(response.records);
        // persist session id so subsequent filter calls are scoped
        this.currentSessionId.set(response.sessionId);
        try { localStorage.setItem('reconSessionId', response.sessionId); } catch { }
        this.successMessage.set('Match completed. Review the results and resolve any open items.');
        this.isLoading.set(false);
      },
      error: (error) => {
        const message = error instanceof Error ? error.message : 'Unable to process the CSV files.';
        this.errorMessage.set(message);
        this.summary.set(null);
        this.records.set([]);
        this.isLoading.set(false);
      },
    });
  }

  private isCsvFile(file: File): boolean {
    return /\.csv$/i.test(file.name);
  }

  private csvFileValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const file = control.value as File | null;
      if (!file) {
        return null;
      }

      return this.isCsvFile(file) ? null : { unsupportedFileType: true };
    };
  }

  onFilterChange(value: MatchFilter): void {
    this.selectedFilter.set(value);
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filterParam = value === 'all' ? undefined : value;
    const sessionId = this.currentSessionId();
    this.paymentMatchingService.getMatches(sessionId ?? undefined, filterParam).subscribe({
      next: (records) => {
        // getMatches returns only records; summary is managed from the process API
        this.records.set(records);
        this.isLoading.set(false);
      },
      error: (err) => {
        const message = err instanceof Error ? err.message : 'Unable to fetch filtered records.';
        this.errorMessage.set(message);
        this.isLoading.set(false);
      },
    });
  }

  acceptResolution(record: PaymentMatchRecord, resolutionSide: 'System' | 'Provider'): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.paymentMatchingService.resolveMatch(record.id, resolutionSide).subscribe({
      next: (updatedRecord) => {
        this.records.set(
          this.records().map((current) =>
            current.id === updatedRecord.id ? updatedRecord : current,
          ),
        );
        this.successMessage.set(
          `Resolution saved for order ${record.orderId} as ${resolutionSide}.`,
        );
        this.isLoading.set(false);
      },
      error: (error) => {
        const message = error instanceof Error ? error.message : 'Unable to save the resolution.';
        this.errorMessage.set(message);
        this.isLoading.set(false);
      },
    });
  }

  private formatStatus(status: PaymentMatchRecord['status']): string {
    return status
      .replace(/([A-Z])/g, ' $1')
      .replace(/^\s/, '')
      .replace(/\s+/g, ' ')
      .trim();
  }

  private formatAmount(amount: number | null): string {
    return amount === null ? '—' : amount.toFixed(2);
  }
  
  private formatResolved(rowData: PaymentMatchRecord): string {
    return rowData.status !== 'Matched' ? (rowData.resolved ? 'Yes' : 'No') : 'N/A';
  }

  private resetMessages(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }
}
