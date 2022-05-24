---
slug: release-1.0.0b10
title: Release 1.0.0b10
date: 2022-03-14
authors: [merlin]
tags: [release beta]
---

## Changelog
- Fix script upgrade causing duplication of code in some cases reported by @Narry
- Add support for serializing auto property backing fields
- Mitigate dropdowns having incorrect margins, contributed by @ArchiTechAnon
- Obsolete DrawPublicVariables U# API method and replace with DrawVariables
- Fix unary plus operator causing compile errors
- Make force upgrade button also queue prefab upgrade
- Fix Odin conflicting with U# inspectors, reported by @GlitchyDev and @BocuD
- Fix default editor fallbacks
- Fix multi-edit not being disabled on custom inspectors that do not support it
- Add handling for custom editors that edit child classes
- Re-add exposure tree