# Play.Inventory.Contracts
Common library used by Play Economy services.

## Create and publish package
```powershell
$version="1.0.1"
$owner="samphamdotnetmicroservices02"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Inventory.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Inventory -o ../packages

dotnet nuget push ..\packages\Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

```mac
version="1.0.1"
owner="samphamdotnetmicroservices02"
gh_pat="[PAT HERE]"

dotnet pack src/Play.Inventory.Contracts/ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Inventory -o ../packages

dotnet nuget push ../packages/Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```