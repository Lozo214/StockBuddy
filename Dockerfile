FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/StockBuddy.Web/StockBuddy.Web.csproj src/StockBuddy.Web/
RUN dotnet restore src/StockBuddy.Web/StockBuddy.Web.csproj
COPY src/StockBuddy.Web/ src/StockBuddy.Web/
RUN dotnet publish src/StockBuddy.Web/StockBuddy.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data && chown -R $APP_UID /data
ENV ConnectionStrings__StockBuddy="Data Source=/data/stockbuddy.db"
ENV ConnectionStrings__Accounts="Data Source=/data/accounts.db"
ENV DataProtection__KeyPath="/data/keys"
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StockBuddy.Web.dll"]
