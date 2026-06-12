# =========================================================
# STAGE 1: BUILD (runs inside docker:dind in Jenkins pod)
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

#  Set working directory
WORKDIR /src

# copy tenant service
COPY apps/services/fund ./apps/services/fund

# copy shared dependencies
COPY shared ./shared

# move into fund directory
WORKDIR /src/apps/services/fund


#  Restore dependencies
RUN dotnet restore Fund.Api/Fund.Api.csproj

#  Publish ONLY API project (YOU CHANGE THIS PER SERVICE)
RUN dotnet publish Fund.Api/Fund.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false



# =========================================================
# STAGE 2: RUNTIME (what gets deployed to EKS)
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0

#  Set runtime directory
WORKDIR /app

#  Copy compiled output from build stage
COPY --from=build /app/publish ./

#  Accept port from Jenkins
ARG SERVICE_PORT=5000

#  Bind to all interfaces (REQUIRED in Kubernetes)
ENV ASPNETCORE_URLS=http://+:${SERVICE_PORT}

#  OPTIONAL (but recommended for Kubernetes debugging)
EXPOSE ${SERVICE_PORT}

#  Run application (YOU change DLL name)
ENTRYPOINT ["dotnet", "Fund.Api.dll"]