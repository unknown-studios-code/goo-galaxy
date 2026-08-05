---
description: "Use when writing or reviewing Goo Galaxy multiplayer code. Covers the distributed authority topology, ownership and authority checks, NetworkVariable and RPC choices, and the Multiplayer Services session, matchmaking and relay flow."
paths:
  - "Assets/Scripts/Runtime/Networking/**/*.cs"
  - "Assets/Scripts/Runtime/**/*Network*.cs"
  - "Assets/Scripts/Runtime/**/*Session*.cs"
---

# Unity Netcode — Distributed Authority

## 1. Overview

Goo Galaxy runs on **Netcode for GameObjects in a distributed authority topology**, joined through the **Multiplayer Services SDK** (Sessions, which wraps Lobby, Matchmaker and Relay). There is no authoritative server process: each client owns a subset of the `NetworkObject`s and is the authority over them, and one client holds the **session owner** role, which coordinates scene management, synchronizes late joiners, and is promoted automatically if the current owner leaves.

Two consequences drive every rule below. First, code must never assume a server exists — `IsServer` branching is meaningless here. Second, Unity states plainly that this topology makes cheating easier and is not aimed at high-performance competitive play, so **match-critical state is arbitrated by the session owner and verified by the opponent**, and anything that touches money or progression is settled by a Unity Gaming Services backend, never by a peer.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Naming, `Awaitable`, event subscription discipline)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (VContainer wiring, `MatchEvents` as a local bus)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Allocation-free serialization on the network tick)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Desync triage and async cancellation)
- **Testing** → [unity-testing.md](unity-testing.md) (Session and replication flows are PlayMode territory)

## 3. Core Rules

- **Rule 1 (Authority, Not Server):** Gate every mutation with `HasAuthority` — it exists on both `NetworkObject` and `NetworkBehaviour` and is topology-agnostic. Use `IsOwner` when synchronizing transform movement. Never branch on `IsServer` or `IsHost`, and never write to a `NetworkObject` this client does not have authority over: the write is silently local and the next replication overwrites it.
- **Rule 2 (Session Owner):** Exactly one client is session owner (`NetworkManager.Singleton.IsSessionOwner`). It owns scene loading, late-join state, and — in this project — arbitration of match-critical state. Subscribe to `OnSessionOwnerPromoted` and re-establish that arbitration when the role moves, because promotion happens mid-match without warning when the current owner drops.
- **Rule 3 (Ownership Model):** Declare the permission on every networked prefab deliberately, using `NetworkObject.OwnershipStatus`:
  - `SessionOwner` — board state, turn/phase, and anything that decides the outcome of the match.
  - `None` — a player's own avatar or hand: static ownership, never redistributed.
  - `Distributable` — cosmetic or load-bearing-only objects that may follow whoever is connected.
  - `Transferable` / `RequestRequired` — units that change hands mid-match; `RequestRequired` when the current authority must approve.
    Transfer with `ChangeOwnership(clientId)` from the current authority, or `RequestOwnership()` from a challenger, which returns an `OwnershipRequestStatus`. Handle `OnOwnershipRequested`, `OnOwnershipRequestResponse`, and `OnOwnershipPermissionsFailure` explicitly — a denied request is a normal outcome, not an error. Use `SetOwnershipLock(true)` while a multi-step action is resolving, and always unlock it in the same code path that locked it.
