# Stage 1: npm dependency install with pinned Node 20
FROM node:20-alpine AS npm-deps

WORKDIR /src/Blogify.Web
COPY Blogify.Web/package*.json ./
RUN npm ci

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy solution and project files for layer-cache-friendly restore
COPY Blogify.sln global.json ./
COPY Blogify.Web/Blogify.Web.csproj Blogify.Web/
COPY Blogify.ServiceDefaults/Blogify.ServiceDefaults.csproj Blogify.ServiceDefaults/

RUN dotnet restore Blogify.Web/Blogify.Web.csproj

# Copy Node.js runtime from pinned image and pre-built npm packages
COPY --from=npm-deps /usr/local/bin/node /usr/local/bin/node
COPY --from=npm-deps /src/Blogify.Web/node_modules Blogify.Web/node_modules

# Copy remaining source
COPY . .

# Publish with Tailwind build enabled; keep portable debug symbols for production diagnostics
RUN dotnet publish Blogify.Web/Blogify.Web.csproj \
    -c Release \
    -o /app/publish \
    -p:EnableTailwindBuild=true \
    -p:DebugType=portable \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Install ICU libraries to enable full globalization support (required for tr/en cultures)
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Blogify.Web.dll"]
