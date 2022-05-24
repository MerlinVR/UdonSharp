---
slug: release-1.0.0b12
title: Release 1.0.0b12
date: 2022-04-01
authors: [merlin]
tags: [release beta]
---

## Changelog
- Make UdonBehaviourSyncMode attribute affect inherited classes, requested by @BocuD
- Add UDONSHARP scripting define to check for presence of U# in project
- Make upgrade process with scene open more robust
- Add handling for `GetComponent(s)<UdonSharpBehaviour>()` (the exact UdonSharpBehaviour type, types directly inherited from UdonSharpBehaviour were already handled)
- Add handling for `GetComponent(s)<T>()` on UdonSharpBehaviour types using inheritance
- Add workaround handling for `GetComponent(s)<T>()` on VRC component types
- Fix issue where generic methods could leak their type arguments to other uses with different type arguments
- Fix serialization on base class declared fields, reported by @kafeijao
- Add better error when declaring nested types