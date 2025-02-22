---
slug: release-1.0.0b4
title: Release 1.0.0b4
date: 2021-09-20
authors: [merlin]
tags: [release beta]
---

## Changelog
- Add @bd_'s optimization for bitwise not operator
- Fix assembly finding handling causing issues with some 3rd party assets, reported by @Haï~
- Add blacklist entry for CyanEmu since it needs to reference some VRC scripts which U# doesn't link, reported by @Haï~ and @Miner28_3
- Fix calls to ToString() on enums, reported by @Miner28_3 and @Haï~
- Fix calls to `GetComponents<T>` not returning an array type internally, reported by @Miner28_3
- Fix calls to VRC methods with inconsistent return types in their signature such as VRCPlayerApi.GetPickupInHand(), reported by @Haï~ 
