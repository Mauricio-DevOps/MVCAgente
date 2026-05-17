# Atendente WhatsApp MVC

Main MVC panel for the WhatsApp attendant.

## Required configuration

Configure these values outside source control:

- `Api__BaseUrl`
- `ExternalLinks__RestaurantAdminBaseUrl`
- `Sso__SigningKey`
- `InternalApi__ServiceKey`

`Sso__SigningKey` must be identical to the value used by `Restaurantes.Web`.
`InternalApi__ServiceKey` must match the API and `Restaurantes.Web`.

## Run locally

```powershell
dotnet run --project Mvc.csproj --urls http://localhost:5105
```
