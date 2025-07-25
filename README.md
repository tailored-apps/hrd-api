# HRD API .NET Library

This project provides a .NET 8 port of the PHP based HRD API client.
Only a subset of the full PHP API is implemented but most common actions are available.

## Features
- Connects to the HRD API over SSL/TCP.
- Login using login/password.
- Retrieve partner balance information.
- Domain operations (info, create, renew, update, trade, whois, DNS changes).
- Certificate operations (info, create, renew).
- Poll, user and news endpoints.

## Usage
```csharp
using HrdApiNet;

await using var api = new HRDApi();
api.SetHash(Convert.FromHexString("<apiHashHex>"));
await api.LoginAsync("<login>", "<pass>", "partner");
var balance = await api.PartnerGetBalanceAsync();
var domainInfo = await api.DomainInfoAsync("example.com");
```

This library is based on the original PHP implementation found in `src/HRDApi.php`.
