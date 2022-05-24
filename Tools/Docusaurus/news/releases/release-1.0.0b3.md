---
slug: release-1.0.0b3
title: Release 1.0.0b3
date: 2021-09-19
authors: [merlin]
tags: [release beta]
---

## Changelog
- Fix increment/decrement/compound assignment operators not converting numeric types of lower precision than int in some cases like on user defined properties, reported by @GlitchyDev
- Fix compile error when using static methods defined in other U# behaviours, reported by @GlitchyDev
- Add errors for when people attempt to use U# scripts that do not belong to a U# assembly, but are part of a C# assembly
- Optimize string interpolations and fix potential issues when using string interpolations in recursive methods