- **Rule 4 (State Lives in NetworkVariables):** In distributed authority every `NetworkVariable` is owner-write and everyone-read by default. Keep replicated state in `NetworkVariable<T>` rather than plain fields, so it survives ownership transfer and reaches late joiners. Local-only derived values stay local and are recomputed from the replicated source; never replicate what the receiver can derive.
- **Rule 5 (RPC Targeting):** Use the universal `[Rpc(...)]` attribute — `ServerRpc` and `ClientRpc` are legacy. Pick the target explicitly: `SendTo.Authority` reaches the object's owner, and runtime targeting requires `SendTo.SpecifiedInParams` or `AllowTargetOverride = true`. Use RPCs for discrete events (a move was requested, a match ended) and `NetworkVariable` for continuous state; do not emulate one with the other.
- **Rule 6 (Match-Critical Arbitration):** A move is a **request**, never a result. The requesting client sends the intent, the authority over the board — the session owner — runs the same `Services/` validator that EditMode tests cover and publishes the outcome. The opposing client re-runs the validator against the replicated state and reports a mismatch instead of silently accepting it. Currency, progression, and store operations never resolve on a peer.
- **Rule 7 (Local Bus vs Wire):** `MatchEvents` is an **in-process** bus and replicates nothing. Network facts arrive through `NetworkVariable` changes or RPCs, and only then are republished locally with `MatchEvents.Raise*` so presenters and views stay unaware of the transport. Never subscribe a `NetworkBehaviour` to a local event and assume the other client saw it.
- **Rule 8 (Spawn and Despawn):** Wait for `OnClientConnectedCallback` before spawning anything — spawning earlier produces errors and undefined behavior. Any client may spawn objects it will own. When despawning something that still has a visual or audio tail, use `NetworkObject.DeferDespawn` rather than destroying immediately, and let pooling own the instance afterwards.
- **Rule 9 (Sessions Are the Entry Point):** Join through the Multiplayer Services SDK, never by wiring Lobby, Relay and Matchmaker by hand. `MultiplayerService.Instance` provides `CreateSessionAsync`, `JoinSessionByCodeAsync`, `JoinSessionByIdAsync`, `CreateOrJoinSessionAsync`, and `MatchmakeSessionAsync`. Build options as `new SessionOptions { MaxPlayers = 2 }.WithDistributedAuthorityNetwork()`; `WithRelayNetwork()` is the host-client fallback and `WithDirectNetwork()` is for dedicated hosting only — direct connections expose player IPs and are not acceptable for a shipped mobile client.
- **Rule 10 (Matchmaking):** Matchmake with `MatchmakeSessionAsync(matchmakerOptions, sessionOptions, cancellationToken)`, passing `new MatchmakerOptions { QueueName = "<queue>" }`. Always pass a `CancellationTokenSource` and cancel it when the player backs out — cancelling deletes the ticket, and a leaked ticket keeps matching a player who left the screen. Read the result with `session.GetMatchmakingResults()` for team and expected-player data. Queue and pool configuration lives in the Unity dashboard, not in code.
- **Rule 11 (Connection Lifecycle):** Subscribe to `session.Network.StateChanged` and `session.Network.StartFailed` and drive UI from `NetworkState`; a failed start is an expected outcome on mobile, not an exception path. When the network must start later than the session, omit the `With*Network()` call and start it from `session.Network` once the room is full. Surface `session.Id` and `session.Code` only where a player needs them.
- **Rule 12 (Mobile Network Budget):** Design for 150–300 ms round trip with 2–5% loss, reorder and duplication. Serialize with `INetworkSerializable` and fixed-size types so no path allocates on the network tick, batch small updates instead of sending per-frame deltas, and state each replicated field's update frequency and approximate bytes per second when adding one.
- **Rule 13 (Failure Paths Are Features):** Every networked flow declares what happens on disconnect, reconnect, session-owner promotion, and match abort, and the answer is written before the happy path is implemented. Timeouts are explicit; `Awaitable` operations carry `destroyCancellationToken` and catch `OperationCanceledException`.
- **Rule 14 (Assembly and Asset Boundaries):** Networking code lives in `GooGalaxy.Runtime.Networking`. `Runtime.Shared` may hold wire contracts — allocation-free value types such as `MoveCommand` — but must never reference the networking assembly. `NetworkManager`, the network prefabs list, and transport configuration are Unity-authored assets: describe the change as menu path, field and value instead of editing the file.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadNetworkPresenter> : NetworkBehaviour
{
    private int _capturedTiles; // ❌ Replicated state kept in a plain field: lost on ownership transfer

    public override void OnNetworkSpawn()
    {
        // ❌ There is no server in this topology
        if (IsServer)
        {
            SpawnBoard();
        }
    }

    public void RequestMove(MoveCommand command)
    {
        // ❌ The requesting client decides the outcome and then tells everyone
        MovementResolver.Resolve(_grid, command, _units, _affected);
        MatchEvents.RaiseMoveExecuted(command, _affected); // ❌ Local bus does not cross the wire
    }

    [ServerRpc] // ❌ Legacy attribute
    private void ApplyMoveServerRpc(MoveCommand command) { }
}
```

### ✅ Do (Good)

```csharp
public class <MovePresenter> : NetworkBehaviour
{
    private readonly NetworkVariable<int> _capturedTiles = new();

