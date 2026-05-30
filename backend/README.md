# Backend - PaymentsMatching.Api

A .NET 10 minimal Web API for payment record matching, CSV parsing, and reconciliation workflows.

## Architecture

- **Framework**: .NET 10 Minimal APIs
- **Database**: SQLite with EF Core
- **Session Management**: Per-upload ReconciliationSession (Guid-based isolation)
- **API Documentation**: Swashbuckle Swagger UI

## Prerequisites

- .NET 10 SDK

## Running Locally

```powershell
cd backend/PaymentsMatching.Api
dotnet run
```

**Server**: http://localhost:5146  
**Swagger UI**: http://localhost:5146/swagger

## API Endpoints

All endpoints use base path `/api/match`:

### POST /process

Upload two CSV files and run the matching algorithm.

**Request**: Multipart form-data
- `systemFile` (File) - System payment export CSV
- `providerFile` (File) - Provider payment export CSV

**Response** (MatchResponseDto):
```json
{
  "sessionId": "c3f8c7d1-...",
  "summary": {
    "total": 100,
    "matched": 85,
    "amountMismatch": 10,
    "onlySystem": 3,
    "onlyProvider": 2
  },
  "records": [
    {
      "id": "uuid",
      "orderId": "ORD123",
      "currency": "USD",
      "systemAmount": 100.00,
      "providerAmount": 99.50,
      "status": "AmountMismatch",
      "resolved": false,
      "resolutionSide": null
    }
  ]
}
```

**Behavior**:
- Creates a new `ReconciliationSession`
- Parses both CSV files
- Matches records using composite key: `OrderId|Currency` (case-insensitive)
- Persists all records (both matched and unmatched)
- Returns unresolved records (status != `Matched`) + summary

### GET /getMatches

Retrieve persisted match records for a specific reconciliation session with optional filtering.

**Query Parameters**:
- `sessionId` (required): the reconciliation session Guid returned by `POST /process`
- `filter` (optional): `all`, `resolved`, `unresolved`
  - `resolved`: status == `Matched`
  - `unresolved`: status != `Matched` (catches AmountMismatch, OnlySystem, OnlyProvider)
  - `all` or missing: all records for the supplied `sessionId`

**Response**: Array of PaymentMatchRecordDto

**Note**: This endpoint returns records only (no summary). Summary is provided by the process endpoint.

### POST /resolve

Mark a record as resolved and set the chosen amount.

**Request** (ResolveRequestDto):
```json
{
  "recordId": "uuid",
  "resolutionSide": "System" or "Provider"
}
```

**Response**: Updated PaymentMatchRecordDto with `resolved: true` and `resolutionSide` set.

## Match Statuses

Records are assigned one of four statuses during matching:

| Status | Meaning |
|--------|----------|
| `Matched` | Record exists in both files with same amount |
| `AmountMismatch` | Record exists in both files but amounts differ |
| `OnlySystem` | Record exists only in system export |
| `OnlyProvider` | Record exists only in provider export |

## Data Model

### ReconciliationSession

Isolates records per upload session:
- `Id` (Guid): Primary key
- `CreatedAt` (DateTime): Session creation time
- `File1Name` (string): System file name
- `File2Name` (string): Provider file name
- `MatchResults` (ICollection): Related records

### MatchResult

- `Id` (Guid): Primary key (for resolve operations)
- `SessionId` (Guid): FK to ReconciliationSession (session isolation)
- `OrderId` (string): From CSV
- `Currency` (string): From CSV
- `SystemAmount` (decimal?): Amount from system file (null if OnlyProvider)
- `ProviderAmount` (decimal?): Amount from provider file (null if OnlySystem)
- `Status` (MatchStatus enum): One of the four statuses above
- `Resolved` (bool): Whether user has resolved this record
- `ResolutionSide` (ResolutionSide?): System or Provider (set by resolve endpoint)
- `CreatedAt` (DateTime): Record creation time

**Unique Constraint**: (SessionId, OrderId, Currency) ensures no duplicate records within a session.

## CSV Format

Both CSV files must have columns: `OrderId`, `Amount`, `Currency`

Example:
```csv
OrderId,Amount,Currency
ORD001,100.00,USD
ORD002,250.50,EUR
```

## Service Layer

### CsvParserService

- Parses CSV files using CsvHelper
- Trims and normalizes values
- Returns `List<PaymentRecord>`

### MatchService

- **RunMatchAsync**: Creates session, parses files, matches records, persists all, returns unresolved + summary
- **GetMatchesAsync**: Filters and returns records (no summary)
- **ResolveAsync**: Updates record resolution status
- Composite key matching: `OrderId.Trim() | Currency.Trim().ToUpperInvariant()`

## Configuration

Database connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=payment-matching.db"
  }
}
```

Database file is created automatically in the project root on first run.

## NuGet Packages

- `Microsoft.EntityFrameworkCore.Sqlite`
- `CsvHelper`
- `Swashbuckle.AspNetCore.SwaggerGen`
- `Swashbuckle.AspNetCore.SwaggerUi`

See `PaymentsMatching.Api.csproj` for full reference.
