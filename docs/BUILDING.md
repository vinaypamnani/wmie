# Building from Source

Instructions for building WMI Explorer from source code.

## Table of Contents

- [Requirements](#requirements)
- [Build Steps](#build-steps)
- [Project Structure](#project-structure)

## Requirements

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows 10/11 or Windows Server 2016+ (for running the application)

## Build Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/vinaypamnani/wmie.git
   cd wmie
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the solution:**
   ```bash
   dotnet build
   ```

4. **Run the application:**
   ```bash
   dotnet run --project WmiExplorer/WmiExplorer.csproj
   OR
   dotnet run --project WmiExplorer -- -debug
   ```

## Project Structure

- `WmiExplorer/`: Main application project
  - `Models/`: WMI data models (classes, instances, properties, methods)
  - `Services/`: Core services (WMI, caching, settings, messaging)
  - `Presentation/`: UI components (ViewModels, Views, Controls)
  - `Common/`: Shared utilities and base classes
  - `Integration/`: Third-party integrations (AvalonEdit, PropertyGrid, Serilog)
- `WmiExplorer.PropertyGrid/`: Custom property grid component
- `WmiExplorer.Tests/`: Unit tests