    private <MoveArbiter> _arbiter;

    [Inject]
    public void Construct(<MoveArbiter> arbiter)
    {
        _arbiter = arbiter;
    }

    public override void OnNetworkSpawn()
    {
        _capturedTiles.OnValueChanged += HandleCapturedTilesChanged;
    }

    public override void OnNetworkDespawn()
    {
        _capturedTiles.OnValueChanged -= HandleCapturedTilesChanged;
    }

    /// <summary>Sends the player's intent. The authority decides whether it happened.</summary>
    public void RequestMove(MoveCommand command)
    {
        SubmitMoveRpc(command);
    }

    // ✅ Universal RPC routed to whoever owns the board
    [Rpc(SendTo.Authority)]
    private void SubmitMoveRpc(MoveCommand command)
    {
        if (!HasAuthority)
        {
            return;
        }

        MoveResult result = _arbiter.Resolve(command);
        ConfirmMoveRpc(command, result);
    }

    [Rpc(SendTo.Everyone)]
    private void ConfirmMoveRpc(MoveCommand command, MoveResult result)
    {
        // ✅ The wire fact becomes a local fact only after the authority confirmed it
        if (result == MoveResult.Valid)
        {
            MatchEvents.RaiseMoveExecuted(command, _arbiter.AffectedCoordinates);
        }
    }

    private void HandleCapturedTilesChanged(int previous, int current)
    {
        // ✅ Views react to replicated state, not to the transport
    }
}
```

```csharp
public class <SessionBootstrap>
{
    private readonly CancellationTokenSource _matchmakingCancellation = new();

    /// <summary>Queues the player for a 1v1 match and returns once the session is live.</summary>
    public async Awaitable<ISession> MatchmakeAsync()
    {
        var matchmakerOptions = new MatchmakerOptions { QueueName = "<QueueName>" };

        // ✅ Distributed authority transport, two players, joined through Sessions
        var sessionOptions = new SessionOptions { MaxPlayers = 2 }.WithDistributedAuthorityNetwork();

        ISession session = await MultiplayerService.Instance.MatchmakeSessionAsync(matchmakerOptions, sessionOptions, _matchmakingCancellation.Token);

        session.Network.StateChanged += HandleNetworkStateChanged;
        session.Network.StartFailed += HandleNetworkStartFailed;

        return session;
    }

    /// <summary>Cancels the ticket when the player backs out, so the queue stops matching them.</summary>
    public void CancelMatchmaking()
    {
        _matchmakingCancellation.Cancel();
    }
}
```

## 5. Quick Reference & Decision Matrix

| Object                               | `OwnershipStatus` | Authority                   |
| :----------------------------------- | :---------------- | :-------------------------- |
| Board state, phase, win condition    | `SessionOwner`    | Session owner arbitrates    |
| A player's own hand, avatar, input   | `None`            | That player, permanently    |
| Unit that can change hands mid-match | `RequestRequired` | Current owner approves      |
| Cosmetic or ambient object           | `Distributable`   | Whoever the framework picks |

| Need                                     | Use                                                | Not                                       |
| :--------------------------------------- | :------------------------------------------------- | :---------------------------------------- |
| Continuous replicated state              | `NetworkVariable<T>` (owner-write by default)      | A plain field plus a broadcast RPC        |
| Discrete event                           | `[Rpc(SendTo.Authority)]` / `SendTo.Everyone`      | `ServerRpc` / `ClientRpc` (legacy)        |
| "Am I allowed to change this?"           | `HasAuthority`                                     | `IsServer`, `IsHost`                      |
| Transform sync ownership check           | `IsOwner`                                          | `HasAuthority` on the movement path       |
| Taking over an object                    | `RequestOwnership()` + response callbacks          | Writing to it and hoping                  |
| Join a match                             | `MatchmakeSessionAsync` with a cancellation token  | Lobby + Relay + Matchmaker wired by hand  |
| Peer-to-peer transport                   | `.WithDistributedAuthorityNetwork()`               | `.WithDirectNetwork()` on a mobile client |
| Telling local systems something happened | `MatchEvents.Raise*` after the authority confirmed | `MatchEvents` as if it replicated         |
| Despawn with a visual tail               | `NetworkObject.DeferDespawn`                       | `Destroy` on the frame of the despawn     |
