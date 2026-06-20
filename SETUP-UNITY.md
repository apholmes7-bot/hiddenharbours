# Setting Up the Unity Project — Step by Step

This guide takes you from a fresh Unity install to **driving the dory around and feeling the wind
and tide**. It's written for a non-developer; follow it in order. Agents: this also defines how the
code scaffold maps onto a real Unity project.

> The code is already written and sits in `Assets/_Project/` and `Assets/Tests/`. What's missing is
> the Unity project shell (which Unity must generate) and two scenes (which must be built in the
> editor). That's what this guide does.

---

## 0. Get the right Unity version

Install **Unity Hub**, then in Hub install **Unity 6.3 LTS (6000.3.x)** with these modules:
**2D**, **iOS Build Support**, **Android Build Support**.

> Use **6.3 LTS**, not 6.5. 6.5 is a short-support "Update" release with planned breaking changes;
> LTS is the stable foundation supported to Dec 2027 (see `docs/adr/0001-engine-choice.md`). If you
> already installed 6.5, the project still works — but LTS is the recommended pick.

## 1. Create the Unity project, then bring in this scaffold

Unity needs to generate its project files (`Packages/`, `ProjectSettings/`). The cleanest way to
keep **this folder** as the project root:

1. In Unity Hub → **New project** → **2D (URP)** template → name it `HiddenHarboursTemp`, create it
   in a temporary location, and let it open once, then close Unity.
2. In your file explorer, open `HiddenHarboursTemp` and **copy** its `Packages` and `ProjectSettings`
   folders into **this** project folder (`Hidden Harbours`), right next to the existing `Assets`,
   `docs`, and `agents` folders.
3. Back in Unity Hub → **Add → Add project from disk** → select **this** `Hidden Harbours` folder →
   open it.
4. Unity imports everything and **generates a `.meta` file for every script and folder**. That's
   normal — commit those `.meta` files (the `.gitignore` is set up to keep them).

You now have one project: your design docs, the agent system, and the game code, all in one repo.

## 2. One-time version-control setup (do before committing scenes/art)

Follow `docs/architecture/project-structure.md` §6: `git lfs install`, set Unity's
**Asset Serialization = Force Text** and **Version Control = Visible Meta Files**, and configure the
**UnityYAMLMerge** smart-merge tool. This is what lets agents (or you) work without corrupting
scenes and binary files.

## 3. One project setting for the greybox controls

**Edit → Project Settings → Player → Other Settings → Active Input Handling → "Both".**
(The placeholder dory controls use the legacy input manager; this enables it. The shipping mobile
controls will replace this later.) Restart Unity if it asks.

## 4. Create the two data assets

Content is data, not code — so the config and the dory are **assets** you create:

1. In `Assets/_Project/Data/Config`, right-click → **Create → Hidden Harbours → Game Config**.
   Leave the defaults (a 20-minute day, etc.) — you can tune these anytime, no coding.
2. In `Assets/_Project/Data/Boats`, right-click → **Create → Hidden Harbours → Boat Hull**.
   It defaults to **The Dory**. Done.

## 5. Build the Bootstrap scene (the services)

1. In `Assets/_Project/Scenes`, create a scene named **`Bootstrap`** (right-click → Create → Scene).
2. In it, create an empty GameObject named **`GameRoot`**.
3. Add three components to `GameRoot`: **GameClock**, **EnvironmentService**, **GameRoot**.
4. Assign the **Game Config** asset (from step 4) into the `Config` field of both **GameClock** and
   **EnvironmentService**.
5. On the **GameRoot** component, drag the GameClock into its `Clock` field and the EnvironmentService
   into its `Environment` field.
6. **File → Build Settings → Add Open Scenes** so Bootstrap is index 0.

Press Play: the Console should say **"Hidden Harbours services online. Fair winds."**

## 6. Build a greybox water scene + the dory

1. Create a scene named **`CoddleCove`** in `Assets/_Project/Scenes`. (For now, open it together with
   Bootstrap, or just test the dory here directly — full additive loading comes with backlog item
   VS-02.)
2. Make the **dory**: create a GameObject → add a **Sprite Renderer** (any placeholder sprite, e.g. a
   small rectangle — a real dory sprite is an `art-pipeline` task) → add a **Rigidbody2D** → add
   **BoatController** and **DevBoatInput**.
3. On **BoatController**, assign the **Boat Hull** asset to the `Hull` field.
4. Make sure your **Camera** is set to **Orthographic** and points at the dory.
5. (Optional, to feel the tide ground you) On **EnvironmentService**, lower `Local Seabed Depth`
   via the BoatController's field, or set the active tide profile to **Fundy Rips** in code/inspector
   to feel bigger swings.

Press Play. **Up/W** drives the dory; **A/D** steer. Notice it carries way (momentum), crabs in the
wind, and gets set by the current — that's the Pillar-1 feel, at the gentle inshore end.

## 7. Run the tests

**Window → General → Test Runner → EditMode → Run All.** The tide and weather determinism tests
should pass green. These are the guardrails that keep the save system and the "learnable sea"
honest — keep them green.

---

## What you have now

- A clean, modular Unity project matching `docs/architecture/project-structure.md`.
- The deterministic **clock + tide + weather** services (Pillar 1) with passing determinism tests.
- A **dory** you can drive that responds to wind, current, and tide, including running aground.
- The full **catch → hold → sell → wallet** loop in code (fishing with gated catch resolution, a
  supply/demand market, your purse) with tests — see `docs/architecture/code-scaffold.md` to wire it
  in a scene (W/Up throttle, A/D steer, **Space** to cast).
- The architecture (data-driven content, Core/EventBus decoupling, composition root) demonstrated end
  to end — agents now have running code to extend, not a blank project.

## What's next (hand these to agents)

Open `backlog/milestone-1-vertical-slice.md` and pull from the top. The immediate ones:
- **VS-02** additive scene loading + a proper `CoddleCove` greybox scene.
- **VS-05/VS-06** the fishing interaction and the first few fish (as data).
- **VS-13** the glanceable tide/wind/time **HUD** (`ui-ux`).
- **VS-22** a minimal Port Greywick wharf with a fish buyer, to close the catch→sell loop.

Each item lists its owning agent role and acceptance criteria. The agents read `CLAUDE.md` and their
charter in `agents/`, then build to those criteria.
