using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum TrawlSessionPhase { GearReady, Shooting, Towing, Hauling, CatchOnDeck, Sorted, Stowed }
    public enum TrawlDoorState { Closed, Spread, Collapsed, Snagged }
    public enum TrawlCrewTask { ShootGear, PayOutWarp, TendTow, RecoverSnag, HaulGear, SortByHand, CarryToHold, MakeHoldRoom, Complete }

    public readonly struct TrawlSessionInput
    {
        public readonly double GameSeconds, SpeedOverGround, DepthUnderKeelMetres, SeaState01;
        public readonly Vector2 NetCentre;
        public TrawlSessionInput(double gameSeconds, double speedOverGround, double depthUnderKeelMetres,
            double seaState01, Vector2 netCentre)
        {
            GameSeconds = gameSeconds; SpeedOverGround = speedOverGround;
            DepthUnderKeelMetres = depthUnderKeelMetres; SeaState01 = seaState01; NetCentre = netCentre;
        }
        internal bool IsValid => TrawlNumbers.NonNegative(GameSeconds) && TrawlNumbers.NonNegative(SpeedOverGround)
            && TrawlNumbers.NonNegative(DepthUnderKeelMetres) && TrawlNumbers.Unit(SeaState01)
            && TrawlNumbers.Finite(NetCentre.x) && TrawlNumbers.Finite(NetCentre.y);
    }

    public readonly struct TrawlGradedLot
    {
        public readonly TrawlCatchLot Catch;
        public readonly int Grade;
        internal TrawlGradedLot(in TrawlCatchLot catchLot, int grade) { Catch = catchLot; Grade = grade; }
    }

    /// <summary>One unwired tow, composing the existing operation, solver and manual deck ledger.
    /// Catch and stow counters are proposals/acknowledgements, never inventory or depletion authority.</summary>
    public sealed class TrawlSession
    {
        private readonly TrawlSessionDef _def;
        private readonly TrawlGearDef _gear;
        private readonly TrawlHandlingDef _handling;
        private readonly TrawlSessionGround _ground;
        private readonly TrawlOperation _operation;
        private readonly ITrawlSurvey _survey;
        private readonly int _catchSeed;
        private readonly double _snagThreshold;
        private readonly string _towId;
        private readonly string[] _targets;
        private readonly TrawlTrackKnot[] _knots;
        private TrawlGradedLot[] _graded;
        private int[] _stowed;
        private int _knotCount;
        private double _lastTime, _shootAt, _towAt, _haulAt, _plannedWarp, _hazard, _chafe;
        private TrawlSessionInput _lastInput;
        private TrawlTowResult _catch;
        private TrawlDeckBatch _deck;

        public TrawlSessionPhase Phase { get; private set; }
        public TrawlDoorState Doors { get; private set; }
        public bool Snagged { get; private set; }
        public bool UnsafeWarp { get; private set; }
        public double WarpOutMetres { get; private set; }
        public double DoorSpreadMetres => Doors == TrawlDoorState.Spread ? _def.DoorSpreadMetres : 0d;
        public double TowGameSeconds => _knotCount < 2 ? 0d : _knots[_knotCount - 1].GameSeconds - _towAt;
        public double SnagRisk01 => 1d - Math.Exp(-_hazard);
        public double ChafeRisk01 => Math.Min(1d, _chafe);
        public double EndIntegrity01 => _catch == null ? _def.InitialIntegrity01 : _catch.EndIntegrity01;
        public TrawlTowStatus? LastSolveStatus { get; private set; }
        public int CatchCount => _catch == null ? 0 : _catch.Count;
        public int BycatchCount => _catch == null ? 0 : _catch.BycatchCount;
        public int LotCount => _graded == null ? 0 : _graded.Length;
        public int StowedCount { get; private set; }
        public int KeptOnDeckCount => _deck == null ? 0 : _deck.KeptOnDeckCount - StowedCount;
        public int ReleasedCount => _deck == null ? 0 : _deck.ReleasedCount;
        public int UnsortedCount => _deck == null ? 0 : _deck.UnsortedCount;
        public TrawlGradedLot LotAt(int index) => _graded[index];
        public int StowedAt(int index) => _stowed[index];
        public TrawlCrewTask TaskWithHoldRoom(double freeHoldKg)
        {
            if (Phase != TrawlSessionPhase.Sorted || KeptOnDeckCount == 0) return CrewTask;
            if (TrawlNumbers.NonNegative(freeHoldKg))
                for (int i = 0; i < _graded.Length; i++)
                    if (_deck.KeptOnDeckAt(i) > _stowed[i] && _catch[i].UnitWeightKg <= freeHoldKg)
                        return TrawlCrewTask.CarryToHold;
            return TrawlCrewTask.MakeHoldRoom;
        }
        public TrawlCrewTask CrewTask
        {
            get
            {
                switch (Phase)
                {
                    case TrawlSessionPhase.GearReady: return TrawlCrewTask.ShootGear;
                    case TrawlSessionPhase.Shooting: return TrawlCrewTask.PayOutWarp;
                    case TrawlSessionPhase.Towing: return Snagged ? TrawlCrewTask.RecoverSnag
                        : UnsafeWarp || _knotCount == _knots.Length || TowGameSeconds >= _gear.MaxTowGameSeconds
                            ? TrawlCrewTask.HaulGear : TrawlCrewTask.TendTow;
                    case TrawlSessionPhase.Hauling: return TrawlCrewTask.HaulGear;
                    case TrawlSessionPhase.CatchOnDeck: return TrawlCrewTask.SortByHand;
                    case TrawlSessionPhase.Sorted: return TrawlCrewTask.CarryToHold;
                    default: return TrawlCrewTask.Complete;
                }
            }
        }

        public TrawlSession(in TrawlSessionDef def, in TrawlGearDef gear, in TrawlHandlingDef handling,
            TrawlSessionGround ground, ITrawlSurvey survey, System.Random seededRandom, string towId, string[] targetSpeciesIds)
        {
            if (!def.IsValid || !gear.IsValid || !handling.IsValid
                || def.WarpDepthRatio < gear.MinWarpDepthRatio || def.WarpDepthRatio > gear.MaxWarpDepthRatio
                || def.DoorSpreadMetres < gear.MinDoorSpreadMetres || def.DoorSpreadMetres > gear.MaxDoorSpreadMetres)
                throw new ArgumentException("Invalid session, gear or handling configuration.");
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (survey == null) throw new ArgumentNullException(nameof(survey));
            if (seededRandom == null) throw new ArgumentNullException(nameof(seededRandom));
            if (string.IsNullOrWhiteSpace(towId)) throw new ArgumentException("Stable tow id required.", nameof(towId));
            if (targetSpeciesIds == null) throw new ArgumentNullException(nameof(targetSpeciesIds));
            _def = def; _def.GradeWeightBreaksKg = (double[])def.GradeWeightBreaksKg.Clone();
            _gear = gear; _handling = handling; _ground = ground; _towId = towId;
            _targets = (string[])targetSpeciesIds.Clone();
            _knots = new TrawlTrackKnot[gear.MaxTrackKnots];
            _operation = new TrawlOperation(handling);
            _survey = new GroundSurvey(ground, survey);
            // Consume the caller's explicit stream once at setup. No RNG is called by Tick.
            _catchSeed = seededRandom.Next();
            double draw = seededRandom.NextDouble();
            if (!TrawlNumbers.NonNegative(draw) || draw >= 1d) throw new ArgumentException("Invalid RNG draw.", nameof(seededRandom));
            _snagThreshold = -Math.Log(1d - draw);
        }

        public bool TryShoot(in TrawlSessionInput input, TrawlWorkStation station)
        {
            if (Phase != TrawlSessionPhase.GearReady || !Admit(input) || !SafeDepth(input)) return false;
            double warp = (_def.DraughtMetres + input.DepthUnderKeelMetres) * _def.WarpDepthRatio;
            if (!TrawlNumbers.Positive(warp) || !_operation.BeginShooting(input.GameSeconds,
                input.SpeedOverGround, input.SeaState01, station)) return false;
            _plannedWarp = warp; _shootAt = input.GameSeconds; _lastTime = input.GameSeconds;
            Phase = TrawlSessionPhase.Shooting; return true;
        }

        public bool TickShooting(in TrawlSessionInput input)
        {
            if (Phase != TrawlSessionPhase.Shooting || !Admit(input) || !SafeDepth(input)
                || !PlannedWarpAllowed(input)
                || input.SpeedOverGround > _handling.MaxShootingMetresPerGameSecond
                || input.GameSeconds >= _shootAt + _handling.ShootingGameSeconds) return false;
            WarpOutMetres = _plannedWarp * ((input.GameSeconds - _shootAt) / _handling.ShootingGameSeconds);
            _lastTime = input.GameSeconds; return true;
        }

        // The driver supplies a snapshot at the scheduled boundary, even if its render poll is late.
        public bool TryFinishShooting(in TrawlSessionInput input)
        {
            if (Phase != TrawlSessionPhase.Shooting || !Admit(input) || !SafeDepth(input)
                || !PlannedWarpAllowed(input)
                || input.SpeedOverGround > _handling.MaxShootingMetresPerGameSecond
                || input.GameSeconds != _shootAt + _handling.ShootingGameSeconds
                || !_operation.CompleteShooting(input.GameSeconds)) return false;
            WarpOutMetres = _plannedWarp; _towAt = input.GameSeconds; _lastTime = input.GameSeconds;
            _lastInput = input; Phase = TrawlSessionPhase.Towing;
            UpdateDoors(input); _knots[_knotCount++] = Knot(input); return true;
        }

        /// <summary>Bounded, allocation-free telemetry ingestion. Inputs describe this straight
        /// segment; changing bottom requires a separately certified tow. Refusal changes nothing.</summary>
        public bool Tick(in TrawlSessionInput input)
        {
            if (Phase != TrawlSessionPhase.Towing || Snagged || !Admit(input) || input.GameSeconds <= _lastTime
                || input.GameSeconds - _towAt > _gear.MaxTowGameSeconds || _knotCount == _knots.Length) return false;
            double dx = (double)input.NetCentre.x - _lastInput.NetCentre.x;
            double dy = (double)input.NetCentre.y - _lastInput.NetCentre.y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double ratio = WarpOutMetres / (_def.DraughtMetres + input.DepthUnderKeelMetres);
            double hard = ratio >= _gear.MinWarpDepthRatio
                ? Math.Max(0d, _ground.Hardness01 - _gear.HardBottomThreshold01) : 0d;
            double excess = Math.Max(0d, ratio - _gear.MaxWarpDepthRatio);
            double hazard = distance * hard * _def.SnagHazardPerHardMetre;
            double chafe = distance * (hard * _gear.DamagePerHardMetre + excess * _def.ShallowChafePerMetre);
            if (!TrawlNumbers.NonNegative(hazard) || !TrawlNumbers.NonNegative(chafe)
                || !TrawlNumbers.Finite(_hazard + hazard) || !TrawlNumbers.Finite(_chafe + chafe)) return false;
            bool snag = hazard > 0d && _hazard + hazard >= _snagThreshold;
            double fraction = snag
                ? Math.Max(0d, (_snagThreshold - _hazard) / hazard) : 1d;
            var end = input;
            bool append = true;
            if (fraction < 1d)
            {
                double endTime = _lastTime + (input.GameSeconds - _lastTime) * fraction;
                // An unrepresentable or zero-length endpoint cannot make a valid solver track.
                if (endTime > _lastTime)
                    end = new TrawlSessionInput(endTime, input.SpeedOverGround, input.DepthUnderKeelMetres,
                        input.SeaState01, new Vector2((float)(_lastInput.NetCentre.x + dx * fraction),
                            (float)(_lastInput.NetCentre.y + dy * fraction)));
                else append = false;
            }
            _hazard += hazard * fraction; _chafe += chafe * fraction;
            UnsafeWarp = !SafeDepth(input) || ratio < _gear.MinWarpDepthRatio || ratio > _gear.MaxWarpDepthRatio;
            UpdateDoors(input);
            if (append) _knots[_knotCount++] = Knot(end);
            Snagged = snag;
            if (Snagged) Doors = TrawlDoorState.Snagged;
            _lastTime = input.GameSeconds; _lastInput = input; return true;
        }

        public bool TryBeginHauling(in TrawlSessionInput input, TrawlWorkStation station)
        {
            if (Phase != TrawlSessionPhase.Towing || !Admit(input) || input.GameSeconds != _lastTime
                || (_knotCount < 2 && !Snagged) || input.SpeedOverGround > _handling.MaxHaulingMetresPerGameSecond
                || station != _handling.WorkStation) return false;
            TrawlTowResult result;
            if (_knotCount < 2)
                result = new TrawlTowResult(TrawlTowStatus.Complete, Array.Empty<TrawlCatchLot>(), _def.InitialIntegrity01, 0d, 0);
            else
            {
                var knots = new TrawlTrackKnot[_knotCount];
                Array.Copy(_knots, knots, _knotCount);
                result = TrawlTowSolver.Resolve(_gear, _catchSeed, _towId, new TrawlTrack(knots), _survey,
                    _targets, _def.InitialIntegrity01);
            }
            LastSolveStatus = result.Status;
            if (result.Status != TrawlTowStatus.Complete || !_operation.BeginHauling(input.GameSeconds,
                input.SpeedOverGround, input.SeaState01, station)) return false;
            _catch = result; _graded = new TrawlGradedLot[result.LotCount]; _stowed = new int[result.LotCount];
            for (int i = 0; i < result.LotCount; i++)
            {
                int grade = 0;
                while (grade < _def.GradeWeightBreaksKg.Length
                    && result[i].UnitWeightKg >= _def.GradeWeightBreaksKg[grade]) grade++;
                _graded[i] = new TrawlGradedLot(result[i], grade);
            }
            _haulAt = input.GameSeconds;
            Phase = TrawlSessionPhase.Hauling; Doors = TrawlDoorState.Closed; return true;
        }

        public bool TickHauling(double gameSeconds)
        {
            if (Phase != TrawlSessionPhase.Hauling || !TrawlNumbers.Finite(gameSeconds) || gameSeconds < _lastTime
                || gameSeconds >= _haulAt + _handling.HaulingGameSeconds) return false;
            WarpOutMetres = _plannedWarp * (1d - (gameSeconds - _haulAt) / _handling.HaulingGameSeconds);
            _lastTime = gameSeconds; return true;
        }

        public bool TryFinishHauling(double gameSeconds)
        {
            if (Phase != TrawlSessionPhase.Hauling || gameSeconds < _lastTime || !_operation.CompleteHauling(gameSeconds)) return false;
            _deck = new TrawlDeckBatch(_catch, _handling, gameSeconds);
            _lastTime = gameSeconds; WarpOutMetres = 0d; Phase = TrawlSessionPhase.CatchOnDeck; return true;
        }

        public bool SortByHand(double gameSeconds, int lot, int individuals, TrawlSortDestination destination)
        {
            if (Phase != TrawlSessionPhase.CatchOnDeck || !_deck.SortByHand(gameSeconds, lot, individuals, destination)) return false;
            _lastTime = gameSeconds;
            if (_deck.ReadyToCarry) Phase = TrawlSessionPhase.Sorted;
            return true;
        }

        public bool ConfirmEmptyDeck()
        {
            if (Phase != TrawlSessionPhase.Sorted || !_deck.ReadyToCarry || _deck.KeptOnDeckCount != 0) return false;
            Phase = TrawlSessionPhase.Stowed; return true;
        }

        public bool TryFinishSorting()
        {
            if (Phase != TrawlSessionPhase.CatchOnDeck || !_deck.ReadyToCarry) return false;
            Phase = TrawlSessionPhase.Sorted; return true;
        }

        /// <summary>Bookkeeping after an explicit supported hand placement. The future scene
        /// adapter must first commit its Core transfer; this model grants no goods.</summary>
        public bool ConfirmHandStow(double gameSeconds, int lot, int individuals, double freeHoldKg)
        {
            if (Phase != TrawlSessionPhase.Sorted || !TrawlNumbers.Finite(gameSeconds) || gameSeconds < _lastTime
                || lot < 0 || lot >= _graded.Length || individuals <= 0
                || individuals > _deck.KeptOnDeckAt(lot) - _stowed[lot]
                || !TrawlNumbers.NonNegative(freeHoldKg)
                || individuals * _catch[lot].UnitWeightKg > freeHoldKg
                || gameSeconds - _lastTime < individuals * _def.CarryGameSecondsPerIndividual) return false;
            _stowed[lot] += individuals; StowedCount += individuals; _lastTime = gameSeconds;
            if (KeptOnDeckCount == 0) Phase = TrawlSessionPhase.Stowed;
            return true;
        }

        private bool Admit(in TrawlSessionInput input) => input.IsValid && input.GameSeconds >= _lastTime
            && input.SeaState01 <= _handling.MaxWorkingSeaState01
            && _ground.Contains(input.NetCentre, _def.DoorSpreadMetres / 2d);
        private bool SafeDepth(in TrawlSessionInput input) => input.DepthUnderKeelMetres >= _def.MinKeelClearanceMetres
            && input.DepthUnderKeelMetres + _def.DraughtMetres >= _gear.MinDepthMetres
            && input.DepthUnderKeelMetres + _def.DraughtMetres <= _gear.MaxDepthMetres;
        private bool PlannedWarpAllowed(in TrawlSessionInput input)
        {
            double ratio = _plannedWarp / (_def.DraughtMetres + input.DepthUnderKeelMetres);
            return ratio >= _gear.MinWarpDepthRatio && ratio <= _gear.MaxWarpDepthRatio;
        }
        private void UpdateDoors(in TrawlSessionInput input)
        {
            Doors = !UnsafeWarp && input.SpeedOverGround >= _gear.MinTowMetresPerGameSecond
                && input.SpeedOverGround <= _gear.MaxTowMetresPerGameSecond
                ? TrawlDoorState.Spread : TrawlDoorState.Collapsed;
        }
        private TrawlTrackKnot Knot(in TrawlSessionInput input)
            => new TrawlTrackKnot(input.GameSeconds, input.NetCentre, WarpOutMetres, DoorSpreadMetres);

        private sealed class GroundSurvey : ITrawlSurvey
        {
            private readonly TrawlSessionGround _ground;
            private readonly ITrawlSurvey _source;
            internal GroundSurvey(TrawlSessionGround ground, ITrawlSurvey source) { _ground = ground; _source = source; }
            public int Fill(TrawlTrack track, double from, double to, double halfWidth, double time,
                TrawlPopulationSample[] into, out TrawlGroundSample ground)
            {
                int found = _source.Fill(track, from, to, halfWidth, time, into, out ground);
                if (found < 0 || ground.GroundId != _ground.Id || ground.Hardness01 != _ground.Hardness01) return -1;
                if (found > into.Length) return found;
                for (int i = 0; i < found; i++)
                    if (!_ground.Holds(into[i].Species.Id)) return -1;
                return found;
            }
        }
    }
}
