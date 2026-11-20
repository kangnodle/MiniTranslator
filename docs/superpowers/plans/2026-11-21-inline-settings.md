# Inline Settings Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the current workspace. Steps use checkbox syntax for tracking.

**Goal:** Switch between translation and settings inside one window without changing its bounds or losing translation text or scroll positions.

**Architecture:** Convert SettingsWindow into a SettingsView user control. TranslatorWindow retains its translation controls and overlays settings, while AppShell routes all settings entry points through that window. Existing application and domain behavior stays unchanged.

**Tech Stack:** C#, .NET 11, WPF, xUnit.

**Spec:** User-approved request in this chat: preserve window size and position, original text, translated text, and scroll positions when returning from settings.

## Global Constraints

- Preserve existing uncommitted font and theme changes.
- Add no dependencies or source comments.
- Keep settings validation, hotkey resumption, sign-in cancellation, and save failures intact.

## Review Focus

- Save, cancel, back, and Escape return to translation; failed saves remain in settings.
- Settings opened from the tray or startup use the same translator window.
- Translation shortcuts do not execute behind settings.
- Settings fit the translator's minimum width without changing window constraints.
- Hidden translation controls retain their layout, text, selection, and scroll positions.

### Task 1: Embed settings and preserve translation state

**Files:** TranslatorWindow.xaml and code-behind; SettingsWindow.xaml and code-behind renamed to SettingsView; AppShell.cs; WindowUxTests.cs.

- [x] Add navigation regression checks for bounds, text, selection, scroll positions, save failure, and settings cleanup; run the WPF tests to confirm the missing behavior.
- [x] Convert settings to a UserControl with explicit close/cleanup behavior; embed it without recreating or collapsing translation controls.
- [x] Route AppShell settings entry points through TranslatorWindow and adapt existing UI checks to the embedded view.
- [x] Run WPF tests, the solution test suite, and a release build; inspect the final diff for unrelated changes.

No commit or publishing is requested. Work stays in the supplied workspace because it extends existing uncommitted changes.

## Verification

- `dotnet test MiniTranslator.slnx -c Release --no-restore --verbosity minimal`: all 391 tests passed.
- `dotnet build apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj -c Release --no-restore --verbosity minimal`: no errors or warnings.
- Independent review findings were reproduced and fixed: dropdown Escape cancels only the dropdown, and a previous asynchronous save cannot close a newly opened settings draft.
- UI tests process the WPF dispatcher and explicitly prevent application startup, keeping settings persistence and global hotkeys outside the test boundary.
