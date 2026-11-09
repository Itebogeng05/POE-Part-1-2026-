# syntax=docker/dockerfile:1

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached until dependencies change.
COPY RaceDay.Web.csproj ./
RUN dotnet restore RaceDay.Web.csproj

# Now copy the rest of the source and publish a release build.
COPY . ./
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime (small image, no SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# The MVC app only talks to the RaceDay API. Point it at the API with ApiSettings__BaseUrl.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ApiSettings__BaseUrl=http://host.docker.internal:5100/

EXPOSE 8080

COPY --from=build /app/publish ./

# Run as the non-root "app" user that ships with the .NET 8 images.
USER app
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
