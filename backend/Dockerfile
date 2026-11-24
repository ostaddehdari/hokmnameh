# Build context MUST be the repository root so .NET links the shared Unity rules source.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore backend/src/Hokmnameh.Api/Hokmnameh.Api.csproj
RUN dotnet publish backend/src/Hokmnameh.Api/Hokmnameh.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# Keep the runtime identity deterministic and non-root across image revisions.
# The application has no writable application directory; Compose provides only /tmp.
USER 10001:10001
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hokmnameh.Api.dll"]
