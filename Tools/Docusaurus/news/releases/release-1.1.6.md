---
slug: release-1.1.6
title: Release 1.1.6
date: 2022-12-02
authors: [merlin]
tags: [release]
draft: false
---

## Changelog

- Upgrading nested prefabs from U# 0.x projects should now be handled properly [#82](https://github.com/vrchat-community/UdonSharp/pull/82)
- Fixes for float -> int truncation and precision contributed by [@ureishi](https://github.com/ureishi) [#79](https://github.com/vrchat-community/UdonSharp/pull/79)
- Fix for casts from `char` -> float types contributed by [@ureishi](https://github.com/ureishi) [#80](https://github.com/vrchat-community/UdonSharp/pull/80)
- Fixes for string composite assignment contributed by [@ureishi](https://github.com/ureishi) [#29](https://github.com/vrchat-community/UdonSharp/pull/29)
- Fixes for equality between two null constants contributed by [@ureishi](https://github.com/ureishi) [#30](https://github.com/vrchat-community/UdonSharp/pull/30)
- Fix for checking event compatibility with built-in events to not only check event name, this would cause issues if you tried to register an event without the correct parameters for a built-in event.
- Fix to show jagged array fields if they are marked with OdinSerialize, but private
- Adds Curl errors that Unity now likes to throw with the menu updates to the event filter for log watcher
- Fix for inter-prefrab references on instantiated prefabs not applying correctly in some cases, because Unity
- Prevent running upgrade process on intermediate build prefabs
- Workaround for a case where Unity thinks an object is a prefab when it isn't during the prefab build process which could cause the process to fail
