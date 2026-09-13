# WooCommerce Product Manager

Windows desktop application for managing products on a WooCommerce / WordPress store. Version 1 is being built in phases. **Phase 1** provides the WPF application shell, MVVM structure, configuration, and logging. It does **not** call the WooCommerce REST API yet.

## Requirements

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A WooCommerce store with HTTPS and REST API access (needed from Phase 2 onward)

## How to run

From the repository root:

```powershell
dotnet restore
dotnet build WooCommerceProductManager.slnx
dotnet run --project WooCommerceProductManager\WooCommerceProductManager.csproj
```

Or open `WooCommerceProductManager.slnx` in Visual Studio 2022/2026 or Cursor and start debugging.

## How to configure WooCommerce

The application needs three values:

| Setting | Example |
| --- | --- |
| Store URL | `https://your-store.com/wp-json/wc/v3/` |
| Consumer Key | generated in WooCommerce |
| Consumer Secret | generated in WooCommerce |

Use HTTPS only. Do not use HTTP. Do not hard-code credentials in source files.

### In the application

1. Open **Store connection**.
2. Enter the store URL, consumer key, and consumer secret.
3. Click **Save settings**.

Settings are stored for the current Windows user under:

`%AppData%\WooCommerceProductManager\settings.json`

The consumer key and secret are encrypted with Windows DPAPI (`CurrentUser` scope). This file is outside the Git repository.

### Local development JSON (optional)

For development you may copy:

`WooCommerceProductManager/appsettings.Development.json.example`

to:

`WooCommerceProductManager/appsettings.Development.json`

Then fill in your own values. `appsettings.Development.json` is listed in `.gitignore` and must never be committed.

Committed `appsettings.json` contains only placeholders, not real credentials.

## How to create WooCommerce API credentials

1. Sign in to WordPress as an administrator.
2. Go to **WooCommerce → Settings → Advanced → REST API**.
3. Click **Add key**.
4. Set a description (for example `Product Manager desktop`).
5. Choose the user that should own the key.
6. Set permissions to **Read/Write** (product updates need write access).
7. Click **Generate API key**.
8. Copy the **Consumer key** and **Consumer secret** immediately. WooCommerce will not show the secret again.

The REST API base path is typically:

`https://your-store.com/wp-json/wc/v3/`

Replace `your-store.com` with your real domain. Include the trailing slash. The URL must start with `https://`.

## How to test the connection (Phase 2)

1. Run the application.
2. Open **Store connection**.
3. Enter Store URL, Consumer Key, and Consumer Secret.
4. Click **Save settings** (stores credentials on this PC only).
5. Click **Test Connection**.

The app sends an authenticated `GET` request to the official WooCommerce endpoint `products?per_page=1`. It does not display products yet.

- Success: **Connected successfully**
- Failure: a specific error (invalid credentials, forbidden, wrong URL, timeout, network, or store unavailable)

Optional development file (never commit it):

1. Copy `WooCommerceProductManager/appsettings.Development.json.example` to `appsettings.Development.json`
2. Put your Store URL, Consumer Key, and Consumer Secret there
3. Restart the app so it can load those values, then use **Test Connection**

Logs for failed tests are in `%AppData%\WooCommerceProductManager\logs\`. Credentials and `Authorization` headers are not logged.

## Where local configuration goes

| Location | Purpose | In Git? |
| --- | --- | --- |
| `WooCommerceProductManager/appsettings.json` | Placeholder defaults | Yes (no secrets) |
| `WooCommerceProductManager/appsettings.Development.json` | Optional developer overrides | No |
| `%AppData%\WooCommerceProductManager\settings.json` | Saved UI settings; secrets encrypted | No |
| `%AppData%\WooCommerceProductManager\logs\` | Application log files | No |

Logs record request types, endpoints, HTTP status, and errors. They do **not** record consumer keys, consumer secrets, passwords, or authorization headers.

## How to build

```powershell
dotnet build WooCommerceProductManager.slnx -c Release
```

## How to publish a Windows executable

Framework-dependent (requires .NET 10 Desktop Runtime on the target PC):

```powershell
dotnet publish WooCommerceProductManager\WooCommerceProductManager.csproj -c Release -r win-x64 --self-contained false -o .\publish
```

Self-contained (larger, does not require a preinstalled runtime):

```powershell
dotnet publish WooCommerceProductManager\WooCommerceProductManager.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

The executable is `WooCommerceProductManager.exe` in the publish folder.

## Local SQLite database

The product list, search, and pagination use a local SQLite database. WooCommerce is contacted only for **Test Connection** and **Sync from Website**.

Database file:

`%AppData%\WooCommerceProductManager\products.db`

On startup the app runs EF Core migrations (`Database.Migrate`). It does not delete or recreate the database.

To add a future schema change:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MigrationName --project WooCommerceProductManager\WooCommerceProductManager.csproj --output-dir Data/Migrations
```

## Sync from Website

1. Configure Store URL, Consumer Key, and Consumer Secret.
2. Click **Sync from Website**.
3. The app pages through `GET /products` and upserts into SQLite by `WooCommerceId`.
4. Local rows with `IsDirty = true` are not overwritten (counted as conflicts).
5. **Sync to Website** is disabled until a later phase.

## How to test offline mode

1. Sync from Website while online.
2. Confirm products appear in the list.
3. Disconnect from the internet (or stop the site).
4. Restart the application.
5. The product list, search, and pagination should still work from SQLite.
6. Sync from Website should fail with an error, without deleting local products.

## Current phase (local database + sync from website)

Implemented:

- Local SQLite product catalog (EF Core)
- Product list, search, and pagination against SQLite
- **Sync from Website** with progress counters and dirty-row conflict protection
- Existing WooCommerce REST API client retained for connection tests and sync

Not implemented yet:

- Sync to Website
- Product editing, creation, and deletion
- Variations, categories, attributes, or image upload

## Security notes

- Never commit consumer keys or secrets.
- Never send credentials to any host other than the configured WooCommerce HTTPS URL.
- The application will not delete products in version 1.
