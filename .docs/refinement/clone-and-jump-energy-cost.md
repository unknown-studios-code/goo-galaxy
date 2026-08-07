# Energy cost for Clone and Jump

**Status:** open — needs refinement into a task
**Trigger:** Subject Alpha costs 1 Energy, but a player who already owns units can expand by cloning them.
If a Clone is free, the card buys nothing and the cheapest card in the game is dead weight. The GDD prices
every card but never prices the two move types, so the question has no answer today.

---

## What the GDD already fixes

Three constraints bound any answer. They are not up for negotiation in this document.

| Constraint | Source |
| :--- | :--- |
| $P_v = k \cdot E^2$, and **card Energy costs are never changed** — balance is corrected through ability parameters | `02_Mathematics_and_Balancing.md` |
| A **successful deployment** is any placement that passes validation, **spends Energy**, and resolves on the board. Every status and hazard duration is counted in these | `01_Mechanics_and_Core_Gameplay.md` |
| A unit landing by **either** Clone or Jump converts adjacent enemies | `01_Mechanics_and_Core_Gameplay.md` |

The third is why a free move type cannot work: landing converts, so a free move is free conversion, repeatable
without limit in real time. It also hands the player free duration ticks, which would expire an opponent's
Cryo-Stasis at no cost.

## The proposal — flat action costs

Three distinct actions, each with its own price.

| Action | Cost | What it does |
| :--- | :--- | :--- |
| **Deploy** | the card's Energy cost | Places a new unit of the played card's type on a valid hex |
| **Clone** | flat, **~1.0** | An existing unit copies itself onto an adjacent hex |
| **Jump** | flat, **~0.5** | An existing unit relocates two hexes |

At the standard 0.357 E/s that is **2.8 s** for a Clone and **1.4 s** for a Jump. Overtime halves both.

Clone and Jump costs are new parameters. They are not card costs, so the $E^2$ budget is untouched: a card's
power is always paid at its full authored price, exactly once, at deploy.

## What it resolves

**Subject Alpha earns its slot.** At 1 Energy it prices identically to a Clone, so the choice stops being
economic and becomes positional: a Clone must start from a unit you already own and copies that unit's type,
while a Deploy places any card anywhere legal. Same cost, different constraint.

**Volatile Mass becomes reachable.** It is authored `CanClone: false` and can therefore never be produced by a
Clone. Separating Deploy from Clone means the card places the first one and the flag then reads as "this unit
cannot perform the Clone action" — a property of the unit, not a bar on playing the card.

**Action windows keep one rule.** All three actions spend Energy and resolve on the board, so all three are
successful deployments under the existing definition. No second clause is needed anywhere in the status or
hazard duration logic.

**No arbitrage.** Pricing a Clone as a fraction of the copied card would make "deploy once, clone forever"
strictly better than deploying repeatedly, for every card — the same $1/f$ efficiency gain regardless of cost.
That is a change to the price of a card's power by another route, which the $E^2$ rule forbids.

## Open questions

- **The ratio between the Clone cost and the cheapest card is the real tuning knob.** Below 1.0, cloning
  dominates Subject Alpha and the card is dead again. Above 1.0, cards dominate and the board fills slowly.
  1.0 is proposed because it makes the decision positional rather than economic, but it is a starting value,
  not a derived one.
- **Should Jump cost anything at all beyond preventing spam?** 0.5 is chosen to stay cheap enough that
  repositioning does not feel taxed while still consuming a real share of regeneration.
- **Does a Clone copy the source unit, or produce the played card's type?** `MovementResolver` passes the
  source unit's `CardId`; `PlaytestUnitSpawner` overrides it with the played card and documents the
  disagreement. This proposal assumes Clone copies the source and Deploy is the separate action that
  introduces a type — but the contradiction is live in the code and must be settled with it.
- **Where does a Deploy place a unit?** The GDD's Controls section says valid hexes highlight when a card is
  tapped, without defining which hexes those are. Adjacent to owned units is the obvious reading and matches
  the Ataxx lineage, but it is not written down.

## Related

- `.docs/GDD/01_Mechanics_and_Core_Gameplay.md` — Clone and Jump definitions, Action Timing Model, Controls
- `.docs/GDD/02_Mathematics_and_Balancing.md` — the $P_v \propto E^2$ budget and the rule against changing costs
- `.docs/GDD/03_Specimens_Protocols_and_Factions.md` — per-card costs and the `CanClone` / `CanJump` flags
