# DevOpsAPI

Uses the Azure Dev Ops API to get info about Builds (and Releases)

appsettings.json needs configuring for this to work.

- Azure AD is used for authorization so config for this goes in AzureAd section
- DevOpsPAT is a PAT from Azure DevOps
- DevOpsURL is the URL to Azure DevOps something like https://dev.azure.com/companyname
- Reload is the number of ms before page reloads (defaults to 20 seconds)
- Application Insight config if you want to use that
