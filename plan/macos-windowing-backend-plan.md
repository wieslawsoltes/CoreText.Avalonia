# MacOS.Avalonia Windowing Backend Plan

## Goal

- Ship `MacOS.Avalonia` as a standalone `net10.0-macos` AppKit-based windowing backend.
- Keep `CoreText.Avalonia` off `Avalonia.Native` for window creation and surface ownership.
- Reach feature parity with the practical desktop surface of Avalonia's existing macOS backends while preserving direct Metal/OpenGL/native-handle interop.

## Current Status

### Completed

- standalone `MacOS.Avalonia` project, package metadata, and `UseMacOS(...)` bootstrap
- `NSApplication` initialization, delegate wiring, dispatcher integration, activation/background lifetime bridge
- standard `NSWindow` and `NSView` ownership for top-levels
- Metal, OpenGL, software framebuffer, and native-handle platform surfaces
- `CoreText.Avalonia.Native` switched from `UseAvaloniaNative(...)` to `UseMacOS(...)`
- sample bootstrap switched to `UseMacOS(...)`
- screen enumeration, theme variant probing, cursor factory, icon loader, hotkeys, key gesture registration, compositor/render-loop registration
- startup fix for bundled app-host launches via explicit `NSApplication.Init()`

### Added In This Pass

- native drop-target routing from AppKit `NSDraggingDestination` into Avalonia `RawDragEvent` handling for pasteboard-backed text, file, bitmap, and custom payloads
- native drag-source wiring through `IPlatformDragSource` and AppKit `BeginDraggingSession(...)` for pasteboard-backed text, file, bitmap, and custom payloads
- sample drag/drop lab for outbound text and file drags plus inbound file and text drops, intended for Finder/TextEdit-style runtime validation
- payload-aware drag preview image generation instead of the previous placeholder drag badge
- file drag payload polish for macOS destinations through companion `public.url-name` data and real file-path preview icons when the source is a local file
- lightweight embeddable window/top-level support through a shared `NSView`-backed `MacOSEmbeddableWindowImpl`
- embeddable top-level host attachment via `SetParent(...)`, with stable client-frame and child-view origin updates inside parent macOS views
- `MacOSView` lifecycle hardening for tracking areas, mouse-move delivery in host windows, and backing-scale refresh when views move between superviews/windows
- CoreText render-context external IOSurface image import via `IExternalObjectsRenderInterfaceContextFeature`, including safe handle wrapping and CPU snapshot import for Metal-backed shared surfaces
- CoreText Metal shared-event timeline semaphore import and IOSurface snapshot sequencing on top of the external render-context interop path
- macOS text-input method integration via `ITextInputMethodImpl`, plus AppKit `NSTextInput` marked-text and committed-text bridging on `MacOSView`
- bundled app-host runtime hardening for generated net10 AppKit bindings that reject null `MainMenu`, `ServicesMenu`, `MiniWindowImage`, and `ApplicationIconImage` assignments
- raw AppKit mouse, drag, wheel, keyboard, modifier, and text input routing from `MacOSView` into Avalonia raw input
- managed popup positioning via `ManagedPopupPositioner` with native `NSPanel` popup hosting
- basic native window move dragging and managed edge-specific resize dragging
- real clipboard text read/write using Avalonia `DataTransfer` and `NSPasteboard`
- AppKit-backed storage provider integration for file, save, and folder pickers with file-system storage items and well-known folder resolution
- live screen topology change notifications wired through `IScreenImpl.Changed`
- mounted volume discovery bound through `IMountedVolumeInfoProvider`
- `NSStatusItem`-backed tray icon support with visibility, tooltip, icon, template-icon, and click handling
- basic tray native-menu export for nested `NativeMenu` trees with click routing through `NSStatusItem`
- basic active-window native-menu export through `ITopLevelNativeMenuExporter` into the macOS menu bar
- basic application-menu export composed with the active window menu, plus standard hide/show/quit application commands
- basic dock menu export through `NSApplicationDelegate.ApplicationDockMenu`
- richer default application-menu semantics with native About, Services submenu registration, and standard key equivalents
- storage bookmark save/open support for files and folders, including macOS-native bookmark decoding with BCL fallback
- window miniwindow icon updates, dock app icon updates, and app-wide dock visibility toggling from `IWindowImpl` icon/taskbar calls
- clipboard file-url round-tripping for local files and folders alongside plain text clipboard payloads
- clipboard html and custom string/byte platform-format mapping through `NSPasteboard`
- shared Metal platform graphics context backed by a stable native device/command-queue wrapper
- focused backend tests for macOS key/modifier translation helpers
- focused backend tests for macOS clipboard file-url conversion helpers
- focused backend tests for macOS clipboard format mapping helpers
- focused backend tests for macOS window resize geometry
- focused backend tests for macOS storage provider path and file-system operations
- focused backend tests for mounted volume enumeration

