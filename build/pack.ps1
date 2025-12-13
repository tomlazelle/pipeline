param(
  [string]$Configuration = "Release"
)

dotnet restore
dotnet build -c $Configuration
dotnet test -c $Configuration
dotnet pack src/GenericPipeline/GenericPipeline.csproj -c $Configuration -o ./artifacts
