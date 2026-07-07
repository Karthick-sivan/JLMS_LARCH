# JLMS — Full-Stack Test Build (SQL Server + ASP.NET Core + Frontend)

This is a working, end-to-end test build of the Jewel Loan Management
System: a real SQL Server database (empty, no sample data), an ASP.NET
Core 8 Web API, and the front-end UI wired to call that API for the
**core loop**: Customers → New Loan (with jewel appraisal) → Approval →
Disbursement → Interest Collection → Closure.

## What's wired vs. what's still static

**Fully wired to the database (real save/load/calculate):**
- Login (checks real Users table)
- Dashboard (live KPIs, collections-today, loans-due-today — all real queries)
- Customer Registration (saves real customers) & Customer Search (searches real customers)
- New Loan (search customer → add jewel items → live valuation → submit) — this also replaces the separate static "Jewel Appraisal" screen, since appraising jewels is part of creating a loan
- Loan Approval (real pending queue, approve/reject)
- Loan Disbursement (real disbursement, generates a receipt)
- Interest Collection (real outstanding interest/penalty calculation, real payment recording, real receipt)
- Loan Closure (real closure calculation and processing)

**Still static/demo screens** (UI only, not yet connected — same as the
original prototype): Jewel Type Master, Gold Rate Master, Loan Scheme
Master, User Master, Principal Collection, Loan Renewal, Jewel Release,
Auction Management, Report Center, Roles, Permissions, System Settings,
Audit Logs. The API endpoints for most of these already exist (see
Swagger), they're just not yet wired into those specific HTML pages —
ask if you want the next batch wired up.

---

## Step 1 — Set up the database

1. Open **SQL Server Management Studio (SSMS)** and connect to your SQL
   Server using SQL Authentication.
2. Open `database/01_schema.sql` and execute it (F5). This creates the
   `JLMS_DB` database and all tables — completely empty.
