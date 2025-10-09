# Use ASP.NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Use .NET SDK image for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["dnd-witch-backend.csproj", "./"]
RUN dotnet restore "dnd-witch-backend.csproj"

# Copy all source files into build stage
COPY . .

# Build and publish
WORKDIR "/src"
RUN dotnet build "dnd-witch-backend.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "dnd-witch-backend.csproj" -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Ensure the data folder from the build context is present at runtime
COPY --from=build /src/data ./data

# Railway sets PORT automatically
ENV ASPNETCORE_URLS=http://+:${PORT:-80}
ENTRYPOINT ["dotnet", "dnd-witch-backend.dll"]
