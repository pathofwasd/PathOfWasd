# Developer Guide

Read these in order:

1. [Architecture](architecture.md)
2. [Cursor And Overlays](cursor-and-overlays.md)
3. [Input And Remapping](input-and-remapping.md)
4. [Runtime Sequences](runtime-sequences.md)
5. [Settings And Persistence](settings-and-persistence.md)

This guide set explains the current runtime design, not the historical WPF overlay path. The project now renders the virtual cursor through a native layered window and keeps the cursor-movement logic in the manager and controller layers.

Use this guide when:

- tracing a bug through startup, settings, hotkeys, controller state, and overlay rendering
- understanding the exact runtime order used for WASD mode, placeholder-key staging, and cursor jumps
- changing how the virtual cursor is shown or moved
- modifying the input-remapping flow for skills, clicks, or WASD movement
- changing settings storage, default values, or settings-window behavior

Use the root [README.md](../../README.md) for day-to-day build, debug, run, and publish steps.
