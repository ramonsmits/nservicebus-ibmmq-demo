FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config Directory.Packages.props Directory.Build.props ./
COPY src/Acme.Shared/Acme.Shared.csproj src/Acme.Shared/
COPY src/Acme.Sales/Acme.Sales.csproj src/Acme.Sales/
COPY .packages/ .packages/
RUN dotnet restore src/Acme.Sales/Acme.Sales.csproj
COPY .git/ .git/
COPY src/Acme.Shared/ src/Acme.Shared/
COPY src/Acme.Sales/ src/Acme.Sales/
RUN dotnet publish src/Acme.Sales/Acme.Sales.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Acme.Sales.dll"]
