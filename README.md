name: RaceDay CI
on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Check for .NET project files
        id: check_project
        run: |
          if find . -name "*.sln" -o -name "*.csproj" | grep -q .; then
            echo "found=true" >> "$GITHUB_OUTPUT"
          else
            echo "found=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Setup .NET
        if: steps.check_project.outputs.found == 'true'
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        if: steps.check_project.outputs.found == 'true'
        run: dotnet restore

      - name: Build
        if: steps.check_project.outputs.found == 'true'
        run: dotnet build --no-restore --configuration Release

      - name: Run tests
        if: steps.check_project.outputs.found == 'true'
        run: dotnet test --no-build --configuration Release --verbosity normal

      - name: No .NET project yet
        if: steps.check_project.outputs.found == 'false'
        run: echo "No .sln or .csproj found in the repo yet — skipping build. This is expected during Part 1 (planning stage only)."
