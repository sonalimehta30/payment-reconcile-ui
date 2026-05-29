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

import {
  ReusableTableComponent,
  TableAction,
  TableColumn,
} from '../reusable-table/reusable-table';
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
  readonly selectedFilter = signal<MatchFilter>('all');
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  
  form:FormGroup = new FormGroup({
    systemFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
    providerFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
  });
  systemFileControl!: FormControl<File | null>;
  providerFileControl!: FormControl<File | null>;
    
  ngOnInit(): void {
    this.systemFileControl = this.form.get('systemFile') as FormControl<File | null>;
    this.providerFileControl = this.form.get('providerFile') as FormControl<File | null>;
  }

  
  readonly filteredRecords = computed(() => {
    const filter = this.selectedFilter();
    const records = this.records();

    if (filter === 'resolved') {
      return records.filter((record) => record.resolved);
    }

    if (filter === 'unresolved') {
      return records.filter((record) => !record.resolved);
    }

    return records;
  });

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
      value: (row) => (row.resolved ? 'Yes' : 'No'),
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
      visible: (row) => !row.resolved,
      disabled: (row) => row.systemAmount === null || row.status === 'MATCHED',
      onClick: (row) => this.acceptResolution(row, 'System'),
    },
    {
      id: 'accept-provider',
      label: 'Accept Provider',
      variant: 'outline-success',
      visible: (row) => !row.resolved,
      disabled: (row) => row.providerAmount === null || row.status === 'MATCHED',
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

  async onRunMatch(): Promise<void> {
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

    try {
      const response = await this.paymentMatchingService.runMatch(systemFile, providerFile);
      this.summary.set(response.summary);
      this.records.set(response.records);
      this.successMessage.set('Match completed. Review the results and resolve any open items.');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to process the CSV files.';
      this.errorMessage.set(message);
      this.summary.set(null);
      this.records.set([]);
    } finally {
      this.isLoading.set(false);
    }
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
  }

  acceptResolution(record: PaymentMatchRecord, resolutionSide: 'System' | 'Provider'): void {
    this.records.set(this.paymentMatchingService.resolveRecord(this.records(), record.id, resolutionSide));
    this.successMessage.set(`Resolution saved for order ${record.orderId} as ${resolutionSide}.`);
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

  private resetMessages(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }
}
