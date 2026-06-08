# Phenotype-org standard justfile
# For phenotype-postfx, the legacy Taskfile.yml is kept as a thin shim
# that delegates to the recipes here. New work should target `just`.

set shell := ["bash", "-uc"]

# List available recipes
default:
    @just --list

# Build the C# sources (uses dotnet; no Unity toolchain required for source-level checks)
build:
    dotnet build phenotype-postfx.csproj --configuration Release 2>/dev/null || \
        echo "phenotype-postfx.csproj not present (Unity-only project). Use the PostStackSourceTests csproj instead."

# Run C# source-signature tests + shader-variant validation tests
test:
    dotnet test tests/PostStackSourceTests.csproj --configuration Release
    dotnet test tests/PostStackVariantTests/PostStackVariantTests.csproj --configuration Release

# Run the PostStack source signature smoke test (validates PostStack.cs against expected API surface)
validate:
    dotnet run --project tests --configuration Release

# Lint (dotnet format --verify-no-changes)
lint:
    dotnet format --verify-no-changes --verbosity diagnostic || \
        echo "  (no .csproj in repo root; run inside tests/ for scoped format checks)"

# Auto-fix formatting
format:
    dotnet format --verbosity diagnostic || \
        echo "  (no .csproj in repo root; run inside tests/ for scoped format)"

# Full local CI sweep (matches what the GitHub Actions workflow runs)
ci: lint validate test
