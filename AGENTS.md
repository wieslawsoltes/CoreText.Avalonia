# AGENTS.md

## Purpose

This repository exists to deliver a **fully featured, production-grade, standalone macOS rendering backend for Avalonia** built on **Core Text / Core Graphics / Core Image / Metal**, shipped as **NuGet packages outside the Avalonia repository**.

This is **not** a prototype repo. All work must be evaluated against:

- feature parity expectations from `Avalonia.Skia`
- idiomatic macOS API usage
- high-performance .NET implementation
- deterministic native resource ownership
- shippable package quality

If a change improves convenience but weakens correctness, performance, disposal, API idiomaticity, or standalone-packaging quality, it is the wrong change.

## Canonical Requirement Summary

The backend must satisfy all of the following:

1. Build a **fully featured complete macOS Core Text API based rendering backend** for Avalonia.
2. Use **Avalonia.Skia** as the primary structural reference for subsystem coverage and expected behavior.
3. Ship **outside of the Avalonia repository** as standalone NuGet packages.
4. Target **`net10.0-macos` only**.
5. Use modern native macOS APIs in the same spirit as:
   - `/Users/wieslawsoltes/GitHub/PretextSharp/samples/PretextSamples.MacOS`
6. Use the Avalonia private-API access pattern needed for out-of-repo backends, using:
   - `/Users/wieslawsoltes/GitHub/VelloSharp`
   - `/Users/wieslawsoltes/GitHub/Avalonia`
7. Use **idiomatic high-performance native API usage** for **Metal**, **Core Text**, **Core Graphics**, and **Core Image**.
8. Use **high-performance .NET patterns**.
9. Use **no reflection in shipping renderer code paths**.

## Reference Repositories

Use these as the first sources of truth before inventing a new pattern:

- Avalonia rendering/backend reference:
  - `/Users/wieslawsoltes/GitHub/Avalonia/src/Skia/Avalonia.Skia`
- Avalonia private API usage / packaging / contracts:
  - `/Users/wieslawsoltes/GitHub/Avalonia`
- Out-of-repo Avalonia renderer packaging/bootstrap reference:
  - `/Users/wieslawsoltes/GitHub/VelloSharp`
- `net10.0-macos` app/bootstrap reference:
  - `/Users/wieslawsoltes/GitHub/PretextSharp/samples/PretextSamples.MacOS`

## Non-Negotiable Product Constraints

- The backend is **macOS-only**.
- The backend targets **`net10.0-macos`**.
- Shipping packages must remain **standalone** and **out-of-repo**.
- Avalonia package versions must be **exactly pinned**, never floating.
- Private Avalonia APIs may be used only through the explicit MSBuild opt-in properties already established in this repo.
- The codebase must aim for **behavioral parity with Avalonia.Skia**, while using native macOS APIs appropriately instead of mechanically copying Skia internals.

## Required Package Surface

The intended package shape is:

- `CoreText.Avalonia`
  - renderer
  - text stack
  - bitmaps / geometry / effects / render targets
- `CoreText.Avalonia.Native`
  - Avalonia Native bootstrap integration
  - `UseCoreTextNative(...)`

The backend must remain usable without a local Avalonia checkout at consumer build time.

## Architecture Rules

The implementation should continue to mirror `Avalonia.Skia` structurally where it improves parity, maintainability, and discoverability:

- render interface
- render interface context
- drawing context
- render targets
- bitmap implementations
- geometry implementations
- region implementation
- text shaper / glyph run / typeface / font manager
- platform bootstrap

Structural parity is expected; blind source translation is not.

## Native API Requirements

### Core Graphics / Quartz

Use Core Graphics as the primary rasterization path for:

- fills
- strokes
- clipping
- transforms
- bitmap draws
- offscreen layers
- opacity groups
- masks

Quartz usage must be **coordinate-space correct** and **layer-local** where appropriate. Any clip, mask, or effect implementation that behaves correctly only in root-surface space is a bug.

### Core Text

Use Core Text as the native text engine for:

- shaping
- font fallback
- glyph lookup
- glyph metrics
- glyph advances
- glyph outlines
- glyph drawing

The text stack must be **CoreText-native**. Do not add HarfBuzz or another shaping engine to compensate for missing Core Text work unless an explicit design decision is made and documented.

### Core Image

Use Core Image for effect processing when effects are enabled:

- blur
- drop shadow
- effect-oriented compositing

Software fallback code may exist only as an explicit fallback strategy, not as the main implementation behind a Core Image option.

### Metal

The Metal path must be **idiomatic and high-performance**:

- use **IOSurface-backed shared textures**
- present through **Metal texture/blit workflows**
- avoid steady-state CPU upload paths
- avoid `ReplaceRegion`-style CPU uploads as the primary presentation model

If the implementation falls back to CPU upload for the main Metal path, it is not acceptable as the final design.

### ImageIO

Use ImageIO for image decode/encode functionality instead of ad hoc or non-native alternatives.

## Performance Contract

### General

The renderer is expected to be performance-oriented. Do not accept a managed convenience approach when a lower-allocation, more direct implementation is available and maintainable.

### Forbidden Patterns In Shipping Renderer Paths

The following are prohibited in shipping runtime paths unless explicitly justified and documented:

- reflection
- private member access via reflection
- dynamic invocation
- `Type.GetType`-based dispatch
- runtime expression compilation
- repeated LINQ allocation chains inside render loops
- repeated string-based lookup in hot paths
- per-frame selector/class resolution for Objective-C interop

If a feature currently requires reflection, treat that as a bug to be removed, not a stable design.

### Required .NET Performance Practices

Prefer:

