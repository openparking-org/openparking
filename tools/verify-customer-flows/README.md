# Disposable customer-flow verification API

Run `dotnet run --project tools/verify-customer-flows/VerifyCustomerFlows.csproj` from the repository root.
This starts the real API controllers on `http://127.0.0.1:5099`, backed by an in-memory database.
It never loads production environment files. Email and AI calls are replaced by test implementations.

Use `Authorization: Bearer verification-driver` for customer requests and
`Authorization: Bearer verification-admin` for attendant requests. These tokens work only in this verification host.
The host starts with one zone and available space; the IDs are in `CustomerFlowTestHost.cs`.
All reservations and payment records disappear when the process stops.

To verify the web gate screen, run Vite on `127.0.0.1:5199` with
`VITE_API_BASE_URL=http://127.0.0.1:5099`. The `bootstrap.js` script can be evaluated in that page
to prepare the disposable attendant login and customer reservation. Then open `/gate`, enter
`ABC-1234`, and test Check In, Check Out, and Record payment received.

The same real HTTP flow runs automatically in `CustomerFlowHttpTests`.
