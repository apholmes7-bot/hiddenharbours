# RIDE STRIP — arm=before
# GameConfig: WaveExaggeration=1.6 (harness uses 1.6); Water.mat _OceanSwellScale=0.025 => freqScale=1 (harness uses 1)
# WaveField: base λ 6 m + 1.5 m per m/s, A 0.8 m, sharpening 2.6, spectrum blend 0.65
# StormRock: start 0.4, chase ω 7 rad/s, ζ 1, cap 1 g, band 0.8 m

## THE FLEET'S HEAVE CHARACTER — every number from the hull's own asset + the shipped block
# HullWeight: enabled=True spacing=2 m N in [3,65] C_wp=0.85 C_b=0.55 added=1 rho=1025 zeta in [0.35,1]
|hull|L m|beam m|draught m|displacement t|MassKg t|N|spacing m|T s|T(MassKg) s|zeta|overshoot|
|---|---|---|---|---|---|---|---|---|---|---|---|
|Dory|4.5|1.67|0.30|1.3|0.4|3|1.50|1.25|0.70|0.35|0.31|
|FishingSkiff|4.0|1.48|0.35|1.2|0.5|3|1.33|1.35|0.84|0.35|0.31|
|Punt|5.2|1.92|0.50|2.8|0.7|3|1.73|1.61|0.80|0.35|0.31|
|SportSkiff|7.0|2.59|0.50|5.1|1.0|5|1.40|1.61|0.70|0.35|0.31|
|ConsoleSkiff|7.0|2.59|0.55|5.6|1.2|5|1.40|1.69|0.78|0.50|0.16|
|LobsterInshoreOpenFundy|8.6|3.18|1.15|17.7|2.5|5|1.72|2.45|0.91|0.55|0.13|
|LobsterBoat|12.0|4.44|1.30|39.0|6.8|7|1.71|2.60|1.09|0.65|0.07|
|CapeIslander|12.9|4.77|1.40|48.6|6.0|7|1.84|2.70|0.95|0.65|0.07|
|LobsterOffshoreOpenFundy|14.6|5.40|1.45|64.5|12.0|9|1.62|2.75|1.19|0.70|0.05|
|SportFisherConvertible|16.2|5.99|1.75|95.8|15.0|9|1.80|3.02|1.19|0.68|0.05|
|SideDragger|25.0|9.25|2.90|378.1|90.0|13|1.92|3.89|1.90|0.75|0.03|
|SternTrawler|38.0|14.06|4.20|1265.0|316.0|19|2.00|4.68|2.34|0.80|0.02|
|CoastalPacket|60.0|22.20|5.00|3754.6|1244.0|31|1.94|5.10|2.94|0.85|0.01|
|Tanker|110.0|40.70|6.50|16405.4|7668.0|55|2.00|5.82|3.98|0.90|0.00|

## STRIP — sea state 0.30 (wind 2.2 m/s), 30 s at 60 Hz, hull lying to at the origin
|hull|L m|T m|surface RMS m|ride RMS m|ride/surface|reversals/30s|peak rate m/s|lag s|held-at-g %|excursion m|
|---|---|---|---|---|---|---|---|---|---|---|
|Dory|4.5|0.30|0.1962|0.1962|1.000|37|1.124|0.000|0.00|0.000|
|CapeIslander|12.9|1.40|0.1962|0.1962|1.000|37|1.124|0.000|0.00|0.000|
|Tanker|110.0|6.50|0.1962|0.1962|1.000|37|1.124|0.000|0.00|0.000|

## STRIP — sea state 0.75 (wind 8.8 m/s), 30 s at 60 Hz, hull lying to at the origin
|hull|L m|T m|surface RMS m|ride RMS m|ride/surface|reversals/30s|peak rate m/s|lag s|held-at-g %|excursion m|
|---|---|---|---|---|---|---|---|---|---|---|
|Dory|4.5|0.30|0.8020|0.8154|1.017|22|3.231|0.000|0.22|0.065|
|CapeIslander|12.9|1.40|0.8020|0.8275|1.032|22|3.250|0.033|0.06|0.160|
|Tanker|110.0|6.50|0.8020|0.8275|1.032|22|3.250|0.033|0.06|0.160|

## TRANSFER FUNCTION — one pure train, A = 1.00 m, running down the hull's own axis
(steady-state ride amplitude / wave amplitude, measured over the last 20 s of a 40 s run)
|hull|L m|λ2|λ3|λ4|λ6|λ8|λ12|λ16|λ24|λ32|λ48|λ64|
|---|---|---|---|---|---|---|---|---|---|---|---|---|
|Dory|4.5|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|
|CapeIslander|12.9|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|
|Tanker|110.0|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|1.000|

## COST (rule 7) — one WaveMath.Sample over 8 live trains = **459.7 ns** on this box (sink 283099.0, 2000000 reps).
Nine Mile Creek's moored fleet is 30 hulls; at the shipped block a 12 m lobster boat takes 7 samples, so the fleet + the player is ~217 samples/frame = **0.100 ms**, against a 16.7 ms frame. Before this PR it was 31 samples = 0.014 ms.

