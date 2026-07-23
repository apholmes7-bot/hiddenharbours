using System.Runtime.CompilerServices;

// The hull-waterline GPU acceptance test (ADR 0023 phase 3) drives the PRODUCTION off-screen
// feature in EditMode, where DisplacedWaterSurface's play-gated Activate() cannot run — the test
// registers a surface and publishes the calibrated iso-depth frame through the registry's
// internal seam instead, exactly as the component does in play. Tests only; nothing else should
// bind these internals.
[assembly: InternalsVisibleTo("HiddenHarbours.Tests.RigBaking.EditMode")]
