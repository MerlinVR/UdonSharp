---
slug: release-1.0.0b5
title: Release 1.0.0b5
date: 2021-09-22
authors: [merlin]
tags: [release beta]
---

## Changelog
- Fix string compound addition with const character values, reported by @Pema99
- Fix covariant conversion of params arrays on reference type values, reported by @Haï~
- Fix issues with virtual method linkage allocations, reported by @Haï~
- Add checking for cases where base methods are shadowed by inherited methods, reported by @GlitchyDev
- Add checking for cases where base fields are shadowed by fields in inherited classes
- Add checking for abstract U# classes having a U# program asset associated with them, reported by @GlitchyDev
- Add fix for abstract methods causing the compiler to fail
- Add validation to make sure people aren't inheriting from interfaces since they will have support added in the coming weeks
- Add handling for using switch on object condition values, reported by @Pema99
- Add handling for empty statements, reported by @Pema99
- Add validation for the class name of U# behaviours mismatching their containing .cs file name since Unity breaks in dumb ways when it's not the same