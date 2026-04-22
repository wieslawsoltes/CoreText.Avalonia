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

- raw AppKit mouse, drag, wheel, keyboard, modifier, and text input routing from `MacOSView` into Avalonia raw input
- managed popup positioning via `ManagedPopupPositioner` with native `NSPanel` popup hosting
- basic native window move dragging and fallback resize dragging
- real clipboard text read/write using Avalonia `DataTransfer` and `NSPasteboard`
- focused backend tests for macOS key/modifier translation helpers

## Subsystem Status

| Subsystem | Status | Notes |
| --- | --- | --- |
| Bootstrap and package shape | Done | Standalone package and sample integration are in place. |
| Dispatcher and app lifetime | Done | `NSApplication` delegate, run loop bridge, activation/background notifications are wired. |
| Standard top-level windows | Partial | Standard windows are usable; embedded top-levels are still unsupported. |
| Raw input | Partial | Mouse, drag, wheel, key, modifier, and text events are routed; IME/composition and drag-drop input are still missing. |
| Popup hosting | Partial | Native popup windows and managed positioning are implemented; native resize semantics still need a real edge-specific path. |
| Clipboard | Partial | Text clipboard works; richer formats and file/url round-tripping are still missing. |
| Screens | Partial | Enumeration works; live screen-change notification wiring is still missing. |
| Platform graphics | Partial | Metal/OpenGL/software/native-handle surfaces work; external Metal object import and IOSurface-sharing interop are still missing. |
| Platform services | Partial | Cursors, theme settings, icons, hotkeys, and key gesture formatting are registered; storage provider, mounted volumes, tray, and menus are still missing. |
| Accessibility and text services | Missing | Accessibility/automation, IME, marked text, and text input client support are not implemented. |
| Drag and drop | Missing | No native drag source/target bridge yet. |
| Embedding | Missing | `CreateEmbeddableWindow` and `CreateEmbeddableTopLevel` are still unsupported. |

## Remaining Work For Full Backend Completion

### 1. Desktop parity blockers

- implement native drag/drop source and target wiring
- add storage provider and file-picker integration backed by AppKit panels
- implement embeddable windows/top-levels for Avalonia hosting scenarios
- add live screen topology change notifications

### 2. Text and interaction completeness

- implement IME and marked-text composition via AppKit text input protocols
- add accessibility/automation peers and native accessibility bridge
- replace fallback resize dragging with true edge-specific native or managed resize behavior
- extend clipboard support beyond text to file urls, html, and binary payloads

### 3. macOS platform integration parity

- implement native menu bar integration, dock menu, and application command handling
- add tray/status-item support
- complete mounted-volume and storage-related platform services
- harden icon handling and dock/taskbar visibility behaviors

### 4. GPU interop hardening

- expose external Metal resource import paths required for zero-copy `CoreText.Avalonia` interop
- add IOSurface-backed sharing and synchronization primitives where Avalonia composition expects them
- validate OpenGL context sharing and compositor interop behavior under real renderer workloads

### 5. Validation and hardening

- add behavioral tests for clipboard formats, popup positioning, and raw input translation
- add renderer/sample validation coverage for standard controls, text editing, and popup-heavy scenarios
- add memory/lifetime checks for window, popup, graphics-context, and clipboard native handle ownership

## Execution Order

1. Finish desktop parity blockers: drag/drop, storage provider, embedding, screen notifications.
2. Finish text/input completeness: IME, accessibility, and real resize behavior.
3. Finish macOS shell integration: menus, tray, dock/app commands.
4. Finish GPU interop hardening for external Metal and IOSurface workflows.
5. Expand regression coverage and lifetime validation after each subsystem lands.

## Exit Criteria

- standard Avalonia desktop controls work without `Avalonia.Native`
- text entry works for plain input and IME/composition scenarios
- popups, clipboard, drag/drop, and storage flows work in normal desktop apps
- `CoreText.Avalonia` can interop with the window surfaces through Metal first and OpenGL when required
- remaining native handles and intermediate surfaces have deterministic ownership and regression coverage