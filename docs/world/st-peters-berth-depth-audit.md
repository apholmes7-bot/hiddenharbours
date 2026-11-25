# St Peters berth depth audit

The arrival Cape Islander and the moored dory retain water under their keels at every tide on both models. Their actual scene positions, their sampled full berthed rectangles, all four NPC arrival waypoints and the three authored arrival centreline legs and the sea-entry continuation leg have a **-4.000 m bed** in both models. At lowest water this gives **1.800 m depth**, leaving **0.400 m** beneath the Cape Islander's 1.400 m draft and **1.500 m** beneath the dory's 0.300 m draft. No hull gains a new bottom contact at either occupied berth or at any arrival waypoint.

This does not make the entire wharf an all-tide berth. The east face's sampled minimum depth is 1.626 m analytic / 1.641 m painted at lowest water, enough for those two hulls. The north and south faces run into high ground toward their western ends; the west face is on land. The shoreward beach-slip point at (190, 0) dries on both models at lowest water: depths -1.200 / -1.230 m. At mean water it carries the dory (1.000 / 0.970 m depth) but not the Cape Islander; at highest water both clear it. The on-deck slip-head station is not a floating berth. These existing limitations must not be presented as new failures caused by the painted ground.

The changes that matter are farther inshore. **Ten of the 113 named samples differ by more than 0.5 m**, all at clam spots: nine become shallower, one deeper. The largest loss is **2.641 m at Clam 69 (-59.001, -41.835)**. A dory that floats on the analytic model newly touches bottom at lowest water at Clams 46, 52, 54, 58, 67 and 69, with shortfalls 0.711, 1.151, 1.076, 1.146, 0.955 and 1.141 m respectively. At mean water it also loses Clam 74 by 0.118 m. The Cape Islander loses those same six low-water sites, and additional mean-water sites listed below. These are possible boat positions at authored fishing spots, not boats already placed on the flats.

Screening **all 46 hull Defs** produces new contact cases at **26 named locations** (25 clam spots and one tie-off coordinate). At `wharf.st_peters.bollard_1` (203, 3.033), a hypothetical 1.750 m Sport Fisher Convertible goes from 0.004 m clearance to a 0.035 m shortfall at lowest water. The bollard is a deck attachment, not an assigned keel; this is a depth warning at that XY, not a failed occupied berth. The face sweeps below also expose small threshold crossings between named points. Larger hulls already unable to float in either model are not counted as regressions.

