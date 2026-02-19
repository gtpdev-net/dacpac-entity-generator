# DACPAC Entity Generator - Blazor Server Conversion Plan

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Solution Organization](#solution-organization)
5. [Service Layer Refactoring](#service-layer-refactoring)
6. [State Management](#state-management)
7. [Component Design](#component-design)
8. [UI/UX Design](#uiux-design)
9. [File Management](#file-management)
10. [Progress Tracking](#progress-tracking)
11. [Error Handling](#error-handling)
12. [Implementation Steps](#implementation-steps)
13. [Testing Strategy](#testing-strategy)
14. [Deployment Considerations](#deployment-considerations)

---

## Overview

### Purpose

Convert the existing console application into a **Blazor Server** web application that provides:
- Visual file browser for `_input` and `_output` folders
- File upload capabilities for DACPAC files and Excel worksheets
- Real-time progress tracking during entity generation
- Interactive viewing of generated files
- Download capabilities for individual files or entire output
- Discovery report viewer in the browser

### Key Benefits of Blazor Server

1. **Real-time UI Updates** - SignalR enables live progress tracking
2. **Shared Code** - Reuse existing service layer with minimal changes
3. **Server-Side Execution** - No client-side limitations on file size or processing
4. **Interactive UI** - Better user experience than console
5. **No JavaScript Required** - Pure C# for both server and client logic

### Design Philosophy

- **Minimal Service Changes** - Existing services remain largely unchanged
- **Progressive Enhancement** - Console app remains functional, Blazor adds UI layer
- **Shared Library Approach** - Core logic in separate project, consumed by both console and web
- **Async-First** - All long-running operations use async/await with cancellation support

---

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Blazor Server App                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              User Interface Layer                      │  │
│  │  - Pages (Home, Upload, Generate, Output, Reports)    │  │
│  │  - Components (FileList, ProgressBar, LogViewer)      │  │
│  │  - Layouts (MainLayout)                               │  │
│  └──────────────────┬────────────────────────────────────┘  │
│                     │                                        │
│  ┌──────────────────▼────────────────────────────────────┐  │
│  │           State Management Layer                       │  │
│  │  - AppState (Scoped service)                          │  │
│  │  - Generation orchestrator with progress events       │  │
│  └──────────────────┬────────────────────────────────────┘  │
└────────────────────┬┘                                        │
                     │                                         │
┌────────────────────▼─────────────────────────────────────┐  │
│           DacpacEntityGenerator.Core (Class Library)      │  │
│  ┌───────────────────────────────────────────────────┐   │  │
│  │                   Services                         │   │  │
│  │  - ExcelReaderService                             │   │  │
│  │  - DacpacExtractorService                         │   │  │
│  │  - ModelXmlParserService                          │   │  │
│  │  - EntityClassGenerator                           │   │  │
│  │  - FileWriterService                              │   │  │
│  │  - ReportWriterService                            │   │  │
│  │  - PrimaryKeyEnricher                             │   │  │
│  │  - ILoggerService (abstraction for ConsoleLogger) │   │  │
│  └───────────────────────────────────────────────────┘   │  │
│                                                            │  │
│  ┌───────────────────────────────────────────────────┐   │  │
│  │                   Models                           │   │  │
│  │  - All existing models                            │   │  │
│  └───────────────────────────────────────────────────┘   │  │
│                                                            │  │
│  ┌───────────────────────────────────────────────────┐   │  │
│  │                  Utilities                         │   │  │
│  │  - NameConverter                                  │   │  │
│  │  - SqlTypeMapper                                  │   │  │
│  └───────────────────────────────────────────────────┘   │  │
└────────────────────────────────────────────────────────────┘  │
```

### Technology Stack

- **.NET 8.0** - Target framework
- **Blazor Server** - Interactive server-side rendering with SignalR
- **Bootstrap 5** - Responsive UI framework
- **Blazor.FileUpload** or **InputFile** - File upload component
- **System.IO.Abstractions** (optional) - For better testability of file operations
- **Existing Dependencies**:
  - ClosedXML 0.105.0
  - System.IO.Compression
  - System.Xml.Linq
  - System.Text.Json

---

## Project Structure

### New Solution Structure

```
dacpac-entity-generator/
├── DacpacEntityGenerator.sln (updated)
├── README.md
│
├── src/
│   ├── DacpacEntityGenerator.Core/              # NEW - Class Library
│   │   ├── DacpacEntityGenerator.Core.csproj
│   │   ├── Models/
│   │   │   ├── TableDefinition.cs
│   │   │   ├── ViewDefinition.cs
│   │   │   ├── ColumnDefinition.cs
│   │   │   ├── ForeignKeyDefinition.cs
│   │   │   ├── IndexDefinition.cs
│   │   │   ├── CheckConstraintDefinition.cs
│   │   │   ├── UniqueConstraintDefinition.cs
│   │   │   ├── FunctionDefinition.cs
│   │   │   ├── ElementDiscoveryReport.cs
│   │   │   ├── ExcelRow.cs
│   │   │   ├── GenerationResult.cs
│   │   │   └── GenerationProgress.cs           # NEW
│   │   ├── Services/
│   │   │   ├── Interfaces/
│   │   │   │   ├── ILoggerService.cs           # NEW
│   │   │   │   └─ IGenerationService.cs       # NEW
│   │   │   ├── ExcelReaderService.cs
│   │   │   ├── DacpacExtractorService.cs
│   │   │   ├── ModelXmlParserService.cs
│   │   │   ├── EntityClassGenerator.cs
│   │   │   ├── FileWriterService.cs
│   │   │   ├── ReportWriterService.cs
│   │   │   ├── PrimaryKeyEnricher.cs
│   │   │   └── GenerationOrchestrator.cs        # NEW
│   │   └── Utilities/
│   │       ├── NameConverter.cs
│   │       └── SqlTypeMapper.cs
│   │
│   ├── DacpacEntityGenerator/                    # Console App (existing)
│   │   ├── DacpacEntityGenerator.csproj (updated - references Core)
│   │   ├── Program.cs (refactored - thinner)
│   │   ├── Utilities/
│   │   │   └── ConsoleLoggerAdapter.cs          # NEW - ILoggerService impl
│   │   ├── _input/
│   │   └── _output/
│   │
│   └── DacpacEntityGenerator.Web/                # NEW - Blazor Server
│       ├── DacpacEntityGenerator.Web.csproj
│       ├── Program.cs
│       ├── App.razor
│       ├── _Imports.razor
│       ├── appsettings.json
│       ├── Components/
│       │   ├── Layout/
│       │   │   ├── MainLayout.razor
│       │   │   ├── NavMenu.razor
│       │   │   └── MainLayout.razor.css
│       │   └── Shared/
│       │       ├── FileListComponent.razor       # Display files/folders
│       │       ├── FileUploadComponent.razor     # Upload files
│       │       ├── ProgressComponent.razor       # Progress tracking
│       │       ├── LogViewerComponent.razor      # Live log viewer
│       │       ├── CodeViewerComponent.razor     # Syntax-highlighted code
│       │       └── DiscoveryReportComponent.razor
│       ├── Pages/
│       │   ├── Home.razor                        # Dashboard
│       │   ├── InputFiles.razor                  # Manage input files
│       │   ├── Generate.razor                    # Run generation
│       │   ├── OutputFiles.razor                 # Browse/download output
│       │   ├── Reports.razor                     # View discovery reports
│       │   └── Settings.razor                    # Configuration
│       ├── Services/
│       │   ├── AppState.cs                       # Scoped state management
│       │   └── BlazorLoggerAdapter.cs            # ILoggerService impl
│       └── wwwroot/
│           ├── css/
│           │   ├── app.css
│           │   └── prism.css                     # Syntax highlighting
│           ├── js/
│           │   ├── app.js
│           │   └── prism.js                      # Syntax highlighting
│           └── favicon.ico
```

---

## Solution Organization

### Step 1: Create Core Class Library

**Project Name**: `DacpacEntityGenerator.Core`  
**Type**: Class Library (.NET 8.0)  
**Purpose**: Shared business logic, services, and models

**Move from Console App to Core**:
- All classes in `Models/`
- All classes in `Services/`
- `Utilities/NameConverter.cs`
- `Utilities/SqlTypeMapper.cs`

**Do NOT move**:
- `Utilities/ConsoleLogger.cs` (stays in console app, becomes adapter)
- `Program.cs` (specific to console app)
- `_input/` and `_output/` folders (not code)

### Step 2: Update Console App

**Keep**:
- `Program.cs` (refactored to use Core services)
- `Utilities/ConsoleLogger.cs` (renamed to ConsoleLoggerAdapter)
- `_input/` and `_output/` folders

**Add Reference**:
- Project reference to `DacpacEntityGenerator.Core`

### Step 3: Create Blazor Server App

**Project Name**: `DacpacEntityGenerator.Web`  
**Type**: Blazor Server (.NET 8.0)  
**Add Reference**:
- Project reference to `DacpacEntityGenerator.Core`

---

## Service Layer Refactoring

### Problem: Hard-Coded Console Logging

Current services use `ConsoleLogger.LogInfo()`, `ConsoleLogger.LogError()`, etc. This won't work in Blazor.

### Solution: Logging Abstraction

#### Step 1: Create ILoggerService Interface

**File**: `DacpacEntityGenerator.Core/Services/Interfaces/ILoggerService.cs`

```csharp
namespace DacpacEntityGenerator.Core.Services.Interfaces;

public interface ILoggerService
{
    void LogInfo(string message);
    void LogProgress(string message);
    void LogWarning(string message);
    void LogError(string message);
}
```

#### Step 2: Update All Services

**Pattern**: Constructor injection of `ILoggerService`

**Before** (Example: DacpacExtractorService):
```csharp
public class DacpacExtractorService
{
    public string? ExtractModelXml(string inputDirectory, string server, string database)
    {
        ConsoleLogger.LogInfo($"Extracting model.xml from {server}_{database}.dacpac");
        // ...
    }
}
```

**After**:
```csharp
public class DacpacExtractorService
{
    private readonly ILoggerService _logger;

    public DacpacExtractorService(ILoggerService logger)
    {
        _logger = logger;
    }

    public string? ExtractModelXml(string inputDirectory, string server, string database)
    {
        _logger.LogInfo($"Extracting model.xml from {server}_{database}.dacpac");
        // ...
    }
}
```

**Services to Update**:
1. DacpacExtractorService
2. ExcelReaderService
3. ModelXmlParserService
4. EntityClassGenerator
5. FileWriterService
6. ReportWriterService
7. PrimaryKeyEnricher

#### Step 3: Console App Adapter

**File**: `DacpacEntityGenerator/Utilities/ConsoleLoggerAdapter.cs`

```csharp
using DacpacEntityGenerator.Core.Services.Interfaces;

namespace DacpacEntityGenerator.Utilities;

public class ConsoleLoggerAdapter : ILoggerService
{
    public void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogProgress(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    public void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
    }

    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }
}
```

#### Step 4: Blazor App Adapter

**File**: `DacpacEntityGenerator.Web/Services/BlazorLoggerAdapter.cs`

```csharp
using DacpacEntityGenerator.Core.Services.Interfaces;

namespace DacpacEntityGenerator.Web.Services;

public class BlazorLoggerAdapter : ILoggerService
{
    public event Action<LogEntry>? OnLogReceived;

    public void LogInfo(string message)
    {
        OnLogReceived?.Invoke(new LogEntry(LogLevel.Info, message));
    }

    public void LogProgress(string message)
    {
        OnLogReceived?.Invoke(new LogEntry(LogLevel.Success, message));
    }

    public void LogWarning(string message)
    {
        OnLogReceived?.Invoke(new LogEntry(LogLevel.Warning, message));
    }

    public void LogError(string message)
    {
        OnLogReceived?.Invoke(new LogEntry(LogLevel.Error, message));
    }
}

public record LogEntry(LogLevel Level, string Message, DateTime Timestamp)
{
    public LogEntry(LogLevel Level, string Message) 
        : this(Level, Message, DateTime.Now) { }
}

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}
```

---

## State Management

### AppState Service (Scoped)

**File**: `DacpacEntityGenerator.Web/Services/AppState.cs`

```csharp
using DacpacEntityGenerator.Core.Models;

namespace DacpacEntityGenerator.Web.Services;

public class AppState
{
    private GenerationProgress? _currentProgress;
    private List<LogEntry> _logs = new();
    
    // Events for UI updates
    public event Action? OnStateChanged;
    public event Action<LogEntry>? OnLogAdded;
    public event Action<GenerationProgress>? OnProgressUpdated;
    
    // Properties
    public bool IsGenerating { get; private set; }
    public GenerationProgress? CurrentProgress => _currentProgress;
    public IReadOnlyList<LogEntry> Logs => _logs.AsReadOnly();
    public string InputDirectory { get; set; } = "_input";
    public string OutputDirectory { get; set; } = "_output";
    
    // Methods
    public void StartGeneration()
    {
        IsGenerating = true;
        _logs.Clear();
        _currentProgress = new GenerationProgress();
        OnStateChanged?.Invoke();
    }
    
    public void CompleteGeneration()
    {
        IsGenerating = false;
        OnStateChanged?.Invoke();
    }
    
    public void AddLog(LogEntry entry)
    {
        _logs.Add(entry);
        OnLogAdded?.Invoke(entry);
    }
    
    public void UpdateProgress(GenerationProgress progress)
    {
        _currentProgress = progress;
        OnProgressUpdated?.Invoke(progress);
        OnStateChanged?.Invoke();
    }
    
    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
```

### GenerationProgress Model

**File**: `DacpacEntityGenerator.Core/Models/GenerationProgress.cs`

```csharp
namespace DacpacEntityGenerator.Core.Models;

public class GenerationProgress
{
    public string? CurrentOperation { get; set; }
    public int TotalDatabases { get; set; }
    public int CompletedDatabases { get; set; }
    public int TotalTables { get; set; }
    public int CompletedTables { get; set; }
    public int TotalViews { get; set; }
    public int CompletedViews { get; set; }
    public int EntitiesGenerated { get; set; }
    public int ViewsGenerated { get; set; }
    public int TablesSkipped { get; set; }
    public int ErrorsEncountered { get; set; }
    
    public int OverallPercentage => TotalDatabases > 0 
        ? (CompletedDatabases * 100 / TotalDatabases) 
        : 0;
        
    public int TablePercentage => TotalTables > 0 
        ? (CompletedTables * 100 / TotalTables) 
        : 0;
}
```

---

## Component Design

### 1. FileListComponent.razor

**Purpose**: Display files and folders with actions (view, download, delete)

**Features**:
- Tree view of directories
- File icons by type (.xlsx, .dacpac, .cs, .json, .html)
- File size and modified date
- Actions: View, Download, Delete
- Breadcrumb navigation

**Props**:
- `DirectoryPath` - Root directory to display
- `AllowUpload` - Show upload button
- `AllowDelete` - Show delete buttons
- `OnFileSelected` - Callback when file is clicked

### 2. FileUploadComponent.razor

**Purpose**: Upload DACPAC and Excel files

**Features**:
- Drag-and-drop support
- File type validation (.xlsx, .dacpac)
- Progress bar for upload
- Multiple file selection
- Auto-organize (DACPACs to `/dacpacs/` subfolder)

**Props**:
- `TargetDirectory` - Where to save uploaded files
- `AllowedExtensions` - File filter
- `OnUploadComplete` - Callback after upload

### 3. ProgressComponent.razor

**Purpose**: Real-time progress tracking during generation

**Features**:
- Overall progress bar (databases processed)
- Current operation description
- Table/view progress bar
- Statistics counters
- Elapsed time
- Cancel button

**Props**:
- `Progress` - `GenerationProgress` object
- `OnCancel` - Callback to cancel operation

### 4. LogViewerComponent.razor

**Purpose**: Live scrolling log viewer

**Features**:
- Color-coded log levels (info, success, warning, error)
- Auto-scroll to latest entry
- Filter by log level
- Search/filter logs
- Copy to clipboard
- Clear logs button

**Props**:
- `Logs` - `List<LogEntry>`
- `MaxDisplayCount` - Limit visible entries (performance)

### 5. CodeViewerComponent.razor

**Purpose**: Syntax-highlighted C# code viewer

**Features**:
- Syntax highlighting (using Prism.js or similar)
- Line numbers
- Copy to clipboard
- Download button
- Full-screen mode

**Props**:
- `FilePath` - Path to code file
- `Content` - Optional pre-loaded content
- `Language` - Code language (C#, JSON, HTML)

### 6. DiscoveryReportComponent.razor

**Purpose**: Interactive discovery report viewer

**Features**:
- Summary cards (element type counts)
- Tabs for different element types
- Searchable tables
- Export to JSON/HTML
- Navigate to related tables

**Props**:
- `Report` - `ElementDiscoveryReport` object

---

## UI/UX Design

### Page Layout

#### Home Page (/)

**Purpose**: Dashboard and status overview

**Sections**:
1. **Welcome Card**
   - Application description
   - Quick start guide
   
2. **Status Cards**
   - Input Files count (Excel: X, DACPACs: X)
   - Output Files count
   - Last generation timestamp
   
3. **Quick Actions**
   - Upload Files button → `/input`
   - Generate Entities button → `/generate`
   - View Output button → `/output`

#### Input Files Page (/input)

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Input Files                                          │
│ ┌─────────────────┐                                 │
│ │ Upload Files    │                                 │
│ └─────────────────┘                                 │
├─────────────────────────────────────────────────────┤
│ Excel Files (*.xlsx)                                │
│ ┌───────────────────────────────────────────────┐   │
│ │ 📊 DatabaseMetadata.xlsx      [View][Delete]  │   │
│ └───────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────┤
│ DACPAC Files (dacpacs/*.dacpac)                     │
│ ┌───────────────────────────────────────────────┐   │
│ │ 📦 Server1_Database1.dacpac   [Download][Del] │   │
│ │ 📦 Server1_Database2.dacpac   [Download][Del] │   │
│ │ 📦 Server2_Database1.dacpac   [Download][Del] │   │
│ └───────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

#### Generate Page (/generate)

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Generate Entities                                    │
│                                                      │
│ ┌──────────────────────────────────────────────┐    │
│ │ Settings                                     │    │
│ │ Input Directory:  [_input            ]       │    │
│ │ Output Directory: [_output           ]       │    │
│ │ □ Purge output before generation             │    │
│ └──────────────────────────────────────────────┘    │
│                                                      │
│ ┌─────────────────┐                                 │
│ │ Start Generation│  [Cancel]                       │
│ └─────────────────┘                                 │
│                                                      │
│ ┌──────────────────────────────────────────────┐    │
│ │ Progress                                     │    │
│ │                                              │    │
│ │ Overall: ████████░░░░░░░░░░ 40% (2/5)       │    │
│ │ Tables:  ██████████░░░░░░░░ 60% (12/20)     │    │
│ │                                              │    │
│ │ Current: Processing [Server1].[Database1]   │    │
│ │          Generating Order entity...          │    │
│ │                                              │    │
│ │ Entities: 45 | Views: 12 | Errors: 0        │    │
│ └──────────────────────────────────────────────┘    │
│                                                      │
│ ┌──────────────────────────────────────────────┐    │
│ │ Live Logs                            [Clear] │    │
│ │ ─────────────────────────────────────────    │    │
│ │ [INFO] Starting generation...                │    │
│ │ [SUCCESS] Found Excel file                   │    │
│ │ [INFO] Processing Server1.Database1          │    │
│ │ [SUCCESS] Generated Order entity             │    │
│ │ [WARNING] Table Customer has no PK           │    │
│ └──────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

#### Output Files Page (/output)

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Output Files                     [Download All ZIP] │
│                                                      │
│ 📁 Server1/                                          │
│   📁 Database1/                                      │
│     📄 Order.cs                    [View][Download]  │
│     📄 Customer.cs                 [View][Download]  │
│     📁 Views/                                        │
│       📄 OrderSummaryView.cs       [View][Download]  │
│   📁 Database2/                                      │
│     📄 Product.cs                  [View][Download]  │
│                                                      │
│ 📁 Server2/                                          │
│   📁 Database1/                                      │
│     📄 Invoice.cs                  [View][Download]  │
│                                                      │
│ 📁 Configuration/                                    │
│   📁 Server1/                                        │
│     📁 Database1/                                    │
│       📄 Database1EntityConfiguration.cs [View][DL]  │
│                                                      │
│ 📁 DiscoveryReports/                                 │
│   📄 Server1_Database1_Discovery.json    [View][DL]  │
│   📄 Server1_Database1_Discovery.html    [View][DL]  │
│                                                      │
│ 📄 DbContext.onModelCreating         [View][Download]│
└─────────────────────────────────────────────────────┘
```

#### Reports Page (/reports)

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Discovery Reports                                    │
│                                                      │
│ Select Database:                                     │
│ ┌───────────────────────────────────────────────┐   │
│ │ [Server1 - Database1 ▼]                      │   │
│ └───────────────────────────────────────────────┘   │
│                                                      │
│ ┌───────────────────────────────────────────────┐   │
│ │ Summary                                       │   │
│ │                                               │   │
│ │ ┌──────────┐ ┌──────────┐ ┌──────────┐       │   │
│ │ │   125    │ │    42    │ │    18    │       │   │
│ │ │  Tables  │ │  Views   │ │ Stored   │       │   │
│ │ │          │ │          │ │   Procs  │       │   │
│ │ └──────────┘ └──────────┘ └──────────┘       │   │
│ └───────────────────────────────────────────────┘   │
│                                                      │
│ Tabs: [Stored Procedures] [Sequences] [Triggers]    │
│       [Extended Properties] [Spatial Types]          │
│                                                      │
│ ┌───────────────────────────────────────────────┐   │
│ │ Stored Procedures (42 found)                  │   │
│ │ ╔════════════════════════════════════════╗    │   │
│ │ ║ Name                    │ Location     ║    │   │
│ │ ╠════════════════════════════════════════╣    │   │
│ │ ║ dbo.uspGetOrders        │ [dbo]       ║    │   │
│ │ ║ dbo.uspCreateCustomer   │ [dbo]       ║    │   │
│ │ ╚════════════════════════════════════════╝    │   │
│ └───────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### Color Scheme

**Log Levels**:
- Info: Gray/Default
- Success: Green
- Warning: Orange/Yellow
- Error: Red

**Status Indicators**:
- Pending: Gray
- In Progress: Blue
- Success: Green
- Error: Red

---

## File Management

### File Upload Implementation

**Service**: Built-in `InputFile` component (Blazor)

**Process**:
1. User selects files (drag-drop or file picker)
2. Validate file extensions and size
3. Read file stream
4. Save to `_input/` or `_input/dacpacs/`
5. Update file list UI
6. Show success notification

**Code Example**:
```csharp
private async Task HandleFileUpload(InputFileChangeEventArgs e)
{
    foreach (var file in e.GetMultipleFiles())
    {
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        
        if (ext != ".xlsx" && ext != ".dacpac")
        {
            _appState.AddLog(new LogEntry(LogLevel.Warning, 
                $"Skipped {file.Name} - unsupported file type"));
            continue;
        }
        
        var targetDir = ext == ".dacpac" 
            ? Path.Combine(_appState.InputDirectory, "dacpacs")
            : _appState.InputDirectory;
            
        Directory.CreateDirectory(targetDir);
        
        var targetPath = Path.Combine(targetDir, file.Name);
        
        await using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB
        await using var fs = File.Create(targetPath);
        await stream.CopyToAsync(fs);
        
        _appState.AddLog(new LogEntry(LogLevel.Success, 
            $"Uploaded {file.Name}"));
    }
    
    StateHasChanged();
}
```

### File Download Implementation

**Single File Download**:
```csharp
private async Task DownloadFile(string filePath)
{
    var fileName = Path.GetFileName(filePath);
    var fileBytes = await File.ReadAllBytesAsync(filePath);
    var base64 = Convert.ToBase64String(fileBytes);
    
    await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64);
}
```

**JavaScript Helper** (wwwroot/js/app.js):
```javascript
window.downloadFile = (filename, base64) => {
    const link = document.createElement('a');
    link.download = filename;
    link.href = `data:application/octet-stream;base64,${base64}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
```

**Zip All Files**:
```csharp
private async Task DownloadAllAsZip()
{
    var zipPath = Path.Combine(Path.GetTempPath(), "output.zip");
    
    if (File.Exists(zipPath))
        File.Delete(zipPath);
        
    ZipFile.CreateFromDirectory(_appState.OutputDirectory, zipPath);
    
    var fileBytes = await File.ReadAllBytesAsync(zipPath);
    var base64 = Convert.ToBase64String(fileBytes);
    
    await JSRuntime.InvokeVoidAsync("downloadFile", "output.zip", base64);
    
    File.Delete(zipPath);
}
```

### File Viewing Implementation

**Code Files (.cs)**:
- Load content into string
- Pass to CodeViewerComponent
- Render with syntax highlighting (Prism.js)

**JSON Files**:
- Parse and pretty-print
- Or render as syntax-highlighted JSON

**HTML Files**:
- Load and render in iframe or embedded div
- Sandbox for security

---

## Progress Tracking

### Generation Orchestrator Service

**File**: `DacpacEntityGenerator.Core/Services/GenerationOrchestrator.cs`

**Purpose**: Coordinate generation workflow with progress events

```csharp
namespace DacpacEntityGenerator.Core.Services;

public class GenerationOrchestrator
{
    private readonly ExcelReaderService _excelReader;
    private readonly DacpacExtractorService _dacpacExtractor;
    private readonly ModelXmlParserService _modelXmlParser;
    private readonly EntityClassGenerator _entityGenerator;
    private readonly FileWriterService _fileWriter;
    private readonly ReportWriterService _reportWriter;
    private readonly PrimaryKeyEnricher _pkEnricher;
    private readonly ILoggerService _logger;
    
    public event Action<GenerationProgress>? OnProgressChanged;
    
    public GenerationOrchestrator(/* inject all services */)
    {
        // ...
    }
    
    public async Task<GenerationResult> GenerateAsync(
        string inputDirectory, 
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var progress = new GenerationProgress();
        
        // Step 1: Find Excel file
        progress.CurrentOperation = "Finding Excel file...";
        OnProgressChanged?.Invoke(progress);
        
        var excelPath = _excelReader.FindExcelFile(inputDirectory);
        // ...
        
        // Step 2: Read Excel
        progress.CurrentOperation = "Reading Excel file...";
        OnProgressChanged?.Invoke(progress);
        
        var rows = _excelReader.ReadAndFilterExcel(excelPath);
        // ...
        
        // Step 3: Group by database
        var grouped = _excelReader.GroupByServerAndDatabase(rows);
        progress.TotalDatabases = grouped.Sum(g => g.Value.Count);
        OnProgressChanged?.Invoke(progress);
        
        // Step 4: Process each database
        foreach (var serverGroup in grouped)
        {
            foreach (var dbGroup in serverGroup.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                progress.CurrentOperation = $"Processing {serverGroup.Key}.{dbGroup.Key}";
                OnProgressChanged?.Invoke(progress);
                
                // Process database...
                
                progress.CompletedDatabases++;
                OnProgressChanged?.Invoke(progress);
                
                // Yield to UI thread
                await Task.Yield();
            }
        }
        
        return result;
    }
}
```

### Component Progress Binding

**Generate.razor**:
```csharp
@inject GenerationOrchestrator Orchestrator
@inject AppState AppState

<ProgressComponent Progress="@AppState.CurrentProgress" OnCancel="CancelGeneration" />

@code {
    private CancellationTokenSource? _cts;
    
    private async Task StartGeneration()
    {
        _cts = new CancellationTokenSource();
        AppState.StartGeneration();
        
        Orchestrator.OnProgressChanged += HandleProgressChanged;
        
        try
        {
            var result = await Orchestrator.GenerateAsync(
                AppState.InputDirectory,
                AppState.OutputDirectory,
                _cts.Token);
                
            AppState.AddLog(new LogEntry(LogLevel.Success, 
                $"Generation complete: {result.EntitiesGenerated} entities generated"));
        }
        catch (OperationCanceledException)
        {
            AppState.AddLog(new LogEntry(LogLevel.Warning, "Generation cancelled"));
        }
        finally
        {
            Orchestrator.OnProgressChanged -= HandleProgressChanged;
            AppState.CompleteGeneration();
        }
    }
    
    private void HandleProgressChanged(GenerationProgress progress)
    {
        AppState.UpdateProgress(progress);
        InvokeAsync(StateHasChanged);
    }
    
    private void CancelGeneration()
    {
        _cts?.Cancel();
    }
}
```

---

## Error Handling

### Strategy

1. **Service-Level Errors**
   - Try-catch in each service method
   - Log error via ILoggerService
   - Return null or default value
   - Set error flag in result

2. **UI-Level Errors**
   - Display error toast/notification
   - Log to LogViewer
   - Don't crash entire app
   - Provide "Retry" option

3. **File Upload Errors**
   - Validate before upload
   - Show clear error messages
   - Limit file size (100MB default)
   - Check for duplicate files

4. **Generation Errors**
   - Continue processing other tables on individual failures
   - Track errors in GenerationResult
   - Display detailed error summary at completion

### Error Notification Component

```csharp
@if (HasErrors)
{
    <div class="alert alert-danger alert-dismissible">
        <strong>Error:</strong> @ErrorMessage
        <button type="button" class="btn-close" @onclick="ClearError"></button>
    </div>
}

@code {
    [Parameter] public string? ErrorMessage { get; set; }
    
    private bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);
    
    private void ClearError() => ErrorMessage = null;
}
```

---

## Implementation Steps

### Phase 1: Core Library Setup (Week 1)

**Tasks**:
1. Create `DacpacEntityGenerator.Core` class library project
2. Move Models, Services, Utilities from console app to Core
3. Create `ILoggerService` interface
4. Update all services to use `ILoggerService` (constructor injection)
5. Create `GenerationProgress` model
6. Create `GenerationOrchestrator` service with progress events
7. Update console app to reference Core
8. Create `ConsoleLoggerAdapter` in console app
9. Refactor console `Program.cs` to use orchestrator
10. Test console app still works

**Success Criteria**:
- ✅ Console app runs and generates entities
- ✅ All tests pass
- ✅ No console-specific code in Core library

### Phase 2: Blazor Project Structure (Week 1-2)

**Tasks**:
1. Create `DacpacEntityGenerator.Web` Blazor Server project
2. Add project reference to Core
3. Set up basic navigation (NavMenu)
4. Create MainLayout
5. Create placeholder pages (Home, Input, Generate, Output, Reports)
6. Create `BlazorLoggerAdapter` service
7. Create `AppState` service
8. Register services in `Program.cs`
9. Set up dependency injection
10. Configure wwwroot/css for styling

**Success Criteria**:
- ✅ Blazor app runs
- ✅ Navigation works between pages
- ✅ Services are injected correctly

### Phase 3: File Management UI (Week 2)

**Tasks**:
1. Create `FileListComponent.razor`
   - Scan directory and list files
   - Display file icons, sizes, dates
   - Implement download functionality
   - Implement delete functionality
2. Create `FileUploadComponent.razor`
   - Drag-drop file upload
   - Validate file types
   - Progress indicator
   - Save to appropriate directory
3. Build Input Files page
   - Integrate FileListComponent (Excel files)
   - Integrate FileListComponent (DACPAC files)
   - Integrate FileUploadComponent
4. Build Output Files page
   - Integrate FileListComponent with tree view
   - Add "Download All as ZIP" button
   - Add file preview/view action

**Success Criteria**:
- ✅ Can upload Excel and DACPAC files
- ✅ Files are saved to correct directories
- ✅ Can view, download, and delete files
- ✅ Output directory shows nested folder structure

### Phase 4: Generation Workflow UI (Week 3)

**Tasks**:
1. Create `ProgressComponent.razor`
   - Overall progress bar
   - Table/view progress bar
   - Statistics display
   - Cancel button
2. Create `LogViewerComponent.razor`
   - Auto-scrolling log view
   - Color-coded log levels
   - Filter by level
   - Clear logs button
3. Build Generate page
   - Input/output directory settings
   - Start/Cancel buttons
   - Integrate ProgressComponent
   - Integrate LogViewerComponent
   - Wire up GenerationOrchestrator
   - Handle cancellation
4. Implement real-time updates
   - Subscribe to progress events
   - Subscribe to log events
   - Use `InvokeAsync(StateHasChanged)`

**Success Criteria**:
- ✅ Can start generation from UI
- ✅ Progress updates in real-time
- ✅ Logs appear as generation runs
- ✅ Can cancel generation mid-process
- ✅ Success/error messages displayed

### Phase 5: Output Viewing (Week 3-4)

**Tasks**:
1. Create `CodeViewerComponent.razor`
   - Load C# file content
   - Syntax highlighting (Prism.js)
   - Line numbers
   - Copy to clipboard
   - Download button
2. Integrate Prism.js
   - Add to wwwroot/js/
   - Add CSS to wwwroot/css/
   - Initialize on component load
3. Add file preview to Output page
   - Click file to view
   - Modal or side panel for code viewer
   - Support .cs, .json files
4. Add HTML report viewer
   - Load discovery HTML reports
   - Render in iframe or embedded div

**Success Criteria**:
- ✅ Can view generated C# files with syntax highlighting
- ✅ Can view JSON reports
- ✅ Can view HTML discovery reports in browser
- ✅ Copy and download work

### Phase 6: Discovery Reports UI (Week 4)

**Tasks**:
1. Create `DiscoveryReportComponent.razor`
   - Load JSON report
   - Display summary cards
   - Tabbed view for element types
   - Searchable tables
2. Build Reports page
   - Database selector dropdown
   - Integrate DiscoveryReportComponent
   - Export to JSON/HTML buttons
3. Add visual elements
   - Charts for element counts (optional: Chart.js)
   - Highlight unhandled element types

**Success Criteria**:
- ✅ Discovery reports load and display
- ✅ Can switch between databases
- ✅ Element details are searchable
- ✅ Export functionality works

### Phase 7: Polish & Testing (Week 4-5)

**Tasks**:
1. Add loading indicators for async operations
2. Add toast notifications for user actions
3. Add confirmation dialogs (delete files, purge output)
4. Improve responsive design (mobile support)
5. Add keyboard shortcuts
6. Add help/documentation page
7. Performance testing with large files
8. Error handling testing
9. Cross-browser testing
10. Write user documentation

**Success Criteria**:
- ✅ UI is polished and professional
- ✅ All user actions have feedback
- ✅ Works on mobile devices
- ✅ No major bugs or crashes
- ✅ Documentation is complete

### Phase 8: Deployment (Week 5)

**Tasks**:
1. Configure production settings
2. Set up authentication (if needed)
3. Set up file storage limits
4. Configure IIS/Kestrel
5. Create deployment documentation
6. Set up logging and monitoring
7. Create Docker container (optional)
8. Deploy to staging environment
9. User acceptance testing
10. Deploy to production

**Success Criteria**:
- ✅ App runs in production environment
- ✅ File uploads work in production
- ✅ Performance is acceptable
- ✅ Deployment process is documented

---

## Testing Strategy

### Unit Tests

**Create**: `DacpacEntityGenerator.Core.Tests` project

**Test Coverage**:
1. **Services** - Mock ILoggerService, test each method
2. **Utilities** - NameConverter, SqlTypeMapper
3. **GenerationOrchestrator** - Mock all dependencies, verify progress events

### Integration Tests

**Create**: `DacpacEntityGenerator.Web.Tests` project

**Test Coverage**:
1. **Component Rendering** - Use bUnit library
2. **File Upload** - Test with mock files
3. **Generation Workflow** - End-to-end test with sample files
4. **State Management** - Verify AppState events

### Manual Testing Checklist

- [ ] Upload Excel file
- [ ] Upload multiple DACPAC files
- [ ] Start generation and observe progress
- [ ] Cancel generation mid-process
- [ ] View generated C# files
- [ ] View discovery reports
- [ ] Download individual files
- [ ] Download all files as ZIP
- [ ] Delete input files
- [ ] Generate entities multiple times
- [ ] Handle invalid Excel file
- [ ] Handle missing DACPAC
- [ ] Handle corrupted DACPAC
- [ ] Test with large DACPAC (100+ tables)

---

## Deployment Considerations

### Hosting Options

1. **IIS** (Windows Server)
   - Traditional ASP.NET hosting
   - Requires .NET 8 Runtime
   - Configure application pool

2. **Kestrel** (Cross-platform)
   - Self-hosted
   - Systemd service on Linux
   - Nginx reverse proxy

3. **Docker**
   - Containerized deployment
   - Easy scaling
   - Cross-platform

4. **Azure App Service**
   - Managed hosting
   - Easy deployment
   - Built-in scaling

### Configuration

**appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AppSettings": {
    "InputDirectory": "_input",
    "OutputDirectory": "_output",
    "MaxFileUploadSizeMB": 100,
    "EnableAuthentication": false
  }
}
```

### Security Considerations

1. **File Upload Limits** - Prevent denial of service
2. **Path Traversal** - Validate file paths
3. **Authentication** - Consider adding login if deployed externally
4. **HTTPS** - Enforce HTTPS in production
5. **CORS** - Configure if needed for API access

### Performance Considerations

1. **Large Files** - Stream files instead of loading into memory
2. **Concurrent Requests** - Limit simultaneous generations
3. **SignalR Limits** - Configure message size and buffer limits
4. **File Cleanup** - Periodic cleanup of old output files

---

## Success Criteria

### Functional Requirements

- ✅ Upload Excel and DACPAC files through web interface
- ✅ View list of input files with actions (view, delete)
- ✅ Start entity generation from web interface
- ✅ View real-time progress during generation
- ✅ View live logs during generation
- ✅ Cancel generation mid-process
- ✅ Browse generated output files in tree view
- ✅ View generated C# files with syntax highlighting
- ✅ Download individual files
- ✅ Download all output as ZIP
- ✅ View discovery reports in browser
- ✅ Search and filter discovery report elements

### Non-Functional Requirements

- ✅ UI is responsive and works on mobile devices
- ✅ Generation of 100+ tables completes in reasonable time (<5 minutes)
- ✅ Progress updates every second during generation
- ✅ No memory leaks during long-running operations
- ✅ Handles 100MB DACPAC files without errors
- ✅ Console app remains functional (backward compatibility)
- ✅ Code is well-documented
- ✅ User documentation is complete

---

## Risks and Mitigations

### Risk: SignalR Connection Limits

**Description**: Blazor Server uses SignalR for real-time updates. Long-running operations may hit connection limits.

**Mitigation**:
- Configure SignalR message buffer size
- Yield control periodically during generation
- Use `Task.Yield()` in orchestrator loops

### Risk: Memory Usage with Large Files

**Description**: Loading large DACPAC files and Excel files into memory may cause issues.

**Mitigation**:
- Stream file uploads instead of loading entirely
- Use streaming XML parsing for large model.xml
- Dispose of streams properly
- Monitor memory usage in production

### Risk: Concurrent Execution

**Description**: Multiple users starting generation simultaneously could cause file conflicts.

**Mitigation**:
- Use user-specific input/output directories
- Implement locking mechanism
- Or limit to single user (if internal tool)

### Risk: Breaking Console App

**Description**: Refactoring might break existing console app functionality.

**Mitigation**:
- Keep console app tests passing throughout
- Maintain backward compatibility
- Test console app after each phase

---

## Future Enhancements (Out of Scope)

1. **Database Connection** - Connect directly to SQL Server instead of using DACPAC
2. **Git Integration** - Commit generated entities to repository
3. **Diff Viewer** - Compare changes between generations
4. **Template Customization** - Allow users to customize entity templates
5. **Batch Processing** - Queue multiple generation jobs
6. **API Endpoint** - REST API for automation
7. **Multi-Tenant** - Support multiple users with isolated workspaces
8. **Entity Preview** - Preview entities before generating
9. **Undo/Rollback** - Restore previous generation
10. **Notifications** - Email or webhook on completion

---

## Appendix: Key Code Patterns

### Service Registration (Program.cs)

```csharp
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ILoggerService, BlazorLoggerAdapter>();
builder.Services.AddScoped<ExcelReaderService>();
builder.Services.AddScoped<DacpacExtractorService>();
builder.Services.AddScoped<ModelXmlParserService>();
builder.Services.AddScoped<EntityClassGenerator>();
builder.Services.AddScoped<FileWriterService>();
builder.Services.AddScoped<ReportWriterService>();
builder.Services.AddScoped<PrimaryKeyEnricher>();
builder.Services.AddScoped<GenerationOrchestrator>();
```

### Component State Updates

```csharp
protected override void OnInitialized()
{
    AppState.OnStateChanged += StateHasChanged;
}

public void Dispose()
{
    AppState.OnStateChanged -= StateHasChanged;
}
```

### Async File Operations

```csharp
private async Task<string> ReadFileAsync(string path)
{
    await using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}
```

---

## Timeline Summary

| Phase | Duration | Deliverables |
|-------|----------|-------------|
| 1. Core Library | 3-5 days | Core project, refactored services, working console app |
| 2. Blazor Setup | 2-3 days | Blazor project, navigation, services |
| 3. File Management | 3-5 days | Upload, download, view files |
| 4. Generation UI | 4-6 days | Start generation, progress tracking, logs |
| 5. Output Viewing | 3-4 days | Code viewer, syntax highlighting |
| 6. Discovery Reports | 2-3 days | Report viewer component |
| 7. Polish & Testing | 5-7 days | Bug fixes, UX improvements, testing |
| 8. Deployment | 2-3 days | Production config, deployment |
| **Total** | **24-36 days** | **Fully functional Blazor web application** |

---

## Conclusion

This plan converts the console application to a modern Blazor Server web application while preserving all existing functionality. The key strategy is:

1. **Minimal disruption** to existing code by creating a shared Core library
2. **Progressive implementation** with testable milestones
3. **Enhanced user experience** through real-time updates and interactive UI
4. **Maintainability** by keeping business logic separate from UI

The result will be a professional web application that provides a superior user experience compared to the console app, while maintaining all the powerful entity generation capabilities.
