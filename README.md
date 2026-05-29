# Payment Reconciliation Tool

A full-stack application to reconcile and resolve payment records between system exports and provider files. The tool matches records using `OrderId + Currency` as the composite key, identifies mismatches, and provides resolution workflows.

## Architecture

- **Backend**: .NET 10 minimal Web API with EF Core (SQLite)
- **Frontend**: Angular 21 with reactive forms and RxJS observables
- **Database**: SQLite for lightweight, session-based record storage

## Features

- CSV file upload with validation (.csv extension only)
- Batch matching using composite key (OrderId + Currency)
- Per-upload session isolation for data consistency
- Four match statuses: `Matched`, `AmountMismatch`, `OnlySystem`, `OnlyProvider`
- Server-side filtering (resolved vs unresolved records)
- Resolution workflow (accept System or Provider amount)
- Swagger UI documentation at `/swagger`
- Reactive frontend with server-side filtering

## Quick Links

- Backend: [`backend/PaymentsMatching.Api`] (backend/PaymentsMatching.Api)
- Frontend: [`frontend`] (frontend)
- CSV samples: `provider-payment-records.csv`, `system-payments-records.csv`

## Prerequisites

- .NET 10 SDK
- Node.js 18+ with npm v10+

## Quick Start (Development)

### 1. Backend API

```powershell
cd backend/PaymentsMatching.Api
dotnet run
```

API runs on `http://localhost:5146` by default.  
Swagger UI: `http://localhost:5146/swagger`

### 2. Frontend

```bash
cd frontend
npm install
npm start
```

UI runs on `http://localhost:4200` by default.

## API Endpoints

All endpoints are under `/api/match`:

- `POST /process` - Upload CSV files and run match
- `GET /getMatches?filter=all|resolved|unresolved` - Retrieve records
- `POST /resolve` - Resolve a mismatched record

See `backend/README.md` for endpoint details.

## Project Structure

```
.
├── backend/PaymentsMatching.Api/
│   ├── Data/                 # EF Core DbContext
│   ├── Models/               # MatchResult, ReconciliationSession
│   ├── Services/             # CsvParserService, MatchService
│   ├── DTOs/                 # Request/response DTOs
│   ├── Endpoints/            # Minimal API route definitions
│   └── Properties/           # Launch settings
├── frontend/
│   ├── src/app/
│   │   ├── components/       # PaymentMatching, ReusableTable
│   │   ├── services/         # PaymentMatchingService
│   │   └── models/           # TypeScript interfaces
│   └── package.json
├── provider-payment-records.csv
├── system-payments-records.csv
└── README.md
```
## Assumptions

* The number of records in each CSV file may vary and can be unequal.
* The `Total` field in the summary represents the number of unique `order_id`s across both uploaded files.
* After parsing both the files, the records table initially displays only unresolved records that have discrepancies.
* The `Resolved` column displays `NA` when there is no mismatch in the record details, since no resolution is required in such cases.

## Development Notes

- Backend uses SQLite for session-based data storage
- Frontend uses reactive forms and observables (no SSR)
- CSV parsing happens server-side; frontend validates file type only
- Each process API call creates a new ReconciliationSession
- Filtering (`resolved`/`unresolved`) is applied server-side

See `backend/README.md` and `frontend/README.md` for detailed component documentation.
