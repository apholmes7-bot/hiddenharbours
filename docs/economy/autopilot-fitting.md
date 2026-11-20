# Autopilot fitting seam

This is dormant, in-memory logic. It has no consumer, scene component, UI,
inventory transfer, global registration or save write.

`AutopilotEligibility` implements Core's `IAutopilotEligibility` as a pure
snapshot of hull id / existing `AutopilotOffered` pairs. A later registry caller
must project those exact fields; the native content tests read them from the
shipped Defs through serialized properties. Unknown and
duplicate hull identities refuse. Propulsion, console art and hull length add no
conditions. The authored fleet has 35 offered hulls and 11 refused hulls, including
the rowed dory. No hull definitions or authored flags change.

Economy's `AutopilotFittingService` implements the Core `IAutopilotFittingService`
contract and accepts an `IAutopilotFitments` store. Callers provide an owned boat
id and the non-negative offer price from data. Eligibility resolves through
`OwnedBoatId.HullOf`; state retains the full owned boat id so separate boats never
share installations. Boat ownership validation remains the eventual caller's
responsibility.

The first eligible fit spends through `IWallet.TrySpend` and records purchase and
installation only on success. Repeated fitting charges nothing. Removal restores
the unfitted state and retains that boat's purchased unit: no refund, destruction
or transfer. Refitting that unit on the same boat charges nothing, even if the
offer's price has since changed. This interprets removal as uninstalling owned
equipment; a later physical goods flow must handle carrying and transfer by hand.
Refused hulls, invalid ids/prices, absent wallets on a first purchase and failed
payments create no state. Calls run on the simulation thread.

`InMemoryAutopilotFitments` is session-local. A later save step must persist, per
stable owned boat id, whether the autopilot is purchased and whether it is
currently fitted. A removed but purchased unit must survive loading or refitting
would charge twice. No paid-price receipt is needed because there is no refund.
The hull's offered flag stays in its Def; neither eligibility nor transient
engagement/target heading should be saved here. The save format remains v25.

Today `BoatController.AutopilotFitted` decides heading-hold availability from a
non-null hull, `AutopilotOffered`, and either a sail plan or an engine helm.
It does not read purchased or fitted state. Its existing engagement safeguards
still apply, and this change leaves that path untouched. A later step must join
the stored installation to that availability check, wire the offer and wallet to
this service, and connect persistence. Catalogue purchase through the existing
instrument shop is not yet that join.

EditMode tests read every shipped hull Def rather than encode an eligibility
list. Pure tests cover payment idempotence, removal and free refitting, refusal,
insufficient funds, separate hulls, separate instances and separate sessions.
