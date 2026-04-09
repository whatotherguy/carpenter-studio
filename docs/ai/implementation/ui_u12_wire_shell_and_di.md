# U12 — Wire Presentation Shell as Running Window + Register All Panel VMs in DI

## Context

The app currently launches `CabinetDesigner.App.MainWindow` (a bare 75-line toolbar), while the
fully styled shell lives in `CabinetDesigner.Presentation.MainWindow` and is never used at runtime.
`App.xaml.cs` also never calls `PresentationServiceRegistration.AddPresentationServices()`, so
`CatalogPanelViewModel`, `PropertyInspectorViewModel`, `RunSummaryPanelViewModel`,
`IssuePanelViewModel`, and `StatusBarViewModel` are unregistered — yet `ShellViewModel`'s
constructor requires all of them. Running the styled shell would throw immediately on DI resolution.

## Files to Read First

- `src/CabinetDesigner.App/App.xaml.cs`
- `src/CabinetDesigner.App/App.xaml`
- `src/CabinetDesigner.App/MainWindow.xaml`
- `src/CabinetDesigner.App/MainWindow.xaml.cs`
- `src/CabinetDesigner.App/WpfEditorCanvasHost.cs`
- `src/CabinetDesigner.Presentation/PresentationServiceRegistration.cs`
- `src/CabinetDesigner.Presentation/MainWindow.xaml`
- `src/CabinetDesigner.Presentation/MainWindow.xaml.cs`
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` (IEditorCanvasHost reference)

## Task

### 1. Update `App.xaml.cs` — replace manual DI wiring with `AddPresentationServices()`

In `ConfigureServices`, replace the current block that registers individual VMs with a call to
`services.AddPresentationServices()`.  Keep only the lines that are genuinely App-layer concerns
(persistence, migrations, application services).  The `WpfEditorCanvasHost` registration must stay
here because `PresentationServiceRegistration` registers `EditorCanvasHost` (which is
Presentation-layer only), not the WPF-specific one.

Before:
```csharp
services.AddScoped<EditorSession>();
services.AddScoped<EditorCanvas>();
services.AddScoped<IEditorCanvasHost, WpfEditorCanvasHost>();
services.AddScoped<IEditorCanvasSession, EditorCanvasSessionAdapter>();
services.AddScoped<IHitTester, DefaultHitTester>();
services.AddScoped<ISceneProjector, SceneProjector>();
services.AddScoped<EditorCanvasViewModel>();
services.AddScoped<ShellViewModel>();
services.AddScoped<MainWindow>();
```

After:
```csharp
services.AddPresentationServices();
// Override the canvas host with the WPF-specific implementation
services.AddScoped<IEditorCanvasHost, WpfEditorCanvasHost>();
services.AddScoped<CabinetDesigner.App.MainWindow>();
```

`AddPresentationServices` registers `IEditorCanvasHost` as `EditorCanvasHost`.  The override line
after it replaces that registration with `WpfEditorCanvasHost` (last registration wins in
Microsoft.Extensions.DependencyInjection).

Add the missing `using CabinetDesigner.Presentation;` using directive.

### 2. Update `App.xaml.cs` — resolve `CabinetDesigner.App.MainWindow`

`App.MainWindow` is resolved by `_appScope.ServiceProvider.GetRequiredService<MainWindow>()`.
Because both `CabinetDesigner.App.MainWindow` and `CabinetDesigner.Presentation.MainWindow` exist,
use the fully qualified type in the `GetRequiredService<>` call to avoid ambiguity, or add an alias.

### 3. Switch the startup window

`App.xaml` currently sets `StartupUri` or relies on `OnStartup` showing `CabinetDesigner.App.MainWindow`.
Keep using `CabinetDesigner.App.MainWindow` as the host window **or** switch to
`CabinetDesigner.Presentation.MainWindow`.

The cleanest path is to keep `App.MainWindow` as the registered window but have its XAML host the
`Presentation` shell content.  However the simpler path that matches the review recommendation is:

- Register `CabinetDesigner.Presentation.MainWindow` in the DI container instead of `CabinetDesigner.App.MainWindow`.
- In `OnStartup`, resolve `CabinetDesigner.Presentation.MainWindow` and show it.
- The `App/MainWindow.xaml` and its `.cs` can stay in the project for now (do not delete them), but
  they will no longer be the startup window.

`CabinetDesigner.Presentation.MainWindow` is guarded with `#if WINDOWS` — verify the App project
builds with `WINDOWS` defined (the `.csproj` should have `<DefineConstants>WINDOWS</DefineConstants>`
or `<UseWPF>true</UseWPF>` which implies it; check and add if missing).

### 4. Verify `App.xaml` — remove `StartupUri` if present

If `App.xaml` has `StartupUri="MainWindow.xaml"` it will try to create a second window.  Remove
`StartupUri` entirely since `OnStartup` creates and shows the window manually.

### 5. Add project reference if needed

`CabinetDesigner.App.csproj` may not reference `CabinetDesigner.Presentation`.  Open the `.csproj`
and add a `<ProjectReference>` to `../CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj`
if it is not already present.

## Requirements

- The app must start and show the full styled shell with all panels visible.
- No DI resolution exceptions on startup.
- `CabinetDesigner.App.MainWindow` can stay in the project but must not be the shown window.
- Do not delete any existing files — only modify `App.xaml.cs`, `App.xaml`, and `.csproj` as needed.
- Do not add `StartupUri` if it was not there; do not leave a stale one if it was.

## End State

- What is now usable: Full styled shell with catalog, canvas, property inspector, run summary, issue panel, and status bar.
- What is still placeholder: Mouse interaction, keyboard shortcuts, file dialogs.
- Next prompt: U13 — Fix async/UI thread marshaling in AsyncRelayCommand.
