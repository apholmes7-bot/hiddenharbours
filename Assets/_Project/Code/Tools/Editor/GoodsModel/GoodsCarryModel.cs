using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.GoodsModel
{
    public enum GoodsResult
    {
        Success, MissingItem, WrongSource, HandsOccupied, SameHandKind, NeedsDevice,
        DoesNotFit, CountFull, TooHeavy, Incompatible, Closed, NoSupport, Occupied,
        InvalidStack, SupportedByOthers
    }

    public enum GoodsKind { Catch, Container, Tool }

    [Serializable]
    public sealed class GoodsHandLimits
    {
        public GoodsBox OneHandEnvelope;
        public GoodsBox TwoHandEnvelope;
        public long OneHandMassG;
        public long TwoHandMassG;
        public long MaxCatchMassG;

        public bool IsValid => OneHandEnvelope.Fits(TwoHandEnvelope) &&
            OneHandMassG > 0 && TwoHandMassG >= OneHandMassG && MaxCatchMassG > 0 && MaxCatchMassG <= TwoHandMassG;
    }

    public sealed class GoodsPiece
    {
        public string Id { get; }
        public string Profile { get; }
        public GoodsKind Kind { get; }
        public GoodsBox Size { get; }
        public long MassG { get; }
        public bool MayRotate { get; }

        public GoodsPiece(string id, string profile, GoodsKind kind, GoodsBox size, long massG, bool mayRotate)
        {
            if (string.IsNullOrWhiteSpace(id) || !GoodsContainerSpec.ValidId(profile, "goods") ||
                !size.IsValid || massG <= 0 || !Enum.IsDefined(typeof(GoodsKind), kind))
                throw new ArgumentException("Invalid goods piece");
            Id = id;
            Profile = profile;
            Kind = kind;
            Size = size;
            MassG = massG;
            MayRotate = mayRotate;
        }
    }

    // An isolated transfer authority. There are no scene adapters, saves, clocks or visual references.
    public sealed class GoodsCarryModel
    {
        private readonly Dictionary<string, GoodsPiece> _pieces = new Dictionary<string, GoodsPiece>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _locations = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _placements = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, GoodsContainerSpec> _containers = new Dictionary<string, GoodsContainerSpec>(StringComparer.Ordinal);
        private readonly HashSet<string> _closed = new HashSet<string>(StringComparer.Ordinal);
        private readonly GoodsHandLimits _limits;
        public string LeftHand { get; private set; }
        public string RightHand { get; private set; }

        public GoodsCarryModel(GoodsHandLimits limits)
        {
            if (limits == null || !limits.IsValid) throw new ArgumentException("Invalid hand limits");
            _limits = limits;
        }

        public void Register(GoodsPiece piece, string supportId)
        {
            if (piece != null && piece.Kind == GoodsKind.Container)
                throw new ArgumentException("Containers require definition data");
            RegisterOnSupport(piece, supportId);
        }

        private void RegisterOnSupport(GoodsPiece piece, string supportId)
        {
            if (piece == null || string.IsNullOrWhiteSpace(supportId) || _pieces.ContainsKey(piece.Id) ||
                piece.Id == HandLocation || supportId == HandLocation || piece.Id == supportId ||
                _pieces.ContainsKey(supportId) || _locations.Values.Contains(piece.Id))
                throw new ArgumentException("Duplicate piece or missing support");
            _pieces.Add(piece.Id, piece);
            _locations.Add(piece.Id, supportId);
        }

        public void RegisterContainer(GoodsPiece shell, GoodsContainerSpec spec, string supportId)
        {
            if (spec == null || spec.Validate().Count != 0 || shell == null || shell.Kind != GoodsKind.Container ||
                shell.Size.LengthMm != spec.Outside.LengthMm || shell.Size.WidthMm != spec.Outside.WidthMm ||
                shell.Size.HeightMm != spec.Outside.HeightMm || shell.MassG != spec.EmptyMassG)
                throw new ArgumentException("Invalid container shell or definition");
            RegisterOnSupport(shell, supportId);
            _containers.Add(shell.Id, spec);
        }

        public string Location(string itemId) => _locations[itemId];
        public IReadOnlyList<string> Contents(string containerId) => _placements.Keys
            .Where(id => _locations[id] == containerId).OrderBy(id => _placements[id]).ToArray();
        public long GrossMassG(string id) => checked(_pieces[id].MassG +
            (_closed.Contains(id) ? _containers[id].LidMassG : 0) + Contents(id).Sum(child => _pieces[child].MassG));

        public GoodsResult SetClosed(string id, bool closed)
        {
            if (!_containers.TryGetValue(id, out var spec)) return GoodsResult.MissingItem;
            if (!spec.HasLid) return GoodsResult.Incompatible;
            if (LeftHand == id || RightHand == id) return GoodsResult.HandsOccupied;
            if (closed && GrossMassG(id) + (_closed.Contains(id) ? 0 : spec.LidMassG) > spec.MaxGrossMassG)
                return GoodsResult.TooHeavy;
            if (closed) _closed.Add(id); else _closed.Remove(id);
            return GoodsResult.Success;
        }

        public GoodsResult Take(string id, string expectedSource, bool preferLeft)
        {
            if (!_pieces.TryGetValue(id, out var piece)) return GoodsResult.MissingItem;
            if (_locations[id] != expectedSource || LeftHand == id || RightHand == id) return GoodsResult.WrongSource;
            if (_closed.Contains(expectedSource)) return GoodsResult.Closed;
            int hands = piece.Size.Fits(_limits.OneHandEnvelope, piece.MayRotate) && GrossMassG(id) <= _limits.OneHandMassG ? 1 : 2;
            if (!piece.Size.Fits(_limits.TwoHandEnvelope, piece.MayRotate) || GrossMassG(id) > _limits.TwoHandMassG)
                return GoodsResult.NeedsDevice;
            if (piece.Kind == GoodsKind.Catch && piece.MassG > _limits.MaxCatchMassG) return GoodsResult.NeedsDevice;
            if (_containers.TryGetValue(id, out var spec))
            {
                if (spec.RequiredHands == 0 || GrossMassG(id) > spec.MaxHandGrossMassG) return GoodsResult.NeedsDevice;
                hands = Math.Max(hands, spec.RequiredHands);
            }
            if ((hands == 2 && (LeftHand != null || RightHand != null)) || (LeftHand != null && RightHand != null))
                return GoodsResult.HandsOccupied;
            foreach (string held in new[] { LeftHand, RightHand })
                if (held != null && _pieces[held].Kind == piece.Kind) return GoodsResult.SameHandKind;
            if (hands == 2) { LeftHand = id; RightHand = id; }
            else if ((preferLeft && LeftHand == null) || RightHand != null) LeftHand = id;
            else RightHand = id;
            _locations[id] = HandLocation;
            _placements.Remove(id);
            return GoodsResult.Success;
        }

        public GoodsResult Put(string id, string destination)
        {
            if (!_pieces.TryGetValue(id, out var piece) || !_containers.TryGetValue(destination, out var spec)) return GoodsResult.MissingItem;
            if (LeftHand != id && RightHand != id) return GoodsResult.WrongSource;
            // Nesting needs its own containment contract; a container cannot contain itself or another container here.
            if (piece.Kind == GoodsKind.Container || !spec.AcceptedProfiles.Contains(piece.Profile)) return GoodsResult.Incompatible;
            if (LeftHand == destination && RightHand == destination) return GoodsResult.HandsOccupied;
            if (_closed.Contains(destination)) return GoodsResult.Closed;
            var contents = Contents(destination);
            if (contents.Count >= spec.CountLimit) return GoodsResult.CountFull;
            if (GrossMassG(destination) + piece.MassG > spec.MaxGrossMassG ||
                ((LeftHand == destination || RightHand == destination) &&
                 (GrossMassG(destination) + piece.MassG > spec.MaxHandGrossMassG ||
                  GrossMassG(destination) + piece.MassG > _limits.OneHandMassG))) return GoodsResult.TooHeavy;
            if (!piece.Size.Fits(spec.Opening, piece.MayRotate) ||
                contents.Sum(child => _pieces[child].Size.VolumeMm3) + piece.Size.VolumeMm3 > checked(spec.UsableVolumeMl * 1000))
                return GoodsResult.DoesNotFit;
            int slot = -1;
            for (int i = 0; i < spec.Slots.Length; i++)
                if (!contents.Any(child => _placements[child] == i) && piece.Size.Fits(spec.Slots[i].Size, piece.MayRotate)) { slot = i; break; }
            if (slot < 0) return GoodsResult.DoesNotFit;
            _locations[id] = destination;
            _placements[id] = slot;
            Release(id);
            return GoodsResult.Success;
        }

        public GoodsResult SetDown(string id, string supportId, bool verifiedDrySupport)
        {
            if (!_pieces.ContainsKey(id)) return GoodsResult.MissingItem;
            if (LeftHand != id && RightHand != id) return GoodsResult.WrongSource;
            if (!verifiedDrySupport || string.IsNullOrWhiteSpace(supportId) ||
                supportId == HandLocation || _pieces.ContainsKey(supportId)) return GoodsResult.NoSupport;
            _locations[id] = supportId;
            Release(id);
            return GoodsResult.Success;
        }

        private const string HandLocation = "@hands";
        private void Release(string id)
        {
            if (LeftHand == id) LeftHand = null;
            if (RightHand == id) RightHand = null;
        }
    }
}