3. **Optional but recommended:** open `database/02_optional_reference_data.sql`
   and execute it. This adds the minimum lookup data the app needs to
   function (one branch, the 4 roles, 5 jewel types, 3 loan schemes,
   today's gold rate, and one admin login). It does **not** add any
   customers, loans, or transactions — those stay empty so you can test
   the full workflow from scratch.

   If you skip this script, every dropdown (Branch, Role, Jewel Type,
   Loan Scheme) will be empty and you'll need to add at least one of
   each yourself before you can create a loan.

4. Create a SQL Server login for the API to use (if you don't already
   have one), and make sure it has `db_owner` (or at least read/write)
   permission on `JLMS_DB`. Example:
   ```sql
   CREATE LOGIN jlms_app WITH PASSWORD = 'YourStrongPassword123!';
   USE JLMS_DB;
   CREATE USER jlms_app FOR LOGIN jlms_app;
   ALTER ROLE db_owner ADD MEMBER jlms_app;
   ```

## Step 2 — Configure the database connection

1. Open `api/JLMS.Api/appsettings.json`.
2. Edit the `ConnectionStrings:JlmsDb` value with your actual server
   name, username, and password:
   ```json
   "JlmsDb": "Server=YOUR_SERVER_NAME;Database=JLMS_DB;User Id=jlms_app;Password=YourStrongPassword123!;TrustServerCertificate=True;"
   ```
   - `YOUR_SERVER_NAME` is usually `localhost`, `.\SQLEXPRESS`, or
     `YOUR-PC-NAME\SQLEXPRESS` for a local install — check what you use
     to connect in SSMS.
   - `TrustServerCertificate=True` avoids SSL certificate errors on
     local dev setups. Remove it if your SQL Server has a proper
     certificate configured.

   **Better for security:** instead of editing `appsettings.json`
   directly, right-click the `JLMS.Api` project in Visual Studio →
   "Manage User Secrets" → paste the same JSON structure there. User
   Secrets are stored outside the project folder and never get
   accidentally shared or zipped up.

## Step 3 — Run the API in Visual Studio

1. Open `api/JLMS.Api.sln` in Visual Studio 2022 (with .NET 8 SDK).
2. Visual Studio should auto-restore the NuGet packages (EF Core, etc.)
   on first open. If not: right-click the solution → "Restore NuGet
   Packages".
3. Press **F5** (or click the green ▶ Run button) with the `http`
   launch profile selected.
4. A browser window should open to `http://localhost:5080/swagger` —
   this is the Swagger UI where you can see and test every API endpoint
   directly, independent of the frontend.
5. If you see a database connection error on startup or on the first
   request, double-check Step 2 — that's almost always a connection
   string issue (wrong server name, wrong password, or SQL Server not
   allowing SQL Authentication — see Troubleshooting below).

**Note:** the project does not run `EnsureCreated()` or migrations on
startup — it expects the schema to already exist from Step 1. This is
intentional, so the API never silently modifies your schema.

## Step 4 — Open the frontend

The frontend is plain HTML/JS — no build step.

1. With the API running (Step 3), open `frontend/pages/login.html`
   directly in your browser (double-click it, or right-click → Open with
   → your browser).
2. Log in with the seed admin account: **admin** / **Admin@123**
   (only works if you ran the optional reference data script in Step 1).
3. You should land on the Dashboard, showing all real zeros (since the
   database is empty) — that's correct and expected.

**If API calls fail with "Could not reach the API":**
- Check the API is actually running (the Swagger page loads at
  `http://localhost:5080/swagger`).
- Check `frontend/pages/js/api-client.js` — the `API_BASE_URL` constant
  at the top must match the port your API is actually running on. Visual
  Studio sometimes picks a different port than 5080; check the console
  window that opens when you press F5, it prints the actual URL
  (something like `Now listening on: http://localhost:5080`).

## Step 5 — Suggested test walkthrough

This exercises the full core loop on a clean database:

1. **Customer Registration** → create a test customer (just name + mobile required).
2. **New Loan** → search for that customer, add a jewel item (pick a
   jewel type, enter gross weight, e.g. 20g), click **Calculate
   Valuation** to see the market value and eligible amount, enter a
   requested loan amount within that limit, pick a scheme, submit.
3. **Loan Approval** → the loan appears in the pending queue. Click it,
   review, click **Approve Loan**.
4. **Loan Disbursement** → enter the loan number, confirm it shows
   "Approved", pick a payment mode, click **Disburse Loan**. The loan
   becomes Active.
5. **Interest Collection** → enter the loan number. Since the loan was
   just disbursed, accrued interest will be ₹0 (no time has passed) —
   that's correct. You can still test partial collection logic by
   waiting, or by manually backdating `LoanDate` in the database for
   testing purposes.
6. **Loan Closure** → enter the loan number, review the closure
   calculation (principal + any accrued interest/penalty), click
   **Close Loan**.
7. Go back to the **Dashboard** and refresh — KPIs should now reflect
   the activity you just performed.

---

## Security notes (read before using this beyond your own machine)

This is a **test/development build**, not a production-hardened system:

- **Password hashing** uses plain SHA-256 with no salt. This is fine for
  local testing but is not how production systems should store
  passwords (they should use a salted, slow hash like bcrypt/Argon2).
- **Auth tokens** are a simple opaque Base64 string with no expiry, no
  signature, no refresh mechanism — not a real JWT. Anyone with the
  token string could replay it. Fine for local testing; not fine for
  anything internet-facing.
- **CORS** is wide open (`SetIsOriginAllowed(_ => true)`) so the static
  frontend can call the API from any origin during testing. Lock this
  down before deploying anywhere shared.
- The connection string as shipped uses **SQL Authentication with a
  plaintext password in a config file**. For anything beyond your own
  test machine, move the password into User Secrets, environment
  variables, or a secrets manager, and never commit it to source
  control.

## Troubleshooting

**"Cannot open database JLMS_DB requested by the login"**
The SQL login you put in the connection string doesn't have access.
Re-run the `CREATE USER` / `ALTER ROLE` commands from Step 1.4 against
the correct login name.

**"A network-related or instance-specific error..."**
Usually means the `Server=` value is wrong, or SQL Server isn't
configured to accept TCP connections / SQL Authentication. In SSMS:
right-click the server → Properties → Security → make sure "SQL Server
and Windows Authentication mode" is selected (requires a SQL Server
service restart after changing).

**Dropdowns are empty (Jewel Type, Loan Scheme) on the New Loan screen**
You skipped the optional reference data script, or your branch/scheme
data was deleted. Add at least one Jewel Type and one Loan Scheme
(directly in SSMS, or build out the Masters screens — the API endpoints
already exist) before trying to create a loan.

**CORS error in the browser console**
Make sure you're opening the HTML file directly (file://) or via a
simple local server, and that the API's `Program.cs` CORS policy
(`AllowFrontendDev`) hasn't been edited to restrict origins.

---

## Folder structure

```
database/
  01_schema.sql                      → run first (creates empty DB + tables)
  02_optional_reference_data.sql     → run second, optional (lookups + 1 admin login)

api/
  JLMS.Api.sln                       → open this in Visual Studio
  JLMS.Api/
    Program.cs                       → startup, CORS, SQL connection wiring
    appsettings.json                 → EDIT THIS with your SQL Server details
    Models/Entities.cs                → EF Core entity classes
    Data/JlmsDbContext.cs             → EF Core DbContext
    DTOs/Dtos.cs                      → request/response shapes
    Services/LoanCalculationService.cs → interest/penalty/LTV math, all in one place
    Controllers/                      → one file per module (Customers, Loans, Collections, Closure, Masters, Dashboard, Auth)

frontend/
  pages/
    login.html, dashboard.html, customer-registration.html, customer-search.html,
    new-loan.html, loan-approval.html, loan-disbursement.html,
    interest-collection.html, loan-closure.html   → wired to the API
    (other .html files)                            → still static demo screens
    js/api-client.js                  → EDIT API_BASE_URL here if your port differs
    js/jlms-shell.js                  → shared sidebar/topbar
    css/jlms-core.css                 → design system
    vendor/                           → Bootstrap, Bootstrap Icons, Chart.js (offline, bundled)
```
