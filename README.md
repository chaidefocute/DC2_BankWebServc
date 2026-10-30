# DC2 Bank Web Service

COMP3008 Distributed Computing, Assignment 2.

- `DataWebAPI` is the ASP.NET Core Web API with a SQLite database (Part A). It runs on http://localhost:5122.
- `BusinessWeb` is the MVC website that calls the API (Part B). It runs on http://localhost:5150.

## Requirements

- .NET 10 SDK (built and tested with SDK 10.0.401)
- Postman, to run the API tests

There is nothing else to install or set up. The NuGet packages are restored automatically on the first build.

## How to run

From the folder that contains `COMP3008-PROJ02.slnx`:

```
dotnet run --project DataWebAPI
```

To also run the website, open a second terminal:

```
dotnet run --project BusinessWeb
```

In Visual Studio, open `COMP3008-PROJ02.slnx` and start both projects together. The saved launch profile already starts both.

## Database

On first start the API creates `DataWebAPI/App_Data/bank.db`, builds the tables and seeds it with 21 users, 35 accounts and over 500 transactions. Every account balance matches its transaction history.

To start again with fresh data, stop the API, delete the `DataWebAPI/App_Data` folder and run it again.

Profile pictures are saved as files in `DataWebAPI/App_Data/pictures`. Only the file path is stored in the database.

## Seeded logins

| Username | Password | Role |
|---|---|---|
| `admin` | `Admin@123` | Admin |
| `customer` | `Customer@123` | Customer |
| any other seeded user, e.g. `olivia.smith` | `Password@123` | Customer |

Passwords are stored as salted PBKDF2 (SHA-256) hashes, never as plain text.

## Testing with Postman

1. Start the API.
2. In Postman, import `DC2_BankWebServc.postman_collection.json`.
3. Run the collection from top to bottom. Each request checks its status code, and the requests that create a user or account save its id for the requests after it.

The "Upload profile picture" request needs a file. Before sending it, pick any .jpg or .png (max 2 MB) for the `file` key under Body > form-data.

## API endpoints

| Method | URL | Description |
|---|---|---|
| GET | `/api/health` | Check the API and database are up |
| POST | `/api/users` | Create a user |
| GET | `/api/users?search=&role=&page=&pageSize=` | List / search users |
| GET | `/api/users/{id}` | Get a user |
| GET | `/api/users/username/{username}` | Get a user by username |
| GET | `/api/users/email/{email}` | Get a user by email |
| PUT | `/api/users/{id}` | Update a user's profile |
| DELETE | `/api/users/{id}` | Deactivate a user |
| PUT | `/api/users/{id}/password` | Reset a user's password |
| POST | `/api/users/login` | Log in (returns the user and their role) |
| POST | `/api/users/{id}/picture` | Upload a profile picture (form-data, key `file`) |
| GET | `/api/users/{id}/picture` | Get a profile picture |
| POST | `/api/accounts` | Create an account |
| GET | `/api/accounts?userId=` | Get a user's accounts |
| GET | `/api/accounts/{accountNumber}` | Get an account |
| PUT | `/api/accounts/{accountNumber}` | Update an account |
| DELETE | `/api/accounts/{accountNumber}` | Deactivate an account |
| POST | `/api/transactions/deposit` | Deposit |
| POST | `/api/transactions/withdraw` | Withdraw |
| POST | `/api/transactions/transfer` | Transfer between two accounts |
| GET | `/api/transactions?accountNumber=&from=&to=&search=&page=&pageSize=&sortBy=&sortDir=` | Search transactions |
| POST | `/api/audit` | Add an audit log entry |
| GET | `/api/audit/{id}` | Get an audit log entry |
| GET | `/api/audit?adminUserId=&action=&search=&from=&to=&page=&pageSize=` | Search the audit log |

## Changes to Part A

These were added to the API so the MVC website (Part B) could use them:

- **User roles:** every user is a `Customer` or an `Admin`, and `POST /api/users/login` returns the role so the website can authorise correctly. The database is seeded with one admin and one known customer (see Seeded logins).
- **Users:** `GET /api/users` lists and searches users, and `PUT /api/users/{id}/password` resets a password.
- **Accounts and transactions:** `GET /api/accounts?userId=` returns all accounts for a user, and `GET /api/transactions` returns transactions across all accounts with date range, search, paging and sorting.
- **Audit trail:** a new `AuditLogs` table, plus `POST /api/audit`, `GET /api/audit/{id}` and `GET /api/audit`. Only admins can have entries recorded (`NOT_ADMIN` otherwise).
- **Health check:** `GET /api/health` confirms the API and database are up. The website uses it to show a warning on the home page.

## Errors

Every error uses the same JSON format:

```json
{ "error": "VALIDATION_ERROR", "message": "One or more fields are invalid.", "details": { "Email": ["The Email field is not a valid e-mail address."] } }
```

`details` lists the messages for each field when validation fails, and is `null` otherwise.

| Status | When |
|---|---|
| 200 / 201 / 204 | Success. 201 responses include a `Location` header |
| 400 | Malformed JSON or invalid fields (`VALIDATION_ERROR`) |
| 401 | Wrong username or password on login (`INVALID_CREDENTIALS`) |
| 404 | The user, account or URL does not exist (`NOT_FOUND`) |
| 409 | Duplicate username or email (`CONFLICT`) |
| 422 | Breaks a business rule, e.g. `INSUFFICIENT_FUNDS`, `SAME_ACCOUNT`, `ACCOUNT_INACTIVE`, `USER_INACTIVE` |
| 500 | Unexpected error (`INTERNAL_ERROR`). The details only go to the server log, never to the client |
