using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>The most materials any creator build paints, and how that was found: by factor
    /// (body = garment x bottom, head = hat x hair style x beard x eye shape), then checked on
    /// seeded random builds across every offered axis, the body axes and the three creator ages.</summary>
    public sealed class CreatorWorst9
    {
        /// <summary>Body x head combinations the factorisation covers.</summary>
        public int Combinations;
        /// <summary>Most materials over every face group (blink, gaze and mouth shapes included).</summary>
        public int AllGroups;
        public string AllGroupsWitness;
        /// <summary>Most materials over the rest face, the faces the baker binds.</summary>
        public int DefaultFace;
        public string DefaultFaceWitness;
        /// <summary>Random builds painted in full and compared with their two factors.</summary>
        public int Sampled;
        /// <summary>Random builds whose painted set is NOT the union of its factors' sets. Any
        /// mismatch means the factorisation is wrong, and so is the worst it reports.</summary>
        public int Mismatches;
        public string FirstMismatch;
        /// <summary>Face materials some build paints and its shading contract does not declare.</summary>
        public string Strays;
        /// <summary>Presets whose count by the audit's counter differs from
        /// <see cref="CharacterSkinExtractor.ReadMaterials9"/>, the baker's counter.</summary>
        public List<string> PresetDisagreements = new List<string>();
    }

    /// <summary>Every offered option of the builder's options file, built on the rig.</summary>
    public sealed class OptionAudit9
    {
        public int Offered;
        /// <summary>One line per option that does not resolve: normBuild drops it, the rig builds
        /// other materials on a part than the file names, a ramp differs, a material is not
        /// declared, or it draws the same figure as the axis default.</summary>
        public List<string> Problems = new List<string>();
        /// <summary>The file's <c>rules.notOffered</c>, as <c>axis.value</c>.</summary>
        public List<string> NotOffered = new List<string>();
        /// <summary>The fixed colours the file documents as a mix of a ramp with the ink (the hair
        /// axis's <c>alsoColours</c>: <c>brow = mix(hair ramp [0], ink, 0.34)</c>), each shown to hold
        /// with ONE ink for every option that names it. The rig does not export its ink, so the
        /// audit finds the inks that reproduce every option (the rig rounds each channel) rather
        /// than carrying a copy.</summary>
        public List<string> Derived = new List<string>();
    }

    /// <summary>A number-by-number comparison of an export against its committed file.</summary>
    public sealed class ExportDrift9
    {
        public int Numbers;
        public double Worst;
        public string WorstAt;
        public List<string> Problems = new List<string>();
    }

    public static partial class CharacterSkinExtractor
    {
        // ---------------------------------------------------------------------------------------
        // The kit as landed: the folder that holds Art/, and the files its README lays out
        // ---------------------------------------------------------------------------------------

        /// <summary>The kit folder rig 9 was landed in, repo-relative: the parent of the folder
        /// that holds the rig script.</summary>
        public static string V9KitFolder =>
            Path.GetDirectoryName(Path.GetDirectoryName(V9ScriptPath)).Replace('\\', '/');

        public static string V9KitRoot => Path.Combine(RigCatalog.RepoRoot, V9KitFolder);

        public const string V9OptionsFile = "data/options.v9.json";
        public const string V9GoldenFile = "golden-report.json";
        public const string V9ManifestFile = "renders/manifest.json";
        const string V9BuildFile = "builds/{0}.v9.json";
        const string V9GameplayFile = "gameplay/{0}.{1}.gameplay.json";

        /// <summary>A kit file's text with CRLF read as LF (the folder is pinned LF; this keeps a
        /// checkout that ignored the pin from failing on line endings alone).</summary>
        public static string ReadKitText9(string kitRoot, string kitRelativePath) =>
            File.ReadAllText(Path.Combine(kitRoot, kitRelativePath)).Replace("\r\n", "\n");

        static string KitRelative9(string repoRelative)
        {
            string kit = V9KitFolder + "/";
            string path = repoRelative.Replace('\\', '/');
            if (!path.StartsWith(kit, StringComparison.Ordinal))
                throw new InvalidOperationException($"{path} is not inside the rig 9 kit folder {kit}.");
            return path.Substring(kit.Length);
        }

        /// <summary>The LF hashes of the rig and the pose library in <paramref name="kitRoot"/>,
        /// the two values every export, sidecar and manifest names as its source.</summary>
        public static (string rig, string poses) KitShas9(string kitRoot) =>
            (LfSha256File(Path.Combine(kitRoot, KitRelative9(V9ScriptPath))),
             LfSha256File(Path.Combine(kitRoot, KitRelative9(V9PosesPath))));

        // ---------------------------------------------------------------------------------------
        // The audit's JS, installed once per host
        // ---------------------------------------------------------------------------------------

        /// <summary>HEAD is the head side of the worst-build factorisation, by face part. It is a
        /// premise, not a fact: the random cross-check fails if a part is on the wrong side.</summary>
        const string AuditJs9 = @"
globalThis.__hh9a = (function (C) {
  var own = function (o, k) { return Object.prototype.hasOwnProperty.call(o, k); };
  var HEAD = { head: 1, nose: 1, ear: 1, hair: 1, hat: 1, hood: 1, beard: 1, face: 1 };
  function partOf(f) { return String(f.part).replace(/_[LR]$/, ''); }
  function copy(o) { var r = {}; for (var k in o) r[k] = o[k]; return r; }
  function restGroups(B) { var rf = C.baseIntent(B.D).face, act = {};
    Object.keys(C.FACE_SLOTS).forEach(function (s) { act[s + '.' + rf[s]] = 1; }); return act; }
  function paints(B, side, rest, strays) {
    var decl = C.shadingContract(B).materials, act = rest ? restGroups(B) : null, s = {}, F = B.mesh.faces;
    for (var i = 0; i < F.length; i++) { var f = F[i];
      if (side !== null && !!HEAD[partOf(f)] !== side) continue;
      if (act && f.group && !act[f.group]) continue;
      if (!own(decl, f.mat)) strays[f.mat] = 1;
      s[f.mat] = 1; }
    return Object.keys(s).sort(); }
  function unite(x, y) { var s = {}; x.forEach(function (m) { s[m] = 1; }); y.forEach(function (m) { s[m] = 1; });
    return Object.keys(s).sort(); }
  function count(p, rest) { var st = {}, n = paints(C.buildOf(p), null, rest, st).length, k = Object.keys(st);
    return k.length ? '!' + k.join(',') : String(n); }
  function values(O, id) { var a = O.axes.filter(function (x) { return x.id === id; })[0];
    if (!a) throw new Error('the options file has no axis ' + id); return a.options.map(function (o) { return o.value; }); }
  function defaults(O) { var b = {}; O.axes.forEach(function (a) { b[a.id] = a.default; }); return b; }
  function worst(O, samples, seed) {
    var base = defaults(O), strays = {}, MB = {}, MH = {}, GB = [], HK = [];
    var top = function (g) { return !!(C.GARMENTS[g] && C.GARMENTS[g].top); };
    var mk = function (o) { var b = copy(base); for (var k in o) b[k] = o[k]; return C.buildOf(b); };
    values(O, 'garment').forEach(function (g) {
      (top(g) ? values(O, 'bottom') : [base.bottom]).forEach(function (bt) { GB.push(g + '/' + bt); }); });
    GB.forEach(function (k) { var p = k.split('/'), B = mk({ garment: p[0], bottom: p[1] });
      MB[k] = { a: paints(B, false, false, strays), d: paints(B, false, true, strays) }; });
    values(O, 'hat').forEach(function (h) { values(O, 'hairStyle').forEach(function (hs) {
      values(O, 'beard').forEach(function (bd) { values(O, 'eyeShape').forEach(function (es) {
        var k = [h, hs, bd, es].join('|'), B = mk({ hat: h, hairStyle: hs, beard: bd, eyeShape: es });
        MH[k] = { a: paints(B, true, false, strays), d: paints(B, true, true, strays) }; HK.push(k); }); }); }); });
    var r = { combos: 0, all: 0, allAt: '', def: 0, defAt: '', sampled: 0, mismatches: 0, first: '' };
    GB.forEach(function (g) { HK.forEach(function (h) { r.combos++;
      var a = unite(MB[g].a, MH[h].a).length, d = unite(MB[g].d, MH[h].d).length;
      if (a > r.all) { r.all = a; r.allAt = g + ' ' + h; }
      if (d > r.def) { r.def = d; r.defAt = g + ' ' + h; } }); });
    var s = seed >>> 0, rnd = function (n) { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s % n; };
    var not = {}; (O.rules.notOffered || []).forEach(function (x) { x.values.forEach(function (v) { not[x.axis + '.' + v] = 1; }); });
    var free = O.axes.filter(function (a) { return a.creator && a.creator.offered; }).map(function (a) { return a.id; });
    var ages = values(O, 'age').filter(function (v) { return !not['age.' + v]; });
    for (var i = 0; i < samples; i++) {
      var b = copy(base);
      free.forEach(function (id) { var v = values(O, id); b[id] = v[rnd(v.length)]; });
      b.age = ages[rnd(ages.length)];
      var B = C.buildOf(b), g = b.garment + '/' + (top(b.garment) ? b.bottom : base.bottom);
      var h = [b.hat, b.hairStyle, b.beard, b.eyeShape].join('|');
      var pa = paints(B, null, false, strays).join(','), pd = paints(B, null, true, strays).join(',');
      var ea = unite(MB[g].a, MH[h].a).join(','), ed = unite(MB[g].d, MH[h].d).join(',');
      r.sampled++;
      if (pa !== ea || pd !== ed) { r.mismatches++;
        if (!r.first) r.first = C.buildKey(B.b) + ' paints [' + pa + '] / [' + pd + '], its factors [' + ea + '] / [' + ed + ']'; } }
    return [r.combos, r.all, r.allAt, r.def, r.defAt, r.sampled, r.mismatches, r.first,
            Object.keys(strays).sort().join(',')].join('\n'); }
  function sig(B) { return JSON.stringify(B.mesh.faces.map(function (f) { return [f.part, f.mat, f.group || '', f.v]; })); }
  function paintOf(B) { return JSON.stringify(C.shadingContract(B).materials); }
  function mixRule(a, m) { var out = null;
    (a.alsoColours || []).forEach(function (s) { var x = /(\w+) = mix\(\w+ ramp \[(\d+)\], ink, ([0-9.]+)\)/.exec(s);
      if (x && x[1] === m) out = { i: +x[2], t: +x[3] }; });
    return out; }
  function options(O, ctx) {
    var base = defaults(O), offered = 0, bad = [], not = [], mixes = {}, derived = [];
    O.axes.forEach(function (a) {
      if (!a.creator || !a.creator.offered) return;
      var b0 = copy(base), cx = ctx[a.id] || {}; for (var k in cx) b0[k] = cx[k];
      var B0 = C.buildOf(b0), s0 = sig(B0), p0 = paintOf(B0);
      a.options.forEach(function (opt) { offered++;
        var b = copy(b0); b[a.id] = opt.value;
        var why = [], nb = C.normBuild(b);
        if (!(nb[a.id] === opt.value || (typeof opt.value === 'number' && +nb[a.id] === opt.value)))
          why.push('normBuild makes it ' + nb[a.id]);
        var B = C.buildOf(b), F = B.mesh.faces, cm = C.shadingContract(B).materials;
        if (opt.parts) Object.keys(opt.parts).forEach(function (p) { var got = {};
          F.forEach(function (f) { if (partOf(f) === p) got[f.mat] = 1; });
          var g = Object.keys(got).sort().join(','), w = opt.parts[p].slice().sort().join(',');
          if (g !== w) why.push(p + ': the rig builds [' + g + '], the file says [' + w + ']'); });
        if (opt.ramp) (opt.materials || []).forEach(function (m) { var c = cm[m], r;
          if (!c) why.push(m + ' is not in the shading contract');
          else if (!c.fixed) { if (JSON.stringify(c.ramp) !== JSON.stringify(opt.ramp)) why.push(m + ': the contract ramp is not the file ramp'); }
          else if (opt.ramp.indexOf(c.color) >= 0) return;
          else if (!(r = mixRule(a, m))) why.push(m + ' is fixed at ' + c.color + ', neither a colour of the file ramp nor a mix the file documents');
          else (mixes[a.id + '.' + m] = mixes[a.id + '.' + m] || { t: r.t, i: r.i, rows: [] }).rows.push([opt.ramp[r.i], c.color, opt.id]); });
        if (opt.value !== a.default && sig(B) === s0 && paintOf(B) === p0) why.push('draws the same figure as ' + a.default);
        if (why.length) bad.push(opt.id + ': ' + why.join(' | ')); }); });
    (O.rules.notOffered || []).forEach(function (x) { x.values.forEach(function (v) { not.push(x.axis + '.' + v); }); });
    Object.keys(mixes).sort().forEach(function (k) { var e = mixes[k], lo = '#', hi = '#';
      for (var ch = 0; ch < 3 && lo; ch++) { var hits = [];
        for (var v = 0; v < 256; v++) if (e.rows.every(function (r) { var a0 = parseInt(r[0].substr(1 + 2 * ch, 2), 16);
            return Math.round(a0 + (v - a0) * e.t) === parseInt(r[1].substr(1 + 2 * ch, 2), 16); })) hits.push(v);
        if (!hits.length) { lo = ''; break; }
        lo += (256 + hits[0]).toString(16).substr(1); hi += (256 + hits[hits.length - 1]).toString(16).substr(1); }
      if (!lo) bad.push(k + ': no one ink gives every option the documented mix(ramp [' + e.i + '], ink, ' + e.t + '): ' +
        e.rows.map(function (r) { return r[2] + ' ' + r[1]; }).join(', '));
      else derived.push(k + ' = mix(ramp [' + e.i + '], ink, ' + e.t + ') on ' + e.rows.length + ' options, one ink in ' + lo + (hi === lo ? '' : '..' + hi)); });
    return [offered, not.join(','), derived.join(';')].concat(bad).join('\n'); }
  function cmp(a, b, tol, path, st) {
    if (st.p.length >= 8) return;
    if (typeof a === 'number' && typeof b === 'number') { st.n++; var d = Math.abs(a - b);
      if (d > st.w) { st.w = d; st.at = path; } if (!(d <= tol)) st.p.push(path + ': ' + a + ' vs ' + b); return; }
    if (Array.isArray(a)) { if (!Array.isArray(b) || a.length !== b.length) { st.p.push(path + ': ' + a.length + ' items vs ' + (Array.isArray(b) ? b.length : typeof b)); return; }
      for (var i = 0; i < a.length; i++) cmp(a[i], b[i], tol, path + '[' + i + ']', st); return; }
    if (a && typeof a === 'object') { if (!b || typeof b !== 'object' || Array.isArray(b)) { st.p.push(path + ': object vs ' + typeof b); return; }
      var ka = Object.keys(a), kb = Object.keys(b);
      if (ka.join(',') !== kb.join(',')) { st.p.push(path + ': keys [' + ka.join(',') + '] vs [' + kb.join(',') + ']'); return; }
      for (var j = 0; j < ka.length; j++) cmp(a[ka[j]], b[ka[j]], tol, path + '.' + ka[j], st); return; }
    if (a !== b) st.p.push(path + ': ' + JSON.stringify(a) + ' vs ' + JSON.stringify(b)); }
  function build(p, rig, poses, want, tol) {
    var B = C.buildOf(p), e = C.exportBuild(p);
    var o = { rig: e.rig, revision: e.revision, derivedFromRigSha256: rig, posesDerivedFromRigSha256: poses, buildKey: C.buildKey(B.b) };
    Object.keys(e).forEach(function (k) { if (!own(o, k)) o[k] = e[k]; }); o.sockets = C.sockets(p);
    var st = { n: 0, w: 0, at: '', p: [] }; cmp(JSON.parse(JSON.stringify(o)), want, tol, p, st);
    return [st.n, st.w, st.at].concat(st.p).join('\n'); }
  function gameplay(p, rig, poses) { var o = C.gameplay(p), r = {};
    Object.keys(o).forEach(function (k) { if (k === 'derivedFromRigSha256' || k === 'posesDerivedFromRigSha256') return; r[k] = o[k];
      if (k === 'exportSymbol') { r.derivedFromRigSha256 = rig; r.posesDerivedFromRigSha256 = poses; } });
    return JSON.stringify(r, null, 2); }
  function strip(p, clip, dir, n) { var W = C.W * n, H = C.H, out = new Uint8Array(W * H * 4);
    for (var k = 0; k < n; k++) { var R = C.render({ clip: clip, frame: k, dir: dir, build: p });
      for (var y = 0; y < H; y++) for (var x = 0; x < C.W; x++) { var a = (y * C.W + x) * 4, d = (y * W + k * C.W + x) * 4;
        out[d] = R.rgba[a]; out[d + 1] = R.rgba[a + 1]; out[d + 2] = R.rgba[a + 2]; out[d + 3] = R.rgba[a + 3]; } }
    return out; }
  function golden(p, G) { var rows = C.runChecks(p), g = G.builds[p], passed = 0, of = 0, moved = [];
    if (!g) return ['0', '0', 'golden-report.json has no build ' + p].join('\n');
    if (rows.length !== g.checks.length) moved.push(rows.length + ' checks ran, golden has ' + g.checks.length);
    rows.forEach(function (r) { if (r.gate) { of++; if (r.pass) passed++; }
      var x = g.checks.filter(function (c) { return c.id === r.id; })[0];
      if (!x) moved.push(r.id + ': not in golden');
      else if (x.pass !== r.pass || JSON.stringify(x.value) !== JSON.stringify(r.value) || !!x.gate !== !!r.gate)
        moved.push(r.id + ': golden ' + x.pass + ' ' + JSON.stringify(x.value) + ', now ' + r.pass + ' ' + JSON.stringify(r.value)); });
    if (g.passed !== passed || g.of !== of) moved.push('gated ' + passed + '/' + of + ', golden ' + g.passed + '/' + g.of);
    return [passed, of].concat(moved).join('\n'); }
  function posedCorners(p, name, k, groups) {
    var B = C.buildOf(p), cd = C.clipDef(name);
    if (!cd) throw new Error('characterIsoRig9 has no clip ' + name);
    var S = C.evalClip(name, C.uOf(cd.anim, k), B), P = C.posed(S, B), fc = S.I.face, act = {}, mine = {};
    Object.keys(C.FACE_SLOTS).forEach(function (s) { act[s + '.' + fc[s]] = 1; });
    groups.forEach(function (g) { mine[g] = 1; });
    var F = B.mesh.faces, out = [], j = 0;
    for (var i = 0; i < F.length; i++) { var f = F[i], live = !f.group || act[f.group];
      if (live && (j >= P.length || P[j].mat !== f.mat || P[j].part !== f.part || P[j].v.length !== f.v.length))
        throw new Error(name + ' f' + k + ': posed() face ' + j + ' is not bind face ' + i);
      if (!f.group || mine[f.group]) for (var c = 0; c < f.v.length; c++) { var q = live ? P[j].v[c] : null;
        out.push(q ? q[0] : NaN, q ? q[1] : NaN, q ? q[2] : NaN); }
      if (live) j++; }
    if (j !== P.length) throw new Error(name + ' f' + k + ': posed() drew ' + P.length + ' faces, the walk matched ' + j);
    return new Uint8Array(new Float64Array(out).buffer); }
  return { count: count, worst: worst, options: options, build: build, gameplay: gameplay, strip: strip,
           golden: golden, posedCorners: posedCorners };
})($G);";

        static void Install9Audit(IRigScriptHost host)
        {
            Load9(host);
            if (host.EvaluateBool("typeof globalThis.__hh9a==='object'&&globalThis.__hh9a!==null")) return;
            host.Execute(AuditJs9.Replace("$G", V9GlobalName));
        }

        static string[] Lines9(string s) => s.Split('\n');

        const string At9 = "A number returned by rig 9's audit (globalThis.__hh9a)";

        // ---------------------------------------------------------------------------------------
        // Painted materials: presets and the creator's worst build
        // ---------------------------------------------------------------------------------------

        /// <summary>Every face of <paramref name="preset"/>, every face group included: what the
        /// figure paints once the engine plays blink, gaze and mouth shapes (owner ruling 8).</summary>
        public static string AllFacesJs9(string preset) => $"{V9GlobalName}.bindMesh({Js(preset)})";

        /// <summary>
        /// The most materials any build the creator can make paints, over every face group and
        /// over the rest face, counted as <see cref="ReadMaterials9"/> counts (distinct face
        /// materials, each declared by the build's own shading contract). The full product of the
        /// offered structural options is 62,400 builds, too slow to paint one by one, so the audit
        /// paints the 25 bodies and the 2,496 heads apart and unites them, and then proves that
        /// split on <paramref name="samples"/> seeded random builds that vary every offered axis
        /// and the youth, adult and elder ages. Each preset is also counted by both counters.
        /// </summary>
        public static CreatorWorst9 WorstCreatorBuild9(IRigScriptHost host, string optionsJson,
                                                       int samples, uint seed)
        {
            Install9Audit(host);
            string[] f = Lines9(host.EvaluateString(
                $"globalThis.__hh9a.worst(({optionsJson}),{samples.ToString(CultureInfo.InvariantCulture)}," +
                $"{seed.ToString(CultureInfo.InvariantCulture)})"));
            if (f.Length != 9)
                throw new InvalidOperationException($"The worst-build audit returned {f.Length} fields, not 9.");
            var w = new CreatorWorst9
            {
                Combinations = Int9(f[0], At9),
                AllGroups = Int9(f[1], At9), AllGroupsWitness = f[2],
                DefaultFace = Int9(f[3], At9), DefaultFaceWitness = f[4],
                Sampled = Int9(f[5], At9), Mismatches = Int9(f[6], At9), FirstMismatch = f[7],
                Strays = f[8],
            };
            foreach (string p in Presets9(host))
            {
                string rest = host.EvaluateString($"globalThis.__hh9a.count({Js(p)},true)");
                string all = host.EvaluateString($"globalThis.__hh9a.count({Js(p)},false)");
                int bakerRest = ReadMaterials9(host, p, DefaultFaceMeshJs9(host, p)).Count;
                int bakerAll = ReadMaterials9(host, p, AllFacesJs9(p)).Count;
                if (rest != bakerRest.ToString(CultureInfo.InvariantCulture) ||
                    all != bakerAll.ToString(CultureInfo.InvariantCulture))
                    w.PresetDisagreements.Add($"{p}: the audit counts {rest} / {all} (rest face / all groups), " +
                                              $"the baker {bakerRest} / {bakerAll}");
            }
            return w;
        }

        /// <summary>
        /// Every option the creator offers, built on the rig from the file's own defaults, with
        /// <paramref name="contexts"/> setting the one other axis an option needs to show at all
        /// (the file's <c>rules.sameFigure</c>: a bottom under overalls, a hat colour with no hat
        /// and an apron colour on a tee draw nothing). An option resolves when normBuild keeps it,
        /// every part it names is built in exactly the materials it names, every ramp it names is
        /// its materials' ramp in the contract (a fixed material's colour is one of the ramp's, or
        /// the mix of the ramp with the ink that the file documents, with one ink for every
        /// option: <see cref="OptionAudit9.Derived"/>), and it draws a different figure from its
        /// axis default.
        /// </summary>
        public static OptionAudit9 AuditOptions9(IRigScriptHost host, string optionsJson,
                                                 IReadOnlyDictionary<string, (string axis, string value)> contexts)
        {
            Install9Audit(host);
            var ctx = new StringBuilder("{");
            foreach (var kv in contexts)
                ctx.Append(Js(kv.Key)).Append(":{").Append(Js(kv.Value.axis)).Append(':')
                   .Append(Js(kv.Value.value)).Append("},");
            ctx.Append('}');
            string[] f = Lines9(host.EvaluateString($"globalThis.__hh9a.options(({optionsJson}),{ctx})"));
            var a = new OptionAudit9 { Offered = Int9(f[0], At9) };
            a.NotOffered.AddRange(SplitList(f[1]));
            a.Derived.AddRange(f[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            for (int i = 3; i < f.Length; i++) a.Problems.Add(f[i]);
            return a;
        }

        // ---------------------------------------------------------------------------------------
        // The committed export, sidecars, renders and golden report, against the rig today
        // ---------------------------------------------------------------------------------------

        /// <summary><c>exportBuild(preset)</c> keyed as the kit's exporter writes it, against the
        /// committed <c>builds/&lt;preset&gt;.v9.json</c>: the same keys in the same order, the
        /// same strings, and every number within <paramref name="tolerance"/>. Not byte identity:
        /// V8 and Node differ in the last bit of some numbers (INTAKE.md).</summary>
        public static ExportDrift9 CompareBuild9(IRigScriptHost host, string kitRoot, string preset,
                                                 double tolerance)
        {
            Install9Audit(host);
            AssertPreset9(host, preset);
            var (rig, poses) = KitShas9(kitRoot);
            string want = ReadKitText9(kitRoot, string.Format(CultureInfo.InvariantCulture, V9BuildFile, preset));
            host.Execute($"globalThis.__hh9want=({want});");
            try
            {
                string[] f = Lines9(host.EvaluateString(
                    $"globalThis.__hh9a.build({Js(preset)},{Js(rig)},{Js(poses)},globalThis.__hh9want," +
                    $"{tolerance.ToString("R", CultureInfo.InvariantCulture)})"));
                var d = new ExportDrift9
                {
                    Numbers = Int9(f[0], At9),
                    Worst = Finite9(f[1], At9),
                    WorstAt = f[2],
                };
                for (int i = 3; i < f.Length; i++) d.Problems.Add(f[i]);
                return d;
            }
            finally { host.Execute("delete globalThis.__hh9want;"); }
        }

        /// <summary>The gameplay sidecar the rig writes today for <paramref name="preset"/>
        /// against the committed one, byte for byte. Null when they are identical, else the
        /// first line that differs.</summary>
        public static string GameplaySidecarDiff9(IRigScriptHost host, string kitRoot, string preset)
        {
            Install9Audit(host);
            AssertPreset9(host, preset);
            var (rig, poses) = KitShas9(kitRoot);
            string file = string.Format(CultureInfo.InvariantCulture, V9GameplayFile,
                                        Path.GetFileNameWithoutExtension(V9ScriptPath), preset);
            string want = ReadKitText9(kitRoot, file);
            string got = host.EvaluateString($"globalThis.__hh9a.gameplay({Js(preset)},{Js(rig)},{Js(poses)})");
            if (string.Equals(got, want, StringComparison.Ordinal)) return null;
            string[] a = Lines9(got), b = Lines9(want);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "(end)", y = i < b.Length ? b[i] : "(end)";
                if (!string.Equals(x, y, StringComparison.Ordinal))
                    return $"{file} line {i + 1}: the rig writes '{x}', the file has '{y}'";
            }
            return $"{file}: differs only in its line ending at the end";
        }

        /// <summary>Every 1x strip the manifest lists, rendered by the rig today, hashed as RGBA
        /// against the manifest's <c>rgbaSha256</c>, after the manifest's own source hashes are
        /// checked against the rig and pose library in the kit. Returns one line per miss.</summary>
        public static List<string> RenderMisses9(IRigScriptHost host, string kitRoot, out int strips)
        {
            Install9Audit(host);
            var misses = new List<string>();
            var (rig, poses) = KitShas9(kitRoot);
            host.Execute($"globalThis.__hh9man=({ReadKitText9(kitRoot, V9ManifestFile)});");
            try
            {
                string mRig = host.EvaluateString("String(globalThis.__hh9man.derivedFromRigSha256)");
                string mPoses = host.EvaluateString("String(globalThis.__hh9man.posesDerivedFromRigSha256)");
                if (mRig != rig || mPoses != poses)
                    misses.Add($"the manifest was rendered from rig {mRig} / poses {mPoses}; the kit holds {rig} / {poses}");
                string[] rows = Lines9(host.EvaluateString(
                    "globalThis.__hh9man.images.filter(function(i){return i.scale===1;}).map(function(i){" +
                    "return [i.file,i.preset,i.clip,i.dir,i.frames,i.rgbaSha256].join('|');}).join('\\n')"));
                strips = 0;
                foreach (string row in rows)
                {
                    string[] c = row.Split('|');
                    if (c.Length != 6) { misses.Add($"manifest row '{row}' has {c.Length} fields"); continue; }
                    byte[] rgba = host.EvaluateBytes(
                        $"globalThis.__hh9a.strip({Js(c[1])},{Js(c[2])},{c[3]},{c[4]})");
                    string sha = Sha256Hex9(rgba);
                    if (sha != c[5]) misses.Add($"{c[0]}: the rig renders {sha}, the manifest says {c[5]}");
                    strips++;
                }
            }
            finally { host.Execute("delete globalThis.__hh9man;"); }
            return misses;
        }

        /// <summary><c>runChecks(preset)</c> today against the committed golden report: every
        /// row's pass and value, the gate flags and the gated totals. Needs
        /// <see cref="Load9Checks"/>. Returns one line per difference.</summary>
        public static List<string> GoldenDrift9(IRigScriptHost host, string kitRoot, string preset,
                                                out int passed, out int of)
        {
            Install9Audit(host);
            AssertPreset9(host, preset);
            host.Execute($"globalThis.__hh9gold=({ReadKitText9(kitRoot, V9GoldenFile)});");
            try
            {
                string[] f = Lines9(host.EvaluateString($"globalThis.__hh9a.golden({Js(preset)},globalThis.__hh9gold)"));
                passed = Int9(f[0], At9);
                of = Int9(f[1], At9);
                var moved = new List<string>();
                for (int i = 2; i < f.Length; i++) moved.Add(f[i]);
                return moved;
            }
            finally { host.Execute("delete globalThis.__hh9gold;"); }
        }

        // ---------------------------------------------------------------------------------------
        // The rig's own posed geometry, for a def to be replayed against
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rig 9's own posed corners for <paramref name="clipName"/> frame <paramref name="frame"/>
        /// (<c>posed(evalClip(...))</c>, the geometry its renders paint), laid out in the corner
        /// order of the bind mesh that keeps <paramref name="restGroups"/>: x, y, z per corner.
        /// A corner whose face group the frame does not show (a blink shuts the open eyes) is NaN,
        /// because the def binds the rest face and the rig draws another one there.
        /// </summary>
        public static double[] PosedCorners9(IRigScriptHost host, string preset, string[] restGroups,
                                             string clipName, int frame)
        {
            Install9Audit(host);
            var groups = new StringBuilder("[");
            foreach (string g in restGroups) groups.Append(Js(g)).Append(',');
            groups.Append(']');
            byte[] raw = host.EvaluateBytes(
                $"globalThis.__hh9a.posedCorners({Js(preset)},{Js(clipName)}," +
                $"{frame.ToString(CultureInfo.InvariantCulture)},{groups})");
            if (raw.Length % 24 != 0)
                throw new InvalidOperationException($"Rig 9 posed {raw.Length} bytes, not whole corners.");
            var xyz = new double[raw.Length / 8];
            Buffer.BlockCopy(raw, 0, xyz, 0, raw.Length);
            return xyz;
        }

        static string Sha256Hex9(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
