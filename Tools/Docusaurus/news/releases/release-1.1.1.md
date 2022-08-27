---
slug: release-1.1.1
title: Release 1.1.1
date: 2022-08-26
authors: [momo]
tags: [release]
draft: false
---

## Changelog
- Fixes Locator to avoid spamming this Error on first project open: `Exception: Could not find UdonSharp locator, make sure you have installed U# following the install instructions.`
- Fixes file watcher breaking on non-Windows systems, contributed by anatawa12 (https://github.com/vrchat-community/UdonSharp/pull/47)