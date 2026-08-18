# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy the entire backend directory
COPY . ./

# Restore dependencies
RUN dotnet restore MOT.sln

# Build and publish the app
RUN dotnet publish MOT.Api/MOT.Api.csproj -c Release -o out

# Use the official ASP.NET Core runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .

# Entry point
ENTRYPOINT ["dotnet", "MOT.Api.dll"]
