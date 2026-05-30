import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

export interface TableColumn<T> {
  key: string;
  label: string;
  hasTooltip?: boolean;
  tooltip?: string;
  value: (row: T) => string;
  className?: string;
}

export interface TableAction<T> {
  id: string;
  label: string;
  variant?: string;
  disabled?: (row: T) => boolean;
  visible?: (row: T) => boolean;
  onClick: (row: T) => void;
}

@Component({
  selector: 'app-reusable-table',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './reusable-table.html',
  styleUrl: './reusable-table.scss',
})
export class ReusableTableComponent<T> {
  @Input() rows: T[] = [];
  @Input() columns: TableColumn<T>[] = [];
  @Input() actions: TableAction<T>[] = [];
  @Input() emptyMessage = 'No records found.';

  trackByRow = (_index: number, row: T): string => JSON.stringify(row);
}
