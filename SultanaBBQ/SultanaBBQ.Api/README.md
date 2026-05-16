# SultanaBBQ.Api

Deze API verwerkt reserveringen voor de Blazor-site:

- `POST /api/reservations` slaat de reservering op in Postgres.
- Daarna verstuurt de API een bevestigingsmail naar de gast.
- De API stuurt ook een melding naar `bilal.zambib4@gmail.com`.

## Lokale configuratie

Zet geheimen lokaal via user-secrets, zodat wachtwoorden niet in git komen:

```bash
dotnet user-secrets set --project SultanaBBQ.Api "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=sultanabbq;Username=postgres;Password=your_password"
dotnet user-secrets set --project SultanaBBQ.Api "Email:Password" "your_gmail_app_password"
dotnet user-secrets set --project SultanaBBQ.Api "Owner:AccessCode" "your_owner_code"
```

Optioneel kun je ook deze waarden overschrijven:

```bash
dotnet user-secrets set --project SultanaBBQ.Api "Email:Username" "bilal.zambib4@gmail.com"
dotnet user-secrets set --project SultanaBBQ.Api "Email:FromAddress" "bilal.zambib4@gmail.com"
dotnet user-secrets set --project SultanaBBQ.Api "Email:RestaurantNotificationAddress" "bilal.zambib4@gmail.com"
```

Voor Gmail heb je een app-wachtwoord nodig. Je gewone Gmail-wachtwoord hoort hier niet gebruikt te worden.

## Starten

```bash
dotnet run --project SultanaBBQ.Api
```

Open daarna `http://localhost:5170/reserveren`.

De eigenaarspagina staat op `http://localhost:5170/eigenaar/reserveringen`.
