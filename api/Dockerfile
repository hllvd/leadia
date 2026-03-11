# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# ARG to specify which project to publish (Api, MessageWorker, PersistenceWorker)
ARG PROJECT_NAME=Api

# Copy all project files first to leverage Docker layer caching
COPY ["Api/Api.csproj", "Api/"]
COPY ["MessageWorker/MessageWorker.csproj", "MessageWorker/"]
COPY ["PersistenceWorker/PersistenceWorker.csproj", "PersistenceWorker/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# Restore the specific project (and its dependencies)
RUN dotnet restore "${PROJECT_NAME}/${PROJECT_NAME}.csproj"

# Copy the rest of the source code
COPY . .

# Publish the specified project
RUN dotnet publish "${PROJECT_NAME}/${PROJECT_NAME}.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ARG needs to be redefined in the runtime stage if used in ENTRYPOINT
ARG PROJECT_NAME=Api
ENV DLL_NAME=${PROJECT_NAME}.dll

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Create directory for secrets
RUN mkdir -p /app/secrets

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Use a shell form for ENTRYPOINT to allow environment variable expansion
ENTRYPOINT dotnet ${DLL_NAME}
