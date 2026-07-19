# Contributing

Use .NET 8 or .NET 10 and PHP 8.1+ for deterministic contract/documentation checks.

```bash
dotnet restore VediSMM.sln --locked-mode
php scripts/generate.php
dotnet format VediSMM.sln --verify-no-changes --no-restore
dotnet build VediSMM.sln --configuration Release --no-restore
dotnet test VediSMM.sln --configuration Release --no-restore -- --minimum-expected-tests 15
php scripts/check_docs.php
php scripts/check_secrets.php
```

Generated service and API-reference files must be regenerated from `contract/sdk-operations.json` and committed together. New runtime behavior requires regression tests. Never add administrative endpoints, live credentials, generated build output, or local absolute paths.

By contributing, you agree that your contribution is licensed under MIT.