## Subsystem Status

| Subsystem | Status | Notes |
| --- | --- | --- |
| Bootstrap and package shape | Done | Standalone package and sample integration are in place. |
| Dispatcher and app lifetime | Done | `NSApplication` delegate, run loop bridge, activation/background notifications are wired. |
| Standard top-level windows | Partial | Standard windows are usable with move and edge-specific resize drag behavior; embeddable top-level/window creation now returns lightweight `NSView`-backed implementations that can attach into parent macOS views, but broader host-scenario validation is still pending. |
| Raw input | Partial | Mouse, drag, wheel, key, modifier, text, drag-source, and native drop-target events are routed, shared `NSView` tracking/backing updates are in place, and `MacOSView` now bridges AppKit `NSTextInput` into Avalonia preedit/commit flows; broader runtime IME validation is still pending. |
| Popup hosting | Partial | Native popup windows and managed positioning are implemented. |
| Clipboard | Partial | Text clipboard, local file-url round-tripping, and html/custom string-byte platform payloads work; bitmap payload validation is still missing. |
| Screens | Done | Enumeration and live screen-change notifications are wired. |
| Platform graphics | Partial | Metal/OpenGL/software/native-handle surfaces work, including a shared Metal graphics context for renderer context reuse; CoreText now exposes external IOSurface image import plus Metal shared-event timeline synchronization through the render-context public features, but shared-context import paths are still missing. |
| Platform services | Done | Cursors, theme settings, icons, hotkeys, key gesture formatting, storage/file-picker integration with bookmark save/open support, mounted volume discovery, basic tray support with tray menu export, composed application plus active-window native menu export with standard app commands, dock menu export, richer default application-menu semantics, and window/dock icon handling are registered. |
| Accessibility and text services | Partial | IME, marked text, and Avalonia `TextInputMethodClient` bridging are now wired through AppKit `NSTextInput`; accessibility/automation is still missing, and runtime IME validation in a live sample session is still pending. |
| Drag and drop | Partial | Native drag source and drop-target routing are wired for pasteboard-backed text, file, bitmap, and custom payloads, file drags now include companion URL-name metadata and better file icons, and the sample exposes a dedicated drag/drop lab; broader runtime validation against Finder/external apps is still constrained by the current environment. |
| Embedding | Partial | `CreateEmbeddableWindow` and `CreateEmbeddableTopLevel` now return lightweight `NSView`-backed implementations with parent-view attachment and host-window metric refresh; host integration and behavior polish still need validation. |

## Remaining Work For Full Backend Completion

### 1. Desktop parity blockers

- validate native drag/drop flows against Finder/external apps now that the drag session payload and preview paths are hardened
- validate embeddable host scenarios and close remaining behavior gaps in the lightweight `NSView`-backed implementation

### 2. Text and interaction completeness

- add accessibility/automation peers and native accessibility bridge
- extend clipboard integration to validated bitmap payloads
- validate IME and marked-text behavior in a live sample session across at least one non-Latin input source

### 3. GPU interop hardening

- extend the new IOSurface import path with shared-context import support where Avalonia composition expects it
- validate OpenGL context sharing and compositor interop behavior under real renderer workloads

### 4. Validation and hardening

- add behavioral tests for clipboard formats, popup positioning, and raw input translation
- add renderer/sample validation coverage for standard controls, text editing, and popup-heavy scenarios
- add memory/lifetime checks for window, popup, graphics-context, and clipboard native handle ownership

## Execution Order

1. Finish desktop parity validation and hardening for drag/drop runtime flows and embeddable hosting scenarios.
2. Finish text/input completeness: accessibility plus runtime IME validation.
3. Finish remaining GPU interop hardening for shared-context external Metal and IOSurface workflows.
4. Expand regression coverage and lifetime validation after each subsystem lands.

## Exit Criteria

- standard Avalonia desktop controls work without `Avalonia.Native`
- text entry works for plain input and IME/composition scenarios
- popups, clipboard, drag/drop, and storage flows work in normal desktop apps
- `CoreText.Avalonia` can interop with the window surfaces through Metal first and OpenGL when required
- remaining native handles and intermediate surfaces have deterministic ownership and regression coverage