# ADR 0057: The campaign is playable by two: the host owns the world

**Status: Proposed.** Decision owners: product owner and lead-architect.
Date: 2026-11-13.

## Context

The owner directed that multiplayer include the full campaign, played with a
friend over the internet. The current trial shares boat presence and the sea
clock in East Water. Its catch, hands, money and story are not shared and it
keeps no campaign save. CO-01 brings that existing trial onto current main,
off by default; it does not implement the later campaign slices.

## Proposed decision

Two players share one campaign in the host's world. The host's computer is the only place where time, catches, goods, money and story flags change, and the only place a campaign save is written; the save gains a small record per player (place, hands, notes) through a versioned migration that still loads every older save. Tide, wind, weather, fish schools and NPC routines are never sent: both machines recompute them from the host's seed and clock, including the clock's hold state. Each player moves their own body and the boat they steer; the host referees the helm and every action that is counted, each action carrying who did it and a retry key so nothing pays twice. One shared purse, fleet and licence set (owner, 2026-08-04); separate hands, look and notes. One region at a time, crossed together. The host leaving ends the session; no host migration. Network contracts and the per-player context go through Core and the EventBus; the network module never reaches into another module's classes. Co-op is off by default and solo play keeps its existing path and tests. This supersedes the trial's unmerged "0055" draft and promotes `docs/adr/drafts/campaign-coop-proposal.md`, amended to keep body and steered-boat movement local. Not decided here: internet service provider costs, more than two players, separate regions.

## Confirmed direction and delivery order

- Internet joining is required in CO-03; its provider/account setup and charges
  still need review. CO-01 retains the direct-address LAN trial.
- One host world, one save, one purse, one fleet and licence set. The guest keeps
  only their character's look and name locally, with no goods or money export.
- Crew the one earned dory first; a second owned boat comes later. Each player
  moves their own body and the boat they steer; the host owns helm claims and
  counted actions, not guest input simulation.
- Both players are aboard the intro boat at sea from the opening: they can fish
  and sail in together. Joining only at the wharf after landfall is not the target.
  CO-04 to CO-08 include held-clock joining, supported passengers, group landfall,
  shared story and confirmed catches from that opening.
- CO-01 to CO-04 may run beside M1. Reassess at CO-05, including the two-machine
  moving-deck checkpoint, before committing to the larger actor integration.
- Until CO-09, the guest rejoins empty-handed and is not remembered between
  sessions. Stow carried goods before leaving. During a live session a dropped
  guest and goods stay supported where they were; recovery is by hand.
- Intro and campaign integration precede the save-format change. CO-09 owns a
  reviewed migration; its planned version 18 must be checked against main then.
  No save code or schema changes in CO-01.
- The host leaving ends the session. One region is crossed together; sleep
  requires both at rest and ready, with the night advanced once by the host.

See [CO-01 to CO-12](../../backlog/coop-campaign.md) for the ordered work and proof.
The original [campaign audit](../design/campaign-coop-plan.md) remains useful
background; this decision and backlog supersede its movement authority,
licence allocation, LAN-only scope and save-before-intro ordering.

## Packages and approval boundary

CO-01 carries the trial's three direct package additions: Netcode for GameObjects
2.13.3, Unity Transport 2.7.4 (resolved by Unity 6.5 to built-in 6.5.0) and Relay
1.2.0. Both the lead-architect and owner
must approve them before merge. Relay is not used by the current LAN trial.
Package inclusion is not acceptance of service costs or the proposed ADR.

## Consequences and proof

One authority prevents retries from duplicating catches, goods, payments or story
rewards. Deterministic sea and routines remain local computations, while actions
with history need host decisions. The actor context is the large Core change;
it belongs to CO-05, with no later-phase business, staff or freight here.

Solo retains its existing product/company, save path and test suite. One explicit
launch role enables the trial; without it no co-op bootstrap, transport, network
manager or replica is created. CI must run the unchanged solo suite and the
trial's named EditMode and network cases. The draft remains unapproved until
review, even after those tests pass.
