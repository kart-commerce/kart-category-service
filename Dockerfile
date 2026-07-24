FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartCategoryService.sln Directory.Build.props ./
COPY src/Api/KartCategoryService.Api.csproj src/Api/
COPY src/Application/KartCategoryService.Application.csproj src/Application/
COPY src/Domain/KartCategoryService.Domain.csproj src/Domain/
COPY src/Infrastructure/KartCategoryService.Infrastructure.csproj src/Infrastructure/
RUN dotnet restore src/Api/KartCategoryService.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/KartCategoryService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KartCategoryService.Api.dll"]
