param appName string
param location string = resourceGroup().location
param appServicePlanName string = '${appName}-plan'
@secure()
param sqlConnectionString string
@secure()
param jwtKey string
param jwtIssuer string
param clientOrigin string
@secure()
param smtpPassword string
param smtpHost string
param smtpUsername string
param smtpFrom string
param clientResetUrl string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      http20Enabled: true
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'Jwt__Key'
          value: jwtKey
        }
        {
          name: 'Jwt__Issuer'
          value: jwtIssuer
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: clientOrigin
        }
        {
          name: 'Email__From'
          value: smtpFrom
        }
        {
          name: 'Email__SmtpHost'
          value: smtpHost
        }
        {
          name: 'Email__Username'
          value: smtpUsername
        }
        {
          name: 'Email__SmtpPassword'
          value: smtpPassword
        }
        {
          name: 'Email__ClientResetUrl'
          value: clientResetUrl
        }
      ]
    }
  }
}

output appUrl string = 'https://${app.properties.defaultHostName}'