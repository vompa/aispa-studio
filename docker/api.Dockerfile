# API für den lokalen Testbetrieb mit Docker (compose.yaml im Hauptordner).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/AispaStudio.Contracts/AispaStudio.Contracts.csproj src/AispaStudio.Contracts/
COPY src/AispaStudio.ServiceDefaults/AispaStudio.ServiceDefaults.csproj src/AispaStudio.ServiceDefaults/
COPY src/AispaStudio.Help/AispaStudio.Help.csproj src/AispaStudio.Help/
COPY src/AispaStudio.Api/AispaStudio.Api.csproj src/AispaStudio.Api/
RUN dotnet restore src/AispaStudio.Api
COPY .editorconfig ./
COPY src/AispaStudio.Contracts/ src/AispaStudio.Contracts/
COPY src/AispaStudio.ServiceDefaults/ src/AispaStudio.ServiceDefaults/
COPY src/AispaStudio.Help/ src/AispaStudio.Help/
# Die Hilfetexte liegen bei der Web-Oberfläche; die API durchsucht sie für den Assistenten.
COPY web/src/help/content/ web/src/help/content/
COPY src/AispaStudio.Api/ src/AispaStudio.Api/
# Das OpenAPI-Dokument liegt fertig im Repository; im Container wird es nicht neu erzeugt.
RUN dotnet publish src/AispaStudio.Api -c Release --no-restore -o /app -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Schlüssel für Anmelde-Cookies bleiben in einem Volume, damit die Windows-App nach einem Neustart angemeldet bleibt.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=build /app .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AispaStudio.Api.dll"]
