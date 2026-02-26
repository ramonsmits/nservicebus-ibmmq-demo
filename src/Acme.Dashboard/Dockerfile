FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config Directory.Packages.props Directory.Build.props ./
COPY src/Acme.Shared/Acme.Shared.csproj src/Acme.Shared/
COPY src/Acme.Dashboard/Acme.Dashboard.csproj src/Acme.Dashboard/
RUN mkdir -p .packages && dotnet restore src/Acme.Dashboard/Acme.Dashboard.csproj
COPY .git/ .git/
COPY src/Acme.Shared/ src/Acme.Shared/
COPY src/Acme.Dashboard/ src/Acme.Dashboard/
RUN dotnet publish src/Acme.Dashboard/Acme.Dashboard.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Acme.Dashboard.dll"]
