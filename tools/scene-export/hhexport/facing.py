"""The compass bearing a baked cell depicts — derived from the index, and only where a count is.

``facingIndex`` has shipped since the write-back contract's §8.1 ask; ``facing`` shipped ``null``
beside it, because turning a step into a bearing is the one piece of arithmetic this repo had
already got wrong and a confidently wrong compass name is worse than an absent one. This module
is that field, done from the measurements rather than from a plausible re-reading of them.

**The rule: cell ``i`` depicts a ground bearing of ``(360 / facings) * i``, clockwise from north.**

Four independent sources say so, and they were checked against each other rather than stacked:

* ``RigBaker.DirForCell`` states the invariant it exists to create. A counter-clockwise rig is
  baked as ``render((N - k) % N)`` and a clockwise one as ``render(k)``, and its own summary of
  why is *"⇒ Anything baked here is genuinely clockwise"*. The inversion is what the baker hands
  the **rig**; the **sheet** it produces is clockwise either way.
* ``Buildings.json`` — the village kit's own contract — says it in the data, measured at bake
  time by ``BuildingRigAzimuthProbe``: *"The correction is already applied to the sheet, so cell
  i genuinely depicts +45*i."* Every per-sheet village sidecar repeats it.
* **ADR 0034** measured it against the shipped pixels rather than the intent: a standalone V8
  harness re-rendered the whole ``Fisher_idle`` sheet and came back byte-identical, which pins
  all eight rows at ``45°·d`` of ground azimuth, and row 1 — the diagonal, where the rival
  reading differs most — matched **exactly 45.00°**, i.e. NE.
* The **reference package** carries two worked pairs: ``facingIndex 3 -> "SE"`` and
  ``facingIndex 4 -> "S"``.

**The trap, which is why the reference's SE matters more than its S.** Applying the baker's
``dir = (facings - i) mod facings`` to the *exported index* and naming a compass point from the
result gives ``SW`` where the reference says ``SE``, and ``E`` where the village's own door
anchors say ``W``. It agrees on ``S``, because index 4 is a fixed point of that map — and index 4
is exactly the case a spot-check reaches for. That is the shape of the original defect: §6.2
records the schoolhouse door landing ~92° off the green *with a green test*, because the test was
the algebraic inverse of the implementation. The test that guards this one is therefore not the
inverse of this file: it asks whether the village's doors point at the village green, a target
this module never sees (see ``FacingTests``). Under the inverted rule the red saltbox comes out
**176°** from the green — facing away from the village it stands in.

**Where it is emitted, and where it is not.** Only where the sheet DECLARES a facing count
(§6.2's rule, unchanged: the count is read, never assumed) and only where the bearing lands on a
named point. Eight facings put every cell on one; four put all four on cardinals; a hypothetical
sixteen would leave the odd cells between names, and those keep ``facing: null`` and their
bearing rather than being rounded onto a neighbour.
"""

# Clockwise from north, which is the order the sheets are baked in.
NAMES = ("N", "NE", "E", "SE", "S", "SW", "W", "NW")

_STEP = 360.0 / len(NAMES)


def bearing_degrees(index, facings):
    """The ground bearing cell ``index`` depicts, in degrees clockwise from north, or ``None``.

    Ground, not screen: the world XY plane is the squashed ground plane (ADR 0034), and this is
    the un-squashed bearing the turntable actually stepped through. A consumer comparing it
    against a world-space direction has to un-squash that direction first — which is the whole
    subject of ADR 0034 and the reason ``BuildingFacing`` divides by the depth scale before it
    takes any angle.
    """
    if index is None or not facings or facings <= 0:
        return None
    return round(((360.0 / facings) * (index % facings)) % 360.0, 6)


def name_for(index, facings):
    """The compass name for a cell, or ``None`` when the bearing does not land on one.

    Never the nearest name: a bearing between two points is not either of them, and rounding it
    onto a neighbour is the aliasing this export refuses everywhere else.
    """
    bearing = bearing_degrees(index, facings)
    if bearing is None:
        return None
    step = bearing / _STEP
    nearest = round(step)
    if abs(step - nearest) > 1e-9:
        return None
    return NAMES[int(nearest) % len(NAMES)]
