#!/bin/bash
# Script to generate the .sln file when dotnet CLI is available

dotnet new sln -n MOT
dotnet sln add MOT.Domain/MOT.Domain.csproj
dotnet sln add MOT.Application/MOT.Application.csproj
dotnet sln add MOT.Infrastructure/MOT.Infrastructure.csproj
dotnet sln add MOT.Api/MOT.Api.csproj
dotnet sln add MOT.Tests/MOT.Tests.csproj

echo "MOT.sln generated successfully."
