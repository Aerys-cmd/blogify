# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Install Node.js for Tailwind CSS build
RUN apk add --no-cache nodejs npm

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

# Publish with Tailwind build enabled; strip debug symbols to reduce output size
RUN dotnet publish Blogify.Web/Blogify.Web.csproj \
    -c Release \
    -o /app/publish \
    -p:EnableTailwindBuild=true \
    -p:DebugType=none \
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
