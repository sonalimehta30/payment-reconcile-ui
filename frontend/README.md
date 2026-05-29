# Frontend - Payment Reconciliation

Angular 21 standalone component application for the Payment Reconciliation Tool UI.

## Architecture

- **Framework**: Angular 21 with Vite build
- **Styling**: SCSS with Bootstrap classes
- **Forms**: Reactive forms with FormControl and FormGroup
- **HTTP**: RxJS observables

## Prerequisites

- Node.js 18+ with npm v10+
- Angular CLI (optional)

## Development Server

```bash
cd frontend
npm install
npm start
```

Open http://localhost:4200/ in your browser. App reloads automatically on file changes.

## Build

```bash
npm run build
```

Output is in `dist/payment-reconcile-ui/browser`.

## Project Structure

```
src/
├── app/
│   ├── components/
│   │   ├── payment-matching/         # Main upload & results component
│   │   │   ├── payment-matching.ts
│   │   │   ├── payment-matching.html
│   │   │   └── payment-matching.scss
│   │   └── reusable-table/           # Shared component for table display 
│   │       ├── reusable-table.ts
│   │       └── reusable-table.scss
│   ├── services/
│   │   └── payment-matching.service.ts  # HTTP calls
│   ├── models/
│   │   └── payment-matching.models.ts   # TypeScript interfaces
│   ├── app.routes.ts                    # Route definitions
│   ├── app.config.ts                    # App configuration
│   ├── app.ts                           # Root component
│   └── app.scss                         # Global styles
├── main.ts                              # Bootstrap
└── index.html
```

## Key Features

### File Upload with Validation

- Reactive form controls: `systemFile`, `providerFile`
- CSV extension validation (custom validator)
- Error messages for unsupported file types
- Touch tracking for error display

### CSV Validation

```typescript
private csvFileValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const file = control.value as File | null;
    if (!file) return null;
    return /\.csv$/i.test(file.name) ? null : { unsupportedFileType: true };
  };
}
```

### Reactive Form Control

```typescript
form: FormGroup = new FormGroup({
  systemFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
  providerFile: new FormControl<File | null>(null, [this.csvFileValidator()]),
});
filterControl: FormControl<MatchFilter | null> = new FormControl<MatchFilter>('unresolved');
```

### Observable-Based HTTP

```typescript
onRunMatch(): void {
  this.paymentMatchingService.runMatch(systemFile, providerFile).subscribe({
    next: (response) => {
      this.summary.set(response.summary);
      this.records.set(response.records);
    },
    error: (error) => { /* handle error */ }
  });
}

onFilterChange(value: MatchFilter): void {
  this.paymentMatchingService.getMatches(value === 'all' ? undefined : value).subscribe({
    next: (records) => { this.records.set(records); }
  });
}
```

### Server-Side Filtering

Filtering is delegated to the backend:

- Filter control: `all`, `resolved`, `unresolved`
- Calls `GET /api/match/getMatches?filter=...`
- Backend returns only matching records (no UI-side filtering)

### Match Status Display

Match statuses are PascalCase strings:

- `Matched`
- `AmountMismatch`
- `OnlySystem`
- `OnlyProvider`

Formatted for display using `formatStatus()` method.

### Table Actions

Disabled buttons for records that:

- Are already resolved
- Have `status === 'Matched'` (no resolution needed)
- Missing the required amount (null systemAmount for "Accept System", etc.)

## API Integration

### PaymentMatchingService

Located in `src/app/services/payment-matching.service.ts`:

- **`runMatch(systemFile, providerFile): Observable<MatchRunResponse>`**
  - POST to `/api/match/process` with multipart form data
  - Returns summary + records

- **`getMatches(filter?): Observable<PaymentMatchRecord[]>`**
  - GET from `/api/match/getMatches?filter=...`
  - Returns records only (no summary)

- **`resolveMatch(recordId, resolutionSide): Observable<PaymentMatchRecord>`**
  - POST to `/api/match/resolve` with recordId and side
  - Returns updated record

### Backend Communication

```typescript
private get apiUrl(): string {
  // Check for injected API URL (for Docker/production deployments)
  if (typeof window !== 'undefined' && window['API_BASE_URL']) {
    return window['API_BASE_URL'];
  }
  // Dev fallback to localhost:5146
  return 'http://localhost:5146';
}
```

## TypeScript Models

In `src/app/models/payment-matching.models.ts`:

```typescript
export type PaymentStatus = 'Matched' | 'OnlySystem' | 'OnlyProvider' | 'AmountMismatch';
export type ResolutionSide = 'System' | 'Provider' | null;
export type MatchFilter = 'all' | 'resolved' | 'unresolved';

interface PaymentMatchRecord {
  id: string;
  orderId: string;
  currency: string;
  systemAmount: number | null;
  providerAmount: number | null;
  status: PaymentStatus;
  resolved: boolean;
  resolutionSide: ResolutionSide;
}

interface MatchRunResponse {
  summary: MatchSummary;
  records: PaymentMatchRecord[];
}
```

## Signals & Computed Values

Component uses Angular signals for state management:

```typescript
readonly records = signal<PaymentMatchRecord[]>([]);
readonly summary = signal<MatchSummary | null>(null);
readonly isLoading = signal(false);
readonly errorMessage = signal<string | null>(null);
readonly filteredRecords = computed(() => this.records());
```

## Tests

```bash
npm test
```

Runs Karma + Jasmine test suite.

## Notes

- **No SSR**: SSR is disabled to keep the app simple. The app runs in CSR (Client-Side Rendering) mode.
- **Reactive**: All forms and data flow use reactive patterns (FormControl + observables).
- **Server-Driven**: All filtering, parsing, and matching logic runs on the backend.
- **No Client-Side Filtering**: The UI only displays what the backend returns; filtering is done server-side.

## Dependencies

Key npm packages (see `package.json`):

- `@angular/core`, `@angular/common`, `@angular/forms`
- `rxjs`
- `bootstrap` (for styling)
- `typescript`
