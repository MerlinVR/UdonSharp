---
slug: release-1.0.0b9
title: Release 1.0.0b9
date: 2022-03-04
authors: [merlin]
tags: [release beta]
---

## Changelog
- Switch to using Unity C# scripts to store U# script data which has the following benefits for U# behaviours:
  - Support for prefab scene deltas
  - Support for prefab nesting
  - Support for prefab variants
  - Multi-edit support
  - Editor script dirtying behavior makes more sense
  - Custom inspectors and editor scripting now work on prefab assets properly
- Add upgrade path for converting old projects to new data format
  - Does not support upgrading nested prefabs and prefab variants since they were not supported prior to 1.0.0b9
- Improvements to assembly reload performance
- Inspector enum support
- Fixes for struct value write back, reported by @Hai and @Jordo
- Add InteractionText property to UdonSharpBehaviours
- Fixes for some methods not being found ex System.Type.Name, contributed by @bd_
- Remove redundant COW value dirty on this, contributed by @bd_
- Catch unhandled exceptions from compiler and rethrow them as unhandled exceptions to avoid Tasks silencing exceptions
- Fix double brackets not being unexcaped on interpolated strings that weren't preforming any interpolation, contributed by @ureishi
- 'Expected' exceptions used to interrupt compilation now do not dump entire callstack to debug log
- Enable runtime exception watching by default
- Add checks for Unity C# compile errors before initiating a U# compile to avoid confusion
- Add more validation for invalid uses of program assets and script files
- Remove redundant script dirty ignore since it seems like something else was causing the dirtying and is no longer doing it
- Obsolete many editor APIs for editor scripting that are no longer needed
- Obsolete old overloads for station and player join events -- now throws compile error