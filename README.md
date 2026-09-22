# GeoScenery

GeoScenery is an Ionic/Capacitor client backed by an ASP.NET Core API and SQL database.

## Local validation

```powershell
dotnet test GeoScenery.Tests
cd ClientApp
npm ci
npm test -- --watch=false --browsers=ChromeHeadless
npm run build -- --configuration production
```

## Azure deployment

The repository includes:

- `infra/main.bicep` for an HTTPS-only Linux App Service with a system-assigned identity and `/health/ready` health checks.
- `.github/workflows/ci.yml` for backend tests and production frontend builds.
- `.github/workflows/deploy-api.yml` for a manual API deployment using GitHub OIDC.
- `.github/workflows/migrate-production.yml` for a manual EF Core migration run.

Configure the production App Service settings from `GeoScenery.Api/appsettings.Production.template.json`. Store database, JWT, and SMTP secrets in Azure App Service configuration or GitHub environment secrets; do not commit them.

The production client must be built with the deployed API URL in `ClientApp/src/environments/environment.prod.ts`.