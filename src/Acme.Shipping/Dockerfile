FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config Directory.Packages.props Directory.Build.props ./
COPY src/Acme.Shared/Acme.Shared.csproj src/Acme.Shared/
COPY src/Acme.Shipping/Acme.Shipping.csproj src/Acme.Shipping/
RUN mkdir -p .packages && dotnet restore src/Acme.Shipping/Acme.Shipping.csproj
COPY .git/ .git/
COPY src/Acme.Shared/ src/Acme.Shared/
COPY src/Acme.Shipping/ src/Acme.Shipping/
RUN dotnet publish src/Acme.Shipping/Acme.Shipping.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-composite
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Acme.Shipping.dll"]
