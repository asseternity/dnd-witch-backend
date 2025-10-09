#!/bin/bash
# use PORT env var set by Railway, fall back to 5000 locally
export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-5000}"
dotnet run --project ./dnd-witch-backend.csproj
