using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>The definition and scene identity of one placed piece. The Def owns the kit data;
    /// the placement owns its row id, ground point and facing.</summary>
    [DisallowMultipleComponent]
    public sealed class SetPiecePlacement : MonoBehaviour
    {
        [SerializeField] SetPieceDef _def;
        [SerializeField] string _definitionId = "";
        [SerializeField] string _placementId = "";
        [SerializeField] int _rigDir;

        public SetPieceDef Def => _def;
        public string DefinitionId => _definitionId;
        public string PlacementId => _placementId;
        public int RigDir => _rigDir;
        public bool MatchesDefinition => _def != null && _definitionId == _def.Id;

        public void Configure(SetPieceDef definition, string placementId, int rigDir)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(placementId)) throw new ArgumentException("A placed piece needs its scene row id.", nameof(placementId));
            _def = definition;
            _definitionId = definition.Id;
            _placementId = placementId;
            _rigDir = rigDir;
        }

        public bool TryAnchorWorld(string name, out Vector3 at)
        {
            at = default;
            if (!MatchesDefinition || !_def.TryGetAnchor(name, out Vector3 local)) return false;
            Vector2 ground = SetPieceDef.ToWorld(new Vector2(local.x, local.y), _rigDir);
            at = transform.position + new Vector3(ground.x, ground.y, local.z);
            return true;
        }
    }
}
