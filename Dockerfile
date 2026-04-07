# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install Node.js 20 for Tailwind CSS build
RUN apt-get update && apt-get install -y ca-certificates curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy solution and project files for layer-cache-friendly restore
COPY Blogify.sln global.json ./
COPY Blogify.Web/Blogify.Web.csproj Blogify.Web/
COPY Blogify.ServiceDefaults/Blogify.ServiceDefaults.csproj Blogify.ServiceDefaults/

RUN dotnet restore Blogify.Web/Blogify.Web.csproj

# Install npm dependencies for Tailwind
COPY Blogify.Web/package*.json Blogify.Web/
RUN npm install --prefix Blogify.Web

# Copy remaining source
COPY . .

# Publish with Tailwind build enabled
RUN dotnet publish Blogify.Web/Blogify.Web.csproj \
    -c Release \
    -o /app/publish \
    -p:EnableTailwindBuild=true \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Blogify.Web.dll"]
