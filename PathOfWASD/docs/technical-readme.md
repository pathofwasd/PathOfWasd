# PathOfWASD Technical README

This document keeps the workspace and developer-oriented setup that used to live in the root `README.md`.

For the user-facing overview, setup notes, and Wine instructions, start with the root [README.md](../README.md).

## Developer Guide

- Start with [docs/developer/README.md](developer/README.md).
- The guide is split into architecture, cursor and overlay runtime, input and remapping, end-to-end runtime sequences, and settings and persistence.

## VSCode Setup

- Open this folder in VSCode on Windows.
- Install the recommended extensions when prompted: `C#` and `C# Dev Kit`.
- Use a .NET SDK that can build `net9.0-windows`. This workspace was verified with `.NET SDK 10.0.102`.

## Run

- Press `Ctrl+Shift+B` and choose `build PathOfWASD` to build.
- Run `Terminal -> Run Task -> run PathOfWASD` to start the app without the debugger.

## Debug

- Open the file where you want to stop and click in the gutter to add a breakpoint.
- Open `Run and Debug` and choose `Debug PathOfWASD`.
- Press `F5` to build and launch under the debugger.
- When a breakpoint is hit, use the VSCode Debug view to inspect locals, call stack, watch values, and step through code.

## Publish

- Run `Terminal -> Run Task -> publish PathOfWASD single-file exe`.
- The publish output is a single self-contained Windows executable at `artifacts/publish/win-x64-single/PathOfWASD.exe`.

## Notes

- The project currently builds and publishes successfully, but it still has existing compiler warnings.
- The package `InputSimulatorPlus` restores with a compatibility warning against `net9.0-windows`, although it does not block build or publish.
