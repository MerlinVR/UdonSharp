---
slug: release-1.1.3
title: Release 1.1.3
date: 2022-10-27
authors: [merlin]
tags: [release]
draft: false
---

## Changelog

- Fixes sync check to allow syncing user defined enums again, reported by techanon [#75](https://github.com/vrchat-community/UdonSharp/issues/75)
- Use explicit SyntaxTree reference in UdonSharpUpgrader to avoid compile issues with a Unity package that has a bad namespace declaration, reported by Vesturo
