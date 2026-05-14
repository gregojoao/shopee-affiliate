# Publishing

## NuGet

```bash
dotnet test
dotnet pack -c Release
dotnet nuget push src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.0.1.0.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

Before publishing, replace the placeholder `RepositoryUrl` in `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.

## GitHub

```bash
git init
git add .
git commit -m "Initial Shopee affiliate client"
git branch -M main
git remote add origin https://github.com/<user>/Shopee.Affiliate.git
git push -u origin main
```