This is an offline survey of base `81b0795db6bd3bd6a5352d66167d4863f0ae2f41`, dated 2026-11-23. [Draft PR 1467](https://github.com/apholmes7-bot/hiddenharbours/pull/1467) was read at head `29a915a12fb6f231113eb097ae147b20865bd73c`; none of its changes were applied. Its reported 7–9% loss of inshore boat water is an area/reachability measure, not a berth failure rate; this point survey neither reproduces nor disproves that percentage. No scene, asset, runtime code, configuration or save format changes are part of this audit.

<!-- BEGIN GENERATED SURVEY -->
Source base: `81b0795db6bd3bd6a5352d66167d4863f0ae2f41`. Height PNG SHA-256: `8cc0e0335f97b036643146f88b1fc9dea59b26ac9c66823fc92daadbd80805c8`.
Decoded resolution: 1520 x 1040. 113 named samples; 46 hull Defs screened.

A = analytic; P = painted. All numbers are metres; depth triplets are lowest / mean / highest tide. Negative depth means exposed ground. **D** flags absolute bed difference > 0.5 m. **R** flags at least one hull that passes A but touches or penetrates P at a surveyed tide. Exact hulls and shortfalls follow. Coordinates are scene XY, not projected artwork positions.

### Occupied berths

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| Arrival berth / dock zone | 211.000, -0.267 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ArrivalOpening &1401477190 _berth` |
| Dory mooring | 213.000, 7.502 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `BoatController &971817987 transform` |

### Slip and dredged cuts

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| berth from | 240.000, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _berthFrom/To` |
| berth mid | 215.000, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _berthFrom/To` |
| berth to | 190.000, 0.000 | -1.000 / -0.970 | -1.200 / 1.000 / 3.200 | -1.230 / 0.970 / 3.170 | - | `TidalTerrain _berthFrom/To` |
| approach from | 255.000, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _approachFrom/To` |
| approach mid | 230.500, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _approachFrom/To` |
| approach to | 206.000, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _approachFrom/To` |
| berthPocket from | 205.050, -5.800 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _berthPocketFrom/To` |
| berthPocket mid | 211.500, -5.800 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _berthPocketFrom/To` |
| berthPocket to | 217.950, -5.800 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _berthPocketFrom/To` |
| northPocket from | 210.750, 7.502 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _northPocketFrom/To` |
| northPocket mid | 213.000, 7.502 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _northPocketFrom/To` |
| northPocket to | 215.250, 7.502 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `TidalTerrain _northPocketFrom/To` |
| Slip head (on deck) | 183.500, 4.393 | 5.992 / 5.977 | -8.192 / -5.992 / -3.792 | -8.177 / -5.977 / -3.777 | - | `RoutineStations slip_head` |

### NPC arrival waypoints

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| Arrival waypoint 0 | 340.000, 40.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ArrivalOpening _route[0]` |
| Arrival waypoint 1 | 262.000, 26.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ArrivalOpening _route[1]` |
| Arrival waypoint 2 | 255.000, 0.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ArrivalOpening _route[2]` |
| Arrival waypoint 3 | 211.000, -0.267 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ArrivalOpening _route[3]` |
| Arrival continuation entry | 316.000, 40.000 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `RegionAnchor east_water transform &1900200301` |

### All shore tie-offs

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| wharf.st_peters.bollard_0 | 212.000, 3.033 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ShoreCleat &1835875545` |
| wharf.st_peters.bollard_1 | 203.000, 3.033 | -3.954 / -3.915 | 1.754 / 3.954 / 6.154 | 1.715 / 3.915 / 6.115 | R | `ShoreCleat &987019899` |
| wharf.st_peters.bollard_2 | 194.000, 3.033 | 1.010 / 1.013 | -3.210 / -1.010 / 1.190 | -3.213 / -1.013 / 1.187 | - | `ShoreCleat &380891113` |
| wharf.st_peters.bollard_3 | 185.000, 3.033 | 4.753 / 4.749 | -6.953 / -4.753 / -2.553 | -6.949 / -4.749 / -2.549 | - | `ShoreCleat &155087979` |
| wharf.st_peters.cleat_4 | 209.000, 3.033 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `ShoreCleat &1725314845` |

### All pier faces

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| Pier south start | 183.000, 2.533 | 5.903 / 5.887 | -8.103 / -5.903 / -3.703 | -8.087 / -5.887 / -3.687 | - | `StandablePlatform _footprint` |
| Pier south mid | 198.250, 2.533 | 0.038 / 0.042 | -2.238 / -0.038 / 2.162 | -2.242 / -0.042 / 2.158 | - | `StandablePlatform _footprint` |
| Pier south end | 213.500, 2.533 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `StandablePlatform _footprint` |
| Pier north start | 183.000, 6.252 | 6.000 / 6.000 | -8.200 / -6.000 / -3.800 | -8.200 / -6.000 / -3.800 | - | `StandablePlatform _footprint` |
| Pier north mid | 198.250, 6.252 | 2.669 / 2.669 | -4.869 / -2.669 / -0.469 | -4.869 / -2.669 / -0.469 | - | `StandablePlatform _footprint` |
| Pier north end | 213.500, 6.252 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `StandablePlatform _footprint` |
| Pier west start | 183.000, 2.533 | 5.903 / 5.887 | -8.103 / -5.903 / -3.703 | -8.087 / -5.887 / -3.687 | - | `StandablePlatform _footprint` |
| Pier west mid | 183.000, 4.393 | 6.000 / 6.000 | -8.200 / -6.000 / -3.800 | -8.200 / -6.000 / -3.800 | - | `StandablePlatform _footprint` |
| Pier west end | 183.000, 6.252 | 6.000 / 6.000 | -8.200 / -6.000 / -3.800 | -8.200 / -6.000 / -3.800 | - | `StandablePlatform _footprint` |
| Pier east start | 213.500, 2.533 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `StandablePlatform _footprint` |
| Pier east mid | 213.500, 4.393 | -3.929 / -3.931 | 1.729 / 3.929 / 6.129 | 1.731 / 3.931 / 6.131 | - | `StandablePlatform _footprint` |
| Pier east end | 213.500, 6.252 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200 | 1.800 / 4.000 / 6.200 | - | `StandablePlatform _footprint` |

### All authored fishing spots (clam holes)

| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |
|---|---|---|---|---|---|---|
| Clam 01 | -359.214, -4.604 | -0.451 / -0.452 | -1.749 / 0.451 / 2.651 | -1.748 / 0.452 / 2.652 | - | `ClamSpot &32359400` |
| Clam 02 | -353.487, -3.705 | 0.507 / 0.505 | -2.707 / -0.507 / 1.693 | -2.705 / -0.505 / 1.695 | - | `ClamSpot &1105920861` |
| Clam 03 | -352.508, 13.100 | -1.156 / -1.177 | -1.044 / 1.156 / 3.356 | -1.023 / 1.177 / 3.377 | - | `ClamSpot &698684292` |
| Clam 04 | -345.174, -12.447 | -0.943 / -0.820 | -1.257 / 0.943 / 3.143 | -1.380 / 0.820 / 3.020 | R | `ClamSpot &741534453` |
| Clam 05 | -337.271, -5.243 | 0.485 / 0.485 | -2.685 / -0.485 / 1.715 | -2.685 / -0.485 / 1.715 | - | `ClamSpot &522077524` |
| Clam 06 | -336.670, -11.503 | -0.722 / -0.461 | -1.478 / 0.722 / 2.922 | -1.739 / 0.461 / 2.661 | R | `ClamSpot &214185681` |
| Clam 07 | -335.474, 9.235 | -0.223 / -0.320 | -1.977 / 0.223 / 2.423 | -1.880 / 0.320 / 2.520 | - | `ClamSpot &180110178` |
| Clam 08 | -324.657, 9.035 | -0.181 / -0.224 | -2.019 / 0.181 / 2.381 | -1.976 / 0.224 / 2.424 | - | `ClamSpot &188940619` |
| Clam 09 | -321.474, -15.993 | -1.802 / -1.531 | -0.398 / 1.802 / 4.002 | -0.669 / 1.531 / 3.731 | R | `ClamSpot &661474879` |
| Clam 10 | -317.728, -1.470 | 0.846 / 0.845 | -3.046 / -0.846 / 1.354 | -3.045 / -0.845 / 1.355 | - | `ClamSpot &184932393` |
| Clam 11 | -306.695, 12.810 | -1.029 / -1.084 | -1.171 / 1.029 / 3.229 | -1.116 / 1.084 / 3.284 | - | `ClamSpot &355643016` |
| Clam 12 | -306.681, -11.555 | -0.734 / -0.532 | -1.466 / 0.734 / 2.934 | -1.668 / 0.532 / 2.732 | R | `ClamSpot &309393645` |
| Clam 13 | -303.183, -0.811 | 0.870 / 0.869 | -3.070 / -0.870 / 1.330 | -3.069 / -0.869 / 1.331 | - | `ClamSpot &1861089965` |
| Clam 14 | -297.427, -12.908 | -1.053 / -1.268 | -1.147 / 1.053 / 3.253 | -0.932 / 1.268 / 3.468 | - | `ClamSpot &123980069` |
| Clam 15 | -292.011, 13.258 | -1.137 / -1.197 | -1.063 / 1.137 / 3.337 | -1.003 / 1.197 / 3.397 | - | `ClamSpot &1190636141` |
| Clam 16 | -288.532, -1.618 | 0.839 / 0.838 | -3.039 / -0.839 / 1.361 | -3.038 / -0.838 / 1.362 | - | `ClamSpot &1808983655` |
| Clam 17 | -282.645, 12.969 | -1.067 / -1.139 | -1.133 / 1.067 / 3.267 | -1.061 / 1.139 / 3.339 | - | `ClamSpot &1307319436` |
| Clam 18 | -282.434, -12.776 | -1.021 / -1.299 | -1.179 / 1.021 / 3.221 | -0.901 / 1.299 / 3.499 | - | `ClamSpot &883215601` |
| Clam 19 | -274.851, -2.060 | 0.814 / 0.813 | -3.014 / -0.814 / 1.386 | -3.013 / -0.813 / 1.387 | - | `ClamSpot &1390925659` |
| Clam 20 | -268.311, -11.510 | -0.724 / -0.992 | -1.476 / 0.724 / 2.924 | -1.208 / 0.992 / 3.192 | - | `ClamSpot &686512453` |
| Clam 21 | -264.134, -2.531 | 0.782 / 0.781 | -2.982 / -0.782 / 1.418 | -2.981 / -0.781 / 1.419 | - | `ClamSpot &2139305158` |
| Clam 22 | -253.173, 1.666 | 0.837 / 0.836 | -3.037 / -0.837 / 1.363 | -3.036 / -0.836 / 1.364 | - | `ClamSpot &807794232` |
| Clam 23 | -240.987, 11.410 | -0.701 / -0.701 | -1.499 / 0.701 / 2.901 | -1.499 / 0.701 / 2.901 | - | `ClamSpot &1363395923` |
| Clam 24 | -239.302, 2.689 | -0.220 / -0.220 | -1.980 / 0.220 / 2.420 | -1.980 / 0.220 / 2.420 | - | `ClamSpot &1716923698` |
| Clam 25 | -233.787, -11.965 | -0.829 / -0.830 | -1.371 / 0.829 / 3.029 | -1.370 / 0.830 / 3.030 | - | `ClamSpot &1240830714` |
| Clam 26 | -227.055, 11.695 | -0.767 / -0.767 | -1.433 / 0.767 / 2.967 | -1.433 / 0.767 / 2.967 | - | `ClamSpot &132520093` |
| Clam 27 | -225.799, -0.889 | 0.251 / 0.250 | -2.451 / -0.251 / 1.949 | -2.450 / -0.250 / 1.950 | - | `ClamSpot &28690241` |
| Clam 28 | -221.649, -12.675 | -0.997 / -0.997 | -1.203 / 0.997 / 3.197 | -1.203 / 0.997 / 3.197 | - | `ClamSpot &1023972396` |
| Clam 29 | -210.540, 11.707 | -0.769 / -0.665 | -1.431 / 0.769 / 2.969 | -1.535 / 0.665 / 2.865 | R | `ClamSpot &777144933` |
| Clam 30 | -210.344, 1.571 | 0.841 / 0.840 | -3.041 / -0.841 / 1.359 | -3.040 / -0.840 / 1.360 | - | `ClamSpot &518932795` |
| Clam 31 | -199.107, -0.755 | 0.871 / 0.871 | -3.071 / -0.871 / 1.329 | -3.071 / -0.871 / 1.329 | - | `ClamSpot &2131547044` |
| Clam 32 | -197.098, -15.844 | -1.766 / -1.713 | -0.434 / 1.766 / 3.966 | -0.487 / 1.713 / 3.913 | R | `ClamSpot &973576790` |
| Clam 33 | -182.694, -3.621 | 0.684 / 0.683 | -2.884 / -0.684 / 1.516 | -2.883 / -0.683 / 1.517 | - | `ClamSpot &1977137818` |
| Clam 34 | -177.861, 10.252 | -0.440 / -0.442 | -1.760 / 0.440 / 2.640 | -1.758 / 0.442 / 2.642 | - | `ClamSpot &28487289` |
| Clam 35 | -169.321, -3.071 | 0.737 / 0.507 | -2.937 / -0.737 / 1.463 | -2.707 / -0.507 / 1.693 | - | `ClamSpot &927875087` |
| Clam 36 | -165.438, 15.602 | -1.707 / -1.707 | -0.493 / 1.707 / 3.907 | -0.493 / 1.707 / 3.907 | - | `ClamSpot &378771896` |
| Clam 37 | -155.922, 12.342 | -0.918 / -1.109 | -1.282 / 0.918 / 3.118 | -1.091 / 1.109 / 3.309 | - | `ClamSpot &306607950` |
| Clam 38 | -152.901, -13.959 | -1.306 / -1.226 | -0.894 / 1.306 / 3.506 | -0.974 / 1.226 / 3.426 | R | `ClamSpot &864613091` |
| Clam 39 | -149.718, -2.941 | 0.749 / 0.748 | -2.949 / -0.749 / 1.451 | -2.948 / -0.748 / 1.452 | - | `ClamSpot &1251969721` |
| Clam 40 | -143.043, 8.638 | -0.101 / -0.343 | -2.099 / 0.101 / 2.301 | -1.857 / 0.343 / 2.543 | - | `ClamSpot &1169405517` |
| Clam 41 | -140.747, 2.381 | 0.793 / 0.792 | -2.993 / -0.793 / 1.407 | -2.992 / -0.792 / 1.408 | - | `ClamSpot &1611874234` |
| Clam 42 | -134.718, -12.080 | -0.856 / -0.952 | -1.344 / 0.856 / 3.056 | -1.248 / 0.952 / 3.152 | - | `ClamSpot &98835277` |
| Clam 43 | -124.631, 11.050 | -0.618 / -0.698 | -1.582 / 0.618 / 2.818 | -1.502 / 0.698 / 2.898 | - | `ClamSpot &518483025` |
| Clam 44 | -124.318, -11.753 | -0.780 / -0.926 | -1.420 / 0.780 / 2.980 | -1.274 / 0.926 / 3.126 | - | `ClamSpot &1627318634` |
| Clam 45 | -121.171, -4.936 | 0.527 / 0.527 | -2.727 / -0.527 / 1.673 | -2.727 / -0.527 / 1.673 | - | `ClamSpot &1635811915` |
| Clam 46 | -114.800, 44.638 | -4.000 / -1.789 | 1.800 / 4.000 / 6.200 | -0.411 / 1.789 / 3.989 | D R | `ClamSpot &1408179497` |
| Clam 47 | -111.190, 11.705 | -0.769 / -0.769 | -1.431 / 0.769 / 2.969 | -1.431 / 0.769 / 2.969 | - | `ClamSpot &1601949837` |
| Clam 48 | -109.063, -3.520 | 0.694 / 0.693 | -2.894 / -0.694 / 1.506 | -2.893 / -0.693 / 1.507 | - | `ClamSpot &108295071` |
| Clam 49 | -101.235, -12.445 | -0.943 / -0.943 | -1.257 / 0.943 / 3.143 | -1.257 / 0.943 / 3.143 | - | `ClamSpot &1886514471` |
| Clam 50 | -100.632, 15.290 | -1.631 / -1.631 | -0.569 / 1.631 / 3.831 | -0.569 / 1.631 / 3.831 | - | `ClamSpot &1852237319` |
| Clam 51 | -99.227, 0.535 | 0.875 / 0.597 | -3.075 / -0.875 / 1.325 | -2.797 / -0.597 / 1.603 | - | `ClamSpot &1731196048` |
| Clam 52 | -95.757, 43.131 | -3.797 / -1.349 | 1.597 / 3.797 / 5.997 | -0.851 / 1.349 / 3.549 | D R | `ClamSpot &420096345` |
| Clam 53 | -95.493, 30.195 | -2.452 / -1.268 | 0.252 / 2.452 / 4.652 | -0.932 / 1.268 / 3.468 | D R | `ClamSpot &313040599` |
| Clam 54 | -95.082, 51.042 | -4.000 / -1.424 | 1.800 / 4.000 / 6.200 | -0.776 / 1.424 / 3.624 | D R | `ClamSpot &115657243` |
| Clam 55 | -86.987, -13.644 | -1.230 / -1.230 | -0.970 / 1.230 / 3.430 | -0.970 / 1.230 / 3.430 | - | `ClamSpot &185898534` |
| Clam 56 | -85.394, -5.466 | 0.453 / 0.452 | -2.653 / -0.453 / 1.747 | -2.652 / -0.452 / 1.748 | - | `ClamSpot &2091740314` |
| Clam 57 | -85.177, 10.094 | -0.406 / -0.406 | -1.794 / 0.406 / 2.606 | -1.794 / 0.406 / 2.606 | - | `ClamSpot &1287175968` |
| Clam 58 | -84.637, 58.411 | -3.989 / -1.354 | 1.789 / 3.989 / 6.189 | -0.846 / 1.354 / 3.554 | D R | `ClamSpot &737447919` |
| Clam 59 | -83.629, 39.372 | -1.633 / -1.194 | -0.567 / 1.633 / 3.833 | -1.006 / 1.194 / 3.394 | R | `ClamSpot &1620245390` |
| Clam 60 | -80.620, 26.557 | -1.388 / -1.092 | -0.812 / 1.388 / 3.588 | -1.108 / 1.092 / 3.292 | R | `ClamSpot &1466322473` |
| Clam 61 | -73.244, 29.588 | -1.234 / -1.018 | -0.966 / 1.234 / 3.434 | -1.182 / 1.018 / 3.218 | R | `ClamSpot &223466324` |
| Clam 62 | -70.062, -24.867 | -2.115 / -1.176 | -0.085 / 2.115 / 4.315 | -1.024 / 1.176 / 3.376 | D R | `ClamSpot &1872369689` |
| Clam 63 | -69.476, -14.572 | -1.007 / -0.886 | -1.193 / 1.007 / 3.207 | -1.314 / 0.886 / 3.086 | R | `ClamSpot &1675263990` |
| Clam 64 | -68.670, -4.023 | 0.640 / 0.640 | -2.840 / -0.640 / 1.560 | -2.840 / -0.640 / 1.560 | - | `ClamSpot &745989743` |
| Clam 65 | -68.662, 55.497 | -1.668 / -1.126 | -0.532 / 1.668 / 3.868 | -1.074 / 1.126 / 3.326 | D R | `ClamSpot &1224687031` |
| Clam 66 | -68.573, 10.040 | -0.394 / -0.394 | -1.806 / 0.394 / 2.594 | -1.806 / 0.394 / 2.594 | - | `ClamSpot &500515531` |
| Clam 67 | -68.444, -43.654 | -4.000 / -1.545 | 1.800 / 4.000 / 6.200 | -0.655 / 1.545 / 3.745 | D R | `ClamSpot &1909942069` |
| Clam 68 | -66.564, 45.208 | -1.380 / -1.042 | -0.820 / 1.380 / 3.580 | -1.158 / 1.042 / 3.242 | R | `ClamSpot &714915820` |
| Clam 69 | -59.001, -41.835 | -4.000 / -1.359 | 1.800 / 4.000 / 6.200 | -0.841 / 1.359 / 3.559 | D R | `ClamSpot &1162165983` |
| Clam 70 | -56.692, -16.928 | 2.514 / 1.353 | -4.714 / -2.514 / -0.314 | -3.553 / -1.353 / 0.847 | D  | `ClamSpot &2036407633` |
| Clam 71 | -54.476, 45.235 | -1.087 / -0.758 | -1.113 / 1.087 / 3.287 | -1.442 / 0.758 / 2.958 | R | `ClamSpot &703710060` |
| Clam 72 | -51.616, -24.671 | -1.643 / -1.741 | -0.557 / 1.643 / 3.843 | -0.459 / 1.741 / 3.941 | - | `ClamSpot &443632829` |
| Clam 73 | -50.908, 53.650 | -1.232 / -0.928 | -0.968 / 1.232 / 3.432 | -1.272 / 0.928 / 3.128 | R | `ClamSpot &1574435538` |
| Clam 74 | -44.593, 44.242 | -0.680 / -0.182 | -1.520 / 0.680 / 2.880 | -2.018 / 0.182 / 2.382 | R | `ClamSpot &220987118` |
| Clam 75 | -39.152, 56.937 | -1.082 / -0.848 | -1.118 / 1.082 / 3.282 | -1.352 / 0.848 / 3.048 | R | `ClamSpot &2147064138` |
| Clam 76 | -25.574, 52.783 | 1.660 / 1.661 | -3.860 / -1.660 / 0.540 | -3.861 / -1.661 / 0.539 | - | `ClamSpot &2094076016` |

### Every new bottom-contact case at named samples

Hull groups below include **all** Defs with that draft (see the fleet table), whether or not those boats are spawned here. Shortfall = draft minus painted depth, using unrounded values; zero means touching. Existing shallow water in both models is not a new contact. No extra under-keel allowance is imposed.

| Sample | Tide | Draft group | A clearance | P shortfall |
|---|---|---:|---:|---:|
| wharf.st_peters.bollard_1 | lowest | 1.75 | 0.004 | 0.035 |
| Clam 04 | mean | 0.85 | 0.093 | 0.030 |
| Clam 06 | mean | 0.50 | 0.222 | 0.039 |
| Clam 06 | mean | 0.55 | 0.172 | 0.089 |
| Clam 06 | highest | 2.75 | 0.172 | 0.089 |
| Clam 06 | highest | 2.90 | 0.022 | 0.239 |
| Clam 09 | mean | 1.60 | 0.202 | 0.069 |
| Clam 09 | mean | 1.75 | 0.052 | 0.219 |
| Clam 09 | mean | 1.80 | 0.002 | 0.269 |
| Clam 12 | mean | 0.55 | 0.184 | 0.018 |
| Clam 12 | highest | 2.75 | 0.184 | 0.018 |
| Clam 12 | highest | 2.90 | 0.034 | 0.168 |
| Clam 29 | highest | 2.90 | 0.069 | 0.035 |
| Clam 29 | highest | 2.95 | 0.019 | 0.085 |
| Clam 32 | mean | 1.75 | 0.016 | 0.037 |
| Clam 38 | mean | 1.30 | 0.006 | 0.074 |
| Clam 46 | lowest | 0.30 | 1.500 | 0.711 |
| Clam 46 | lowest | 0.50 | 1.300 | 0.911 |
| Clam 46 | lowest | 0.55 | 1.250 | 0.961 |
| Clam 46 | lowest | 0.80 | 1.000 | 1.211 |
| Clam 46 | lowest | 0.85 | 0.950 | 1.261 |
| Clam 46 | lowest | 0.95 | 0.850 | 1.361 |
| Clam 46 | lowest | 1.15 | 0.650 | 1.561 |
| Clam 46 | lowest | 1.30 | 0.500 | 1.711 |
| Clam 46 | lowest | 1.40 | 0.400 | 1.811 |
| Clam 46 | lowest | 1.45 | 0.350 | 1.861 |
| Clam 46 | lowest | 1.60 | 0.200 | 2.011 |
| Clam 46 | lowest | 1.75 | 0.050 | 2.161 |
| Clam 46 | lowest | 1.80 | -0.000 | 2.211 |
| Clam 46 | mean | 1.80 | 2.200 | 0.011 |
| Clam 46 | mean | 1.90 | 2.100 | 0.111 |
| Clam 46 | mean | 2.75 | 1.250 | 0.961 |
| Clam 46 | mean | 2.90 | 1.100 | 1.111 |
| Clam 46 | mean | 2.95 | 1.050 | 1.161 |
| Clam 46 | highest | 4.20 | 2.000 | 0.211 |
| Clam 46 | highest | 4.30 | 1.900 | 0.311 |
| Clam 46 | highest | 4.60 | 1.600 | 0.611 |
| Clam 46 | highest | 5.00 | 1.200 | 1.011 |
| Clam 52 | lowest | 0.30 | 1.297 | 1.151 |
| Clam 52 | lowest | 0.50 | 1.097 | 1.351 |
| Clam 52 | lowest | 0.55 | 1.047 | 1.401 |
| Clam 52 | lowest | 0.80 | 0.797 | 1.651 |
| Clam 52 | lowest | 0.85 | 0.747 | 1.701 |
| Clam 52 | lowest | 0.95 | 0.647 | 1.801 |
| Clam 52 | lowest | 1.15 | 0.447 | 2.001 |
| Clam 52 | lowest | 1.30 | 0.297 | 2.151 |
| Clam 52 | lowest | 1.40 | 0.197 | 2.251 |
| Clam 52 | lowest | 1.45 | 0.147 | 2.301 |
| Clam 52 | mean | 1.40 | 2.397 | 0.051 |
| Clam 52 | mean | 1.45 | 2.347 | 0.101 |
| Clam 52 | mean | 1.60 | 2.197 | 0.251 |
| Clam 52 | mean | 1.75 | 2.047 | 0.401 |
| Clam 52 | mean | 1.80 | 1.997 | 0.451 |
| Clam 52 | mean | 1.90 | 1.897 | 0.551 |
| Clam 52 | mean | 2.75 | 1.047 | 1.401 |
| Clam 52 | mean | 2.90 | 0.897 | 1.551 |
| Clam 52 | mean | 2.95 | 0.847 | 1.601 |
| Clam 52 | highest | 4.20 | 1.797 | 0.651 |
| Clam 52 | highest | 4.30 | 1.697 | 0.751 |
| Clam 52 | highest | 4.60 | 1.397 | 1.051 |
| Clam 52 | highest | 5.00 | 0.997 | 1.451 |
| Clam 53 | mean | 1.30 | 1.152 | 0.032 |
| Clam 53 | mean | 1.40 | 1.052 | 0.132 |
| Clam 53 | mean | 1.45 | 1.002 | 0.182 |
| Clam 53 | mean | 1.60 | 0.852 | 0.332 |
| Clam 53 | mean | 1.75 | 0.702 | 0.482 |
| Clam 53 | mean | 1.80 | 0.652 | 0.532 |
| Clam 53 | mean | 1.90 | 0.552 | 0.632 |
| Clam 53 | highest | 4.20 | 0.452 | 0.732 |
| Clam 53 | highest | 4.30 | 0.352 | 0.832 |
| Clam 53 | highest | 4.60 | 0.052 | 1.132 |
| Clam 54 | lowest | 0.30 | 1.500 | 1.076 |
| Clam 54 | lowest | 0.50 | 1.300 | 1.276 |
| Clam 54 | lowest | 0.55 | 1.250 | 1.326 |
| Clam 54 | lowest | 0.80 | 1.000 | 1.576 |
| Clam 54 | lowest | 0.85 | 0.950 | 1.626 |
| Clam 54 | lowest | 0.95 | 0.850 | 1.726 |
| Clam 54 | lowest | 1.15 | 0.650 | 1.926 |
| Clam 54 | lowest | 1.30 | 0.500 | 2.076 |
| Clam 54 | lowest | 1.40 | 0.400 | 2.176 |
| Clam 54 | lowest | 1.45 | 0.350 | 2.226 |
| Clam 54 | lowest | 1.60 | 0.200 | 2.376 |
| Clam 54 | lowest | 1.75 | 0.050 | 2.526 |
| Clam 54 | lowest | 1.80 | -0.000 | 2.576 |
| Clam 54 | mean | 1.45 | 2.550 | 0.026 |
| Clam 54 | mean | 1.60 | 2.400 | 0.176 |
| Clam 54 | mean | 1.75 | 2.250 | 0.326 |
| Clam 54 | mean | 1.80 | 2.200 | 0.376 |
| Clam 54 | mean | 1.90 | 2.100 | 0.476 |
| Clam 54 | mean | 2.75 | 1.250 | 1.326 |
| Clam 54 | mean | 2.90 | 1.100 | 1.476 |
| Clam 54 | mean | 2.95 | 1.050 | 1.526 |
| Clam 54 | highest | 4.20 | 2.000 | 0.576 |
| Clam 54 | highest | 4.30 | 1.900 | 0.676 |
| Clam 54 | highest | 4.60 | 1.600 | 0.976 |
| Clam 54 | highest | 5.00 | 1.200 | 1.376 |
| Clam 58 | lowest | 0.30 | 1.489 | 1.146 |
| Clam 58 | lowest | 0.50 | 1.289 | 1.346 |
| Clam 58 | lowest | 0.55 | 1.239 | 1.396 |
| Clam 58 | lowest | 0.80 | 0.989 | 1.646 |
| Clam 58 | lowest | 0.85 | 0.939 | 1.696 |
| Clam 58 | lowest | 0.95 | 0.839 | 1.796 |
| Clam 58 | lowest | 1.15 | 0.639 | 1.996 |
| Clam 58 | lowest | 1.30 | 0.489 | 2.146 |
| Clam 58 | lowest | 1.40 | 0.389 | 2.246 |
| Clam 58 | lowest | 1.45 | 0.339 | 2.296 |
| Clam 58 | lowest | 1.60 | 0.189 | 2.446 |
| Clam 58 | lowest | 1.75 | 0.039 | 2.596 |
| Clam 58 | mean | 1.40 | 2.589 | 0.046 |
| Clam 58 | mean | 1.45 | 2.539 | 0.096 |
| Clam 58 | mean | 1.60 | 2.389 | 0.246 |
| Clam 58 | mean | 1.75 | 2.239 | 0.396 |
| Clam 58 | mean | 1.80 | 2.189 | 0.446 |
| Clam 58 | mean | 1.90 | 2.089 | 0.546 |
| Clam 58 | mean | 2.75 | 1.239 | 1.396 |
| Clam 58 | mean | 2.90 | 1.089 | 1.546 |
| Clam 58 | mean | 2.95 | 1.039 | 1.596 |
| Clam 58 | highest | 4.20 | 1.989 | 0.646 |
| Clam 58 | highest | 4.30 | 1.889 | 0.746 |
| Clam 58 | highest | 4.60 | 1.589 | 1.046 |
| Clam 58 | highest | 5.00 | 1.189 | 1.446 |
| Clam 59 | mean | 1.30 | 0.333 | 0.106 |
| Clam 59 | mean | 1.40 | 0.233 | 0.206 |
| Clam 59 | mean | 1.45 | 0.183 | 0.256 |
| Clam 59 | mean | 1.60 | 0.033 | 0.406 |
| Clam 60 | mean | 1.15 | 0.238 | 0.058 |
| Clam 60 | mean | 1.30 | 0.088 | 0.208 |
| Clam 61 | mean | 1.15 | 0.084 | 0.132 |
| Clam 62 | mean | 1.30 | 0.815 | 0.124 |
| Clam 62 | mean | 1.40 | 0.715 | 0.224 |
| Clam 62 | mean | 1.45 | 0.665 | 0.274 |
| Clam 62 | mean | 1.60 | 0.515 | 0.424 |
| Clam 62 | mean | 1.75 | 0.365 | 0.574 |
| Clam 62 | mean | 1.80 | 0.315 | 0.624 |
| Clam 62 | mean | 1.90 | 0.215 | 0.724 |
| Clam 62 | highest | 4.20 | 0.115 | 0.824 |
| Clam 62 | highest | 4.30 | 0.015 | 0.924 |
| Clam 63 | mean | 0.95 | 0.057 | 0.064 |
| Clam 65 | mean | 1.15 | 0.518 | 0.024 |
| Clam 65 | mean | 1.30 | 0.368 | 0.174 |
| Clam 65 | mean | 1.40 | 0.268 | 0.274 |
| Clam 65 | mean | 1.45 | 0.218 | 0.324 |
| Clam 65 | mean | 1.60 | 0.068 | 0.474 |
| Clam 67 | lowest | 0.30 | 1.500 | 0.955 |
| Clam 67 | lowest | 0.50 | 1.300 | 1.155 |
| Clam 67 | lowest | 0.55 | 1.250 | 1.205 |
| Clam 67 | lowest | 0.80 | 1.000 | 1.455 |
| Clam 67 | lowest | 0.85 | 0.950 | 1.505 |
| Clam 67 | lowest | 0.95 | 0.850 | 1.605 |
| Clam 67 | lowest | 1.15 | 0.650 | 1.805 |
| Clam 67 | lowest | 1.30 | 0.500 | 1.955 |
| Clam 67 | lowest | 1.40 | 0.400 | 2.055 |
| Clam 67 | lowest | 1.45 | 0.350 | 2.105 |
| Clam 67 | lowest | 1.60 | 0.200 | 2.255 |
| Clam 67 | lowest | 1.75 | 0.050 | 2.405 |
| Clam 67 | lowest | 1.80 | -0.000 | 2.455 |
| Clam 67 | mean | 1.60 | 2.400 | 0.055 |
| Clam 67 | mean | 1.75 | 2.250 | 0.205 |
| Clam 67 | mean | 1.80 | 2.200 | 0.255 |
| Clam 67 | mean | 1.90 | 2.100 | 0.355 |
| Clam 67 | mean | 2.75 | 1.250 | 1.205 |
| Clam 67 | mean | 2.90 | 1.100 | 1.355 |
| Clam 67 | mean | 2.95 | 1.050 | 1.405 |
| Clam 67 | highest | 4.20 | 2.000 | 0.455 |
| Clam 67 | highest | 4.30 | 1.900 | 0.555 |
| Clam 67 | highest | 4.60 | 1.600 | 0.855 |
| Clam 67 | highest | 5.00 | 1.200 | 1.255 |
| Clam 68 | mean | 1.15 | 0.230 | 0.108 |
| Clam 68 | mean | 1.30 | 0.080 | 0.258 |
| Clam 69 | lowest | 0.30 | 1.500 | 1.141 |
| Clam 69 | lowest | 0.50 | 1.300 | 1.341 |
| Clam 69 | lowest | 0.55 | 1.250 | 1.391 |
| Clam 69 | lowest | 0.80 | 1.000 | 1.641 |
| Clam 69 | lowest | 0.85 | 0.950 | 1.691 |
| Clam 69 | lowest | 0.95 | 0.850 | 1.791 |
| Clam 69 | lowest | 1.15 | 0.650 | 1.991 |
| Clam 69 | lowest | 1.30 | 0.500 | 2.141 |
| Clam 69 | lowest | 1.40 | 0.400 | 2.241 |
| Clam 69 | lowest | 1.45 | 0.350 | 2.291 |
| Clam 69 | lowest | 1.60 | 0.200 | 2.441 |
| Clam 69 | lowest | 1.75 | 0.050 | 2.591 |
| Clam 69 | lowest | 1.80 | -0.000 | 2.641 |
| Clam 69 | mean | 1.40 | 2.600 | 0.041 |
| Clam 69 | mean | 1.45 | 2.550 | 0.091 |
| Clam 69 | mean | 1.60 | 2.400 | 0.241 |
| Clam 69 | mean | 1.75 | 2.250 | 0.391 |
| Clam 69 | mean | 1.80 | 2.200 | 0.441 |
| Clam 69 | mean | 1.90 | 2.100 | 0.541 |
| Clam 69 | mean | 2.75 | 1.250 | 1.391 |
| Clam 69 | mean | 2.90 | 1.100 | 1.541 |
| Clam 69 | mean | 2.95 | 1.050 | 1.591 |
| Clam 69 | highest | 4.20 | 2.000 | 0.641 |
| Clam 69 | highest | 4.30 | 1.900 | 0.741 |
| Clam 69 | highest | 4.60 | 1.600 | 1.041 |
| Clam 69 | highest | 5.00 | 1.200 | 1.441 |
| Clam 71 | mean | 0.80 | 0.287 | 0.042 |
| Clam 71 | mean | 0.85 | 0.237 | 0.092 |
| Clam 71 | mean | 0.95 | 0.137 | 0.192 |
| Clam 73 | mean | 0.95 | 0.282 | 0.022 |
| Clam 73 | mean | 1.15 | 0.082 | 0.222 |
| Clam 74 | mean | 0.30 | 0.380 | 0.118 |
| Clam 74 | mean | 0.50 | 0.180 | 0.318 |
| Clam 74 | mean | 0.55 | 0.130 | 0.368 |
| Clam 74 | highest | 2.75 | 0.130 | 0.368 |
| Clam 75 | mean | 0.85 | 0.232 | 0.002 |
| Clam 75 | mean | 0.95 | 0.132 | 0.102 |

### Face, route and berthed-footprint sweeps

Spacing at most 0.1 m, endpoints included. A and P maxima may occur at different positions. These are finite samples, not collision or reachability proofs; route turns and the moving hull envelope are not certified.

| Sweep | Samples | Highest bed A / P | Minimum depths A; P (L / M / H) | XY worst A; worst P | Max absolute discrepancy (signed P-A) |
|---|---:|---|---|---|---:|
| Pier south edge | 306 | 5.903 / 5.887 | -8.103 / -5.903 / -3.703; -8.087 / -5.887 / -3.687 | (183.000, 2.533); (183.000, 2.533) | 0.039 |
| Pier north edge | 306 | 6.000 / 6.000 | -8.200 / -6.000 / -3.800; -8.200 / -6.000 / -3.800 | (183.000, 6.252); (183.000, 6.252) | 0.060 |
| Pier west edge | 39 | 6.000 / 6.000 | -8.200 / -6.000 / -3.800; -8.200 / -6.000 / -3.800 | (183.000, 3.904); (183.000, 4.784) | -0.018 |
| Pier east edge | 39 | -3.826 / -3.841 | 1.626 / 3.826 / 6.026; 1.641 / 3.841 / 6.041 | (213.500, 4.882); (213.500, 4.784) | -0.032 |
| Arrival centreline leg 0-1 | 794 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (340.000, 40.000); (340.000, 40.000) | 0.000 |
| Arrival centreline leg 1-2 | 271 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (262.000, 26.000); (262.000, 26.000) | 0.000 |
| Arrival centreline leg 2-3 | 442 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (255.000, 0.000); (255.000, 0.000) | 0.000 |
| Arrival continuation entry to waypoint 1 | 559 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (316.000, 40.000); (316.000, 40.000) | 0.000 |
| Dory berthed rectangle | 828 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (210.750, 6.652); (210.750, 6.652) | 0.000 |
| Arrival berthed rectangle | 6370 | -4.000 / -4.000 | 1.800 / 4.000 / 6.200; 1.800 / 4.000 / 6.200 | (204.550, -2.667); (204.550, -2.667) | 0.000 |

New contacts found between named samples, grouped by sweep, tide and draft; the coordinate is the greatest shortfall in that group. Counts include shared endpoints. Tie-off and pier-edge coordinates screen depth at the structure, not an assigned keel.

| Sweep | Water level | Draft group | A clearance | P shortfall | Worst XY | Contact samples |
|---|---:|---:|---:|---:|---|---:|
| Pier south edge | -2.20 | 0.95 | 0.002 | 0.026 | (201.400, 2.533) | 1 |
| Pier south edge | -2.20 | 1.15 | 0.003 | 0.025 | (201.600, 2.533) | 1 |
| Pier south edge | -2.20 | 1.40 | 0.010 | 0.022 | (201.900, 2.533) | 1 |
| Pier south edge | -2.20 | 1.45 | 0.032 | 0.004 | (202.000, 2.533) | 1 |
| Pier south edge | -2.20 | 1.60 | 0.006 | 0.016 | (202.200, 2.533) | 1 |
| Pier south edge | -2.20 | 1.75 | 0.014 | 0.021 | (202.600, 2.533) | 1 |
| Pier south edge | -2.20 | 1.80 | -0.000 | 0.012 | (203.000, 2.533) | 3 |
| Pier south edge | 0.00 | 1.90 | 0.007 | 0.007 | (200.400, 2.533) | 1 |
| Pier south edge | 2.20 | 0.85 | 0.002 | 0.009 | (188.200, 2.533) | 1 |
| Pier south edge | 2.20 | 1.30 | 0.018 | 0.002 | (189.000, 2.533) | 1 |
| Pier south edge | 2.20 | 1.45 | 0.008 | 0.011 | (189.400, 2.533) | 1 |
| Pier south edge | 2.20 | 1.60 | 0.003 | 0.003 | (192.300, 2.533) | 1 |
| Pier south edge | 2.20 | 1.75 | 0.001 | 0.005 | (194.400, 2.533) | 1 |
| Pier south edge | 2.20 | 1.80 | 0.005 | 0.000 | (195.000, 2.533) | 1 |
| Pier south edge | 2.20 | 5.00 | 0.013 | 0.010 | (201.100, 2.533) | 1 |
| Pier north edge | -2.20 | 1.30 | 0.006 | 0.003 | (208.700, 6.252) | 1 |
| Pier north edge | -2.20 | 1.60 | 0.034 | 0.026 | (209.000, 6.252) | 1 |
| Pier north edge | -2.20 | 1.75 | 0.011 | 0.012 | (209.200, 6.252) | 1 |
| Pier north edge | -2.20 | 1.80 | -0.000 | 0.015 | (209.400, 6.252) | 4 |
| Pier east edge | -2.20 | 1.75 | 0.017 | 0.011 | (213.500, 5.469) | 1 |
| Pier east edge | -2.20 | 1.80 | -0.000 | 0.018 | (213.500, 5.665) | 3 |

### Hull drafts from Defs

| Hull ID | Draft (m) | Source |
|---|---:|---|
| `boat.bowrider` | 0.95 | `Assets/_Project/Data/Boats/Bowrider.asset` |
| `boat.cape_islander` | 1.40 | `Assets/_Project/Data/Boats/CapeIslander.asset` |
| `boat.coastal_packet` | 5.00 | `Assets/_Project/Data/Boats/CoastalPacket.asset` |
| `boat.console_skiff` | 0.55 | `Assets/_Project/Data/Boats/ConsoleSkiff.asset` |
| `boat.dory` | 0.30 | `Assets/_Project/Data/Boats/Dory.asset` |
| `boat.dory_outboard` | 0.30 | `Assets/_Project/Data/Boats/DoryOutboard.asset` |
| `boat.expedition_yacht_120` | 2.75 | `Assets/_Project/Data/Boats/ExpeditionYacht120.asset` |
| `boat.express_cruiser` | 1.60 | `Assets/_Project/Data/Boats/ExpressCruiser.asset` |
| `boat.go_fast` | 1.45 | `Assets/_Project/Data/Boats/GoFast.asset` |
| `boat.jet_ski` | 0.80 | `Assets/_Project/Data/Boats/JetSki.asset` |
| `boat.lobster_boat` | 1.30 | `Assets/_Project/Data/Boats/LobsterBoat.asset` |
| `boat.lobster_inshore_hardtop_fundy` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreHardtopFundy.asset` |
| `boat.lobster_inshore_hardtop_newfoundland` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreHardtopNewfoundland.asset` |
| `boat.lobster_inshore_hardtop_northumberland` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreHardtopNorthumberland.asset` |
| `boat.lobster_inshore_open_fundy` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreOpenFundy.asset` |
| `boat.lobster_inshore_open_newfoundland` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreOpenNewfoundland.asset` |
| `boat.lobster_inshore_open_northumberland` | 1.15 | `Assets/_Project/Data/Boats/LobsterInshoreOpenNorthumberland.asset` |
| `boat.lobster_offshore_hardtop_fundy` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreHardtopFundy.asset` |
| `boat.lobster_offshore_hardtop_newfoundland` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreHardtopNewfoundland.asset` |
| `boat.lobster_offshore_hardtop_northumberland` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreHardtopNorthumberland.asset` |
| `boat.lobster_offshore_open_fundy` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreOpenFundy.asset` |
| `boat.lobster_offshore_open_newfoundland` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreOpenNewfoundland.asset` |
| `boat.lobster_offshore_open_northumberland` | 1.45 | `Assets/_Project/Data/Boats/LobsterOffshoreOpenNorthumberland.asset` |
| `boat.lobster_standard_hardtop_fundy` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardHardtopFundy.asset` |
| `boat.lobster_standard_hardtop_newfoundland` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardHardtopNewfoundland.asset` |
| `boat.lobster_standard_hardtop_northumberland` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardHardtopNorthumberland.asset` |
| `boat.lobster_standard_open_fundy` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardOpenFundy.asset` |
| `boat.lobster_standard_open_newfoundland` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardOpenNewfoundland.asset` |
| `boat.lobster_standard_open_northumberland` | 1.30 | `Assets/_Project/Data/Boats/LobsterStandardOpenNorthumberland.asset` |
| `boat.motor_yacht_84` | 1.80 | `Assets/_Project/Data/Boats/MotorYacht84.asset` |
| `boat.punt` | 0.50 | `Assets/_Project/Data/Boats/Punt.asset` |
| `boat.punt_upgraded` | 0.55 | `Assets/_Project/Data/Boats/PuntUpgraded.asset` |
| `boat.side_dragger` | 2.90 | `Assets/_Project/Data/Boats/SideDragger.asset` |
| `boat.sloop_30` | 1.90 | `Assets/_Project/Data/Boats/Sloop30.asset` |
| `boat.sloop_88` | 4.60 | `Assets/_Project/Data/Boats/Sloop88.asset` |
| `boat.sport_fisher_convertible` | 1.75 | `Assets/_Project/Data/Boats/SportFisherConvertible.asset` |
| `boat.sport_fisher_skybridge` | 2.95 | `Assets/_Project/Data/Boats/SportFisherSkybridge.asset` |
| `boat.sport_skiff` | 0.50 | `Assets/_Project/Data/Boats/SportSkiff.asset` |
| `boat.sport_skiff_mk2` | 0.95 | `Assets/_Project/Data/Boats/SportSkiffMk2.asset` |
| `boat.sport_skiff_twin` | 0.55 | `Assets/_Project/Data/Boats/SportSkiffTwin.asset` |
| `boat.stern_trawler` | 4.20 | `Assets/_Project/Data/Boats/SternTrawler.asset` |
| `boat.stern_trawler_mk2` | 4.30 | `Assets/_Project/Data/Boats/SternTrawlerMk2.asset` |
| `boat.tanker` | 6.50 | `Assets/_Project/Data/Boats/Tanker.asset` |
| `boat.yacht_tender` | 0.80 | `Assets/_Project/Data/Boats/YachtTender.asset` |
| `boat.zodiac_frc` | 0.80 | `Assets/_Project/Data/Boats/ZodiacFrc.asset` |
| `boat.zodiac_hurricane` | 0.85 | `Assets/_Project/Data/Boats/ZodiacHurricane.asset` |

<!-- END GENERATED SURVEY -->

## Sources, mapping and scope

The survey script is [st_peters_depth_audit.py](../../tools/scene-export/st_peters_depth_audit.py). Run it with Python 3 and Pillow (the R16 decoder dependency), from a checkout containing the base's actual PNG bytes:

```text
python tools/scene-export/st_peters_depth_audit.py
python tools/scene-export/st_peters_depth_audit.py --json
```

The first command reproduces the delimited generated tables; the second emits unrounded values and sweep contact coordinates. Both are read-only and write to stdout. Run against the pinned base plus this audit script. The exported numbers are not runtime data or a proposed replacement terrain implementation.

### Height and tide arithmetic

- [StPetersSeabed.asset](../../Assets/_Project/Data/Terrain/StPetersSeabed.asset) supplies `_worldCenter = (0, 0)`, `_worldSize = (760, 520)`, `_minElevation = -4`, `_maxElevation = 12`. Both the scene's `WaterSurface._paintedHeightTex` and `TerrainSplatSurface._heightTexture` reference this height texture GUID `4b7e2c9a1d6f4e3aa8c50f2b9d71e6c4`.
- The actual PNG is **1520 x 1040, 16-bit greyscale**, not the older R8 format. Its codes decode as `elevation = -4 + 16 * code / 65535`. The quantum is 0.000244144 m. [PaintedHeightMap.ReadNormalizedR / Decode](../../Assets/_Project/Code/World/PaintedHeightMap.cs) reads R16 with the 65535 denominator, and [PaintedHeightField.DecodeElevation](../../Assets/_Project/Code/World/PaintedHeightField.cs) maps the normalized code to the asset range. The PNG meta has `sRGBTexture: 0`, no mipmaps, readable data, clamp wrapping, bilinear filtering, no NPOT scaling, and an uncompressed Standalone R16 override (`textureFormat: 9`, maximum size 2048). Both dimensions fit that limit.
- [PaintedHeightField.ElevationAt](../../Assets/_Project/Code/World/PaintedHeightField.cs) maps `u=(x+380)/760`, `v=(y+260)/520`, then `fx=u*1520-0.5`, `fy=v*1040-0.5`; four adjacent texels are bilinearly interpolated and indices clamp at the edges. PNG rows start at north; the script flips them to Unity's south-first indexing. No 8-bit conversion, nearest-texel approximation, gamma transform, resampling, art projection or height warp is applied. All named samples are inside the map.
- The analytic values port [TidalTerrain.ElevationAtZones](../../Assets/_Project/Code/World/TidalTerrain.cs) and [CoastPlan.BlendAt](../../Assets/_Project/Code/World/CoastPlan.cs), reading every numeric zone field from the committed scene. This includes the coast classes and their 3-degree blends, not merely an ellipse: centre (70,0), radii (120,70), plateau +6 m, falloff 20 m, reef width 25 m, deep floor -4 m; sandbar crest +0.88 m and crossing bed -0.6 m; the -1 m beach slip; and the -4 m approach, south berth pocket and north pocket. Smoothstep, max composition and all downward carves follow the source order. The serialized south pocket is centred at y=-5.8; the actual arrival berth is y=-0.26654807 and remains deep in the approach. Both are listed, without assuming their positions coincide.
- [StPeters RegionDef](../../Assets/_Project/Data/Regions/StPeters.asset) and the scene's `EnvironmentService._activeTideProfile` agree: mean 0 m, amplitude 2.2 m. Lowest / mean / highest here are the **spring-envelope design limits -2.2 / 0 / +2.2 m**, matching the existing berth tests. [TideModel.Height](../../Assets/_Project/Code/Environment/TideModel.cs) multiplies the semidiurnal sine by the spring/neap envelope: these are conservative bounds, not three claimed timestamps or the extrema of one selected day. Wind, waves and surge are not added to this tide datum.
- [BoatCrossing.DepthAt / CanFloat](../../Assets/_Project/Code/Boats/BoatCrossing.cs) uses `depth = tide - elevation` and passes at `depth >= DraughtMeters`; [BoatController](../../Assets/_Project/Code/Boats/BoatController.cs) grounds at strict `<`. This audit additionally calls exact equality “touching” (zero under-keel clearance). A 0.000001 m comparison tolerance resolves decimal/binary equality, notably 1.8 m at lowest water; figures are printed to 0.001 m, and classification uses unrounded values. Cases within the numerical cross-check error of a threshold require runtime confirmation. Still-water levels are excluded because the boat-depth path reads the tidal environment level, not `StillWaterLevels`.

### Inventory and coverage

The script reuses the existing `hhexport.unityyaml` and `hhexport.scene` parsers and transform composition. It first tries the existing R8 decoder, then uses Pillow's lossless 16-bit decode for this PNG. It does not duplicate or change the exporter's R8-only contract.

The whole committed scene was indexed by component type and hierarchy: one `BoatController` (dory), one `ArrivalOpening`, one `StandablePlatform`, five `ShoreCleat` components, and 76 `ClamSpot` components. All behaviours have class identifiers. The scene's 20 prefab instances resolve to seven prefab sources; these contain navigation-buoy, fixture and storage components, with no additional berth, boat route or fishing-spot components. The referenced `ArrivalSea.asset` supplies a lie point and exit target in the separate EastWater region; those coordinates are not St Peters seabed samples. On re-entry, `ArrivalOpening.ContinuationRoute` replaces waypoint 0 with the scene's `RegionAnchor` east-water arrival at (316,40); that point and its leg to waypoint 1 are included. No fixed rod-fishing spots or extra NPC boat routes were found. Rod casts and player-placed traps select positions at runtime, so there is no finite authored waypoint list for them. Regional species declarations are not spatial fishing spots.

`ArrivalOpening._skipper` resolves to `StPetersArrivalSkipper.asset`, whose `Boat` resolves to `CapeIslander.asset`; the dory's `_hull` resolves to `Dory.asset`. The 46 entries in the fleet table are every `DraughtMeters` declaration under the boat data directory. They are screened as hypothetical visitors to each sampled coordinate, not claimed to be spawned in this scene. Draft comes from `BoatHullDef.DraughtMeters`, never the art mesh's `RestingDraftMeters`.

The 113 named rows comprise two occupied berths, twelve endpoints/midpoints of four authored cuts, four NPC route waypoints plus the sea-entry continuation anchor, five tie-offs, twelve face endpoints/midpoints, one on-deck slip head, and all 76 clam spots. The actual serialized platform footprint is x=183..213.5, y=2.533452..6.252202; the survey uses this footprint rather than the old builder cell bounds. Repeated corner coordinates intentionally identify both incident faces. Clam labels sort by x then y and include the component fileID so each row can be located unambiguously.

The two berthed rectangles use Def lengths (dory 4.5 m; Cape Islander 12.9 m) and the existing berth-test beam mirrors in `StPetersBuilder` (half-beams 0.85 and 2.4 m). Both committed headings lie east-west. Their entire rectangles are sampled at at most 0.1 m spacing, conservatively including pointed ends. These are stronger checks than keel-centre readings but remain finite samples. Hull turning envelopes, fender gaps, lateral collision, route reachability, waves, tide timing and every possible anchoring location are outside this audit. Arbitrary large hulls clearing a point are not thereby certified to fit the harbour.

### Verification and existing coverage

Before adding the measurement script, existing coverage was located in `StPetersEastBerthTests.cs`, `StPetersAlongsideBerthTests.cs`, `StPetersDoryBerthTests.cs`, `World/PaintedHeightFieldTests.cs`, `World/PaintedHeightMapDecodeTests.cs` and `PaintedHeightMapR16Tests.cs`. Those tests already pin approach clearance, berth footprints, mapping/orientation and precision. No duplicate game tests or validators were added, and no assertion was changed. This adds a reproducible report alongside the existing scene exporter.

Offline verification completed:

- Five existing `test_export.UnityYamlTests` ran and passed.
- The base's actual C# `TidalTerrain`, `CoastPlan` and `PaintedHeightField` methods were compiled in a scratch .NET harness, using minimal float/vector substitutes for Unity APIs and the serialized scene fields. Across all 113 named samples plus 16,065 offset grid points, the Python analytic calculation differed by at most **0.000030280 m** and the painted calculation by at most **0.000331620 m**. This checks the port and interpolation arithmetic, not Unity import or GPU execution.
- Repeated survey output was identical; generated tables were checked against the document. The PNG signature, 16-bit samples, source hash, mapping, import settings, all 46 draft declarations and the scene/prefab inventory were checked locally.

**Not run:** Unity, EditMode or PlayMode tests, a player build, a GPU/plate comparison, a live import check, or the boats driven on the proposed ground. No editor lease was supplied for this audit. The berth result is an offline depth result, not acceptance of PR 1467 or of any visual change.
