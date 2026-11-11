# Verification of three unused source candidates

Checked against base `d9d4b8558aaa77b49b6bad9dbb35345d2059814c`.
This records the follow-up to the source-only inventory in PR #1063.

| Candidate | Declaration | Finding | Disposition |
| --- | --- | --- | --- |
| `HiddenHarbours.Audio.AudioBus` | `Assets/_Project/Code/Audio/AudioDirectorLogic.cs:31` | Public enum with `Ambience`, `Sfx`, and `Music`. The declaration is its only occurrence in tracked text. No field, parameter, serializer, content row, or consumer names this type. | Verified unused within this repository. Retain the public type; removing public API would need a separate compatibility decision. |
| `RegionValidatorWindow.FindInSceneMonoBehaviours(Scene)` | `Assets/_Project/Code/App/Editor/RegionValidatorWindow.cs:1122` | Private, unattributed wrapper around `FindInScene<MonoBehaviour>(scene)`. The declaration is its only occurrence in tracked text. Live validator checks call the generic helper directly. | Verified unused private wrapper. Retain during this verification pass. |
| `ProceduralAudio.Chirp(float, float, float, float)` | `Assets/_Project/Code/Audio/ProceduralAudio.cs:456` | Private, unattributed, static windowed-sine helper. The declaration is its only occurrence in tracked text. The procedural generators do not call it. | Verified unused private helper. Retain during this verification pass. |

## Verification scope

The exact whole-word search over **all tracked text**, rather than only C# callers, was:

```text
GIT_NO_LAZY_FETCH=1 GIT_OPTIONAL_LOCKS=0 git grep -I -n -w -e AudioBus -e FindInSceneMonoBehaviours -e Chirp d9d4b8558aaa77b49b6bad9dbb35345d2059814c -- .
```

It returned exactly the three declarations listed above. This scope includes tracked
editor scripts, tests, tooling, scenes, prefabs, data, project settings, package files,
and documents. It checks literal string and serialized references as well as direct
source references. It cannot establish use by untracked tools or external consumers.

The two private helpers have no entry-point attributes and their names and signatures
are not Unity lifecycle callbacks. Their containing code has no `GetMethod`,
`GetMethods`, `Invoke`, `SendMessage`, `BroadcastMessage`, or TypeCache dispatch that
could select either helper. The validator's `GetType().Name` use labels a finding;
it does not invoke a method. The generic scene scanner remains live and necessary.

This verification does not treat a source-reference count as evidence that an asset
is unused. Unity callbacks, editor menu/CLI entry points, TypeCache adapters, script
identities, and assets discovered through Resources or editor APIs remain outside
this cleanup candidate list.

## Behavior and review boundary

This change adds only this document. All three declarations remain intact. Runtime
and editor behavior, sound generation, serialized data, tuning, script metadata,
assets, and rendering are unchanged. No test or visual baseline is added or changed.
The verification introduces no need to run Unity or approve a new look.
