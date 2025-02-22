---
slug: release-1.0.0b11
title: Release 1.0.0b11
date: 2022-03-23
authors: [merlin]
tags: [release beta]
---

## Changelog
- Improve exposure tree scan speed a little
- Make GetUdonSharpComponent APIs just act like Unity equivalents instead of potentially erroring
- Add fallback drawing for jagged arrays
- Fix issue with local symbols getting incorrectly shared between different generic method type arguments, reported by @Miner28_3
- Fix U# behaviour enabled state not getting synced in the editor UI while in play mode properly, reported by @Fairplex
- Prevent editing script asset on UdonSharpProgramAssets once it has been set since it will not work now. Also add validation for out of sync assigned script types.
- Missing source script warning is now an error
- Obsolete and no-op UpdateProxy and ApplyProxyModifications editor API calls since they aren't needed in editor-time now and could cause issues with the new way of doing things.
- Fix multiply operation * on System.Decimal type, reported by @pnivek
- Fix integer -> user enum conversions when the underlying integer types don't match, reported by @GlitchyDev
- Fix script upgrader more, reported by @Phasedragon
- Make file change detection extend to all scripts that are linked by U# builds
- Fix AddUdonSharpComponent editor scripting APIs as they weren't actually working, reported by @BocuD
- Move UdonSharp menu items to be under the VRChat SDK top level menu