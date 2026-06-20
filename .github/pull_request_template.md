<!-- Hidden Harbours PR. Keep PRs small and single-purpose. See ../agents/coordination.md §6. -->

## What & why
<!-- One or two sentences. What does this change do? -->

**Backlog item:** <!-- e.g. VS-07 / M2-12 -->
**Pillar(s) served:** <!-- P1 Sea Has Moods · P2 Dory to Dynasty · P3 Living Coast · P4 Earn-then-Automate · P5 Cozy-with-Teeth -->
**Owning role:** <!-- e.g. gameplay-systems -->

## How I tested
<!-- EditMode/PlayMode tests added? Manual playtest? Determinism check if relevant? Phone build if perf-sensitive? -->

## Definition of Done (check all — see coordination.md §3)
- [ ] Meets the backlog item's acceptance criteria
- [ ] Obeys the ten rules (`../CLAUDE.md` §3): data-driven, scene/module boundaries, determinism, no magic numbers, mobile budget, in-phase
- [ ] Tests pass locally (new logic covered; determinism-sensitive logic has a determinism test)
- [ ] If `Data/` changed: content-validation test passes
- [ ] No stray files (`Library/`, builds, secrets); new binaries are LFS-tracked
- [ ] Docs updated in this PR (and canon updated *first* if a locked fact changed)
- [ ] Leaves a working, playable build

## Notes for reviewers
<!-- Anything touching Core / cross-module boundaries → tag lead-architect. Editing another role's area → tag that owner. -->
