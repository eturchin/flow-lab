FROM node:22-alpine AS frontend
WORKDIR /workspace/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /workspace
COPY global.json Directory.Build.props FlowLab.slnx ./
COPY src/ ./src/
RUN dotnet restore src/FlowLab.Web/FlowLab.Web.csproj
RUN dotnet publish src/FlowLab.Web/FlowLab.Web.csproj -c Release --no-restore -o /publish /p:UseAppHost=false
COPY --from=frontend /workspace/src/FlowLab.Web/wwwroot/ /publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FlowLab.Web.dll"]
