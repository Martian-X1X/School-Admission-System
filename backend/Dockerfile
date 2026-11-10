FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
RUN dotnet restore src/SchoolAdmission.Api/SchoolAdmission.Api.csproj
COPY src/ src/
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
