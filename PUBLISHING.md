# Publishing

The NuGet package metadata lives in `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.

Package ID:

```text
Shopee.Affiliate
```

Current version:

```text
0.2.0
```

## NuGet

1. Run the full validation:

```bash
dotnet test
```

2. Create the release package:

```bash
dotnet pack -c Release
```

The package files are generated in:

```text
src/Shopee.Affiliate/bin/Release/
```

Expected files:

```text
Shopee.Affiliate.0.2.0.nupkg
Shopee.Affiliate.0.2.0.snupkg
```

3. Create a NuGet API key at https://www.nuget.org/account/apikeys with the `Push` scope.

4. Publish the package:

PowerShell:

```powershell
$env:NUGET_API_KEY = "your-api-key"
dotnet nuget push .\src\Shopee.Affiliate\bin\Release\Shopee.Affiliate.0.2.0.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

Bash:

```bash
export NUGET_API_KEY="your-api-key"
dotnet nuget push ./src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.0.2.0.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

Do not commit or share the API key. If the key leaks, revoke it in nuget.org and create a new one.

## Versioning

NuGet does not allow replacing an existing package version. For each new release:

1. Update `<Version>` in `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.
2. Update `<PackageReleaseNotes>` in the same file.
3. Run `dotnet test`.
4. Run `dotnet pack -c Release`.
5. Push the new `.nupkg`.

## GitHub

```bash
git add .
git commit -m "Prepare NuGet package metadata"
git push -u origin main
```