- `Span<T>` / `ReadOnlySpan<T>`
- `ArrayPool<T>` where repeated temporary arrays are needed
- `stackalloc` for small fixed temporary buffers
- `readonly struct` and `sealed` types where appropriate
- explicit ownership and disposal
- amortized caching with clear invalidation rules
- avoiding allocations in hot drawing/text/interop paths

Avoid:

- hidden boxing in hot paths
- unnecessary iterator allocations
- repeated dictionary churn in render loops
- repeated object creation where reuse is practical

### Native Interop Practices

Prefer:

- direct native interop with explicit ownership
- generated interop where practical
- cached selectors, classes, and constants
- stable, typed `objc_msgSend` entry points

Do not:

- repeatedly recreate native state that should be cached
- hide ownership behind opaque helper stacks
- keep native caches alive longer than necessary

## Memory and Disposal Contract

All native resources must have a clear owner and a deterministic lifetime.

This includes:

- Core Graphics contexts
- Core Graphics images
- Core Text fonts / frames / lines / runs
- Core Image contexts / images / filter intermediates where ownership applies
- Metal command buffers, textures, descriptors, and queues
- IOSurfaces
- unmanaged memory buffers

### Mandatory Rules

- Every owned native handle must have a deterministic dispose path.
- Finalizers are a safety net, not the primary lifetime mechanism.
- Unmanaged allocations large enough to materially affect GC behavior must use memory pressure accounting where appropriate.
- Offscreen/layer surfaces must be **bounded to the real content/effect bounds**, not allocated at full target size unless absolutely required.
- Effects must not retain unnecessary full-frame intermediates.
- Shared caches must have explicit reclaim/clear behavior.

### Leak and Footprint Expectations

Every substantial rendering/effects/layer change must be reviewed for:

- startup footprint
- steady-state footprint
- repeated render growth
- native handle retention
- hidden surface churn

Use these tools when relevant:

- `ps`
- `vmmap`
- `leaks`
- targeted leak-style tests with explicit lifecycle boundaries

Small tool-reported system-level leaks may exist, but unbounded growth or unexplained large steady-state surfaces are not acceptable.

## Metal-Specific Requirements

The Metal implementation must be treated as a first-class backend path, not a thin demo bridge.

### Required

- IOSurface-backed presentation path
- explicit command queue / command buffer ownership
- minimal copy count
- predictable synchronization
- stable DPI/size handling
- no unnecessary CPU readback or upload in the steady-state render loop

### Not Acceptable

- CPU bitmap render + texture upload as the intended long-term architecture
- accidental creation of large offscreen IOSurfaces for ordinary effects or small subtrees
- using Metal only nominally while most cost remains in avoidable CPU staging

## Text-Specific Requirements

The text implementation must support normal Avalonia text scenarios, not only direct glyph demos:

- `TextLayout`
- `FormattedText`
- `TextBlock`
- inline formatting
- decorations
- fallback fonts
- control text
- bidi/cluster correctness

Text rendering must remain correct when combined with:

- effects
- opacity
- opacity masks
- clips
- transforms
- offscreen layers

Any workaround that bypasses standard Avalonia text layout is temporary only.

## Visual Correctness Requirements

The backend must handle all of the following correctly:

- rectangular clips
- rounded clips
- geometry clips
- combined geometry
- opacity groups
- opacity masks
- blur
- drop shadow
- box shadows
- tiled image brushes
- gradients
- transforms under clips/effects/layers
- text under effects/layers

Coordinate-space bugs are common in renderer work. Every new layer, mask, clip, or effect implementation must be checked for:

- root-surface coordinates
- local offscreen coordinates
- transformed logical coordinates
- pixel-space offsets during recomposition

## Sample App Requirements

The sample is not ornamental. It is a renderer validation surface.

It must exercise:

- shapes
- images
- gradients
- tiled brushes
- opacity masks
- shadows / effects
- standard Avalonia text layout
- rich text / inlines
- standard controls
- software surface
- Metal surface

A sample that only shows custom glyph runs or avoids standard Avalonia controls/text is insufficient.

## Testing Requirements

Every substantial renderer change should add or update tests in the relevant area.

Minimum expectations:

- targeted bitmap/render regression tests
- text rendering tests where applicable
- effect/layer/clip regression coverage for coordinate-space fixes
- memory/leak-oriented tests when lifetime bugs are touched

Tests should be added for bugs that were actually fixed. If a visual bug was real, encode it.

## Packaging and Build Requirements

- Keep `Avalonia` package versions pinned exactly.
- Preserve the private-API opt-in MSBuild flags required for out-of-repo backends.
- Do not introduce a build-time dependency on a local Avalonia checkout.
- Maintain `net10.0-macos` targets.
- Keep the backend shippable as standalone NuGet packages.

## Review Checklist

Before considering a change done, verify:

- Does it move the repo closer to `Avalonia.Skia` feature parity?
- Is the macOS API usage idiomatic?
- Is the Metal path still high-performance and IOSurface-based?
- Is Core Image actually used when the option says it is?
- Is the Core Text path still native and complete?
- Did this introduce reflection anywhere in shipping runtime paths?
- Are intermediate surfaces bounded and disposed?
- Are native handles clearly owned and released?
- Did startup or steady-state footprint regress?
- Is there regression coverage for the fixed behavior?

## Definition of Done

A change is only done when all of the following are true:

- functionally correct
- performance-conscious
- no reflection-based runtime hacks
- deterministic disposal is in place
- sample behavior is correct
- tests cover the fixed bug or new capability
- packaging/standalone requirements remain intact

## When in Doubt

When choosing between:

- a convenient managed shortcut and a direct native path
- a reflection hack and an explicit contract
- a full-surface intermediate and a bounded one
- a temporary compatibility shim and a real backend implementation

choose the option that best preserves:

- correctness
- performance
- native API idiomaticity
- long-term maintainability
- standalone package quality
