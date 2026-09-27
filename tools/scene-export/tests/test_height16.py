"""Height precision contract; every PNG is generated, never stored as an LFS fixture."""
import os
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from hhexport import heightmap, tide


def chunk(kind, body):
    return (struct.pack('>I', len(body)) + kind + body
            + struct.pack('>I', zlib.crc32(kind + body) & 0xffffffff))


def png_bytes(rows, depth=16, filters=None, colour=0, interlace=0, raw=None):
    width, height = len(rows[0]), len(rows)
    if raw is None:
        previous = bytes(width * (depth // 8))
        raw = bytearray()
        for row, mode in zip(rows, filters or [0] * height):
            line = bytes(row) if depth == 8 else struct.pack('>' + 'H' * width, *row)
            bpp = depth // 8
            raw.append(mode)
            for i, value in enumerate(line):
                a = line[i - bpp] if i >= bpp else 0
                b = previous[i]
                c = previous[i - bpp] if i >= bpp else 0
                # Independent PNG encoder: choose Paeth's closest predictor, stable on ties.
                p = a + b - c
                paeth = min((a, b, c), key=lambda v: abs(p - v))
                predictor = (0, a, b, (a + b) // 2, paeth)[mode]
                raw.append((value - predictor) & 255)
            previous = line
    header = struct.pack('>IIBBBBB', width, height, depth, colour, 0, 0, interlace)
    compressed = zlib.compress(raw)
    split = len(compressed) // 2
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', header)
            + chunk(b'IDAT', compressed[:split]) + chunk(b'IDAT', compressed[split:])
            + chunk(b'IEND', b''))


class Height16Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = SimpleNamespace(abs=lambda rel: os.path.join(self.temp.name, rel),
                                    exists=lambda rel: os.path.exists(os.path.join(self.temp.name, rel)))
        self.map = dict(texture='height.png', minElevation=-4.0, maxElevation=6.0,
                        worldSizeMeters=[5, 5], worldCenter=[2.5, 2.5])

    def write(self, data):
        Path(self.repo.abs('height.png')).write_bytes(data)

    def test_16_bit_all_five_filters_at_odd_width(self):
        rows = [[0, 65535, 256, 32769, 65280], [65535, 1, 65534, 255, 60000],
                [91, 47000, 2, 32000, 65530], [333, 50123, 1000, 256, 60001],
                [61000, 111, 22222, 44444, 0]]
        decoded = heightmap.decode_r8(png_bytes(rows, filters=range(5)))
        self.assertIsNotNone(decoded, '16-bit PNG rejected')
        self.assertEqual((decoded.width, decoded.height, decoded.bit_depth, decoded.max_code),
                         (5, 5, 16, 65535.0))
        self.assertEqual([list(row) for row in decoded.rows], rows)

    def test_widening_preserves_all_readers_and_quantum_ratio(self):
        rows = [[(y * 51 + x * 17) % 256 for x in range(5)] for y in range(5)]
        outputs = []
        for depth, codes in [(8, rows), (16, [[257 * v for v in row] for row in rows])]:
            self.write(png_bytes(codes, depth, range(5)))
            with patch.object(heightmap, 'read_bands', return_value=(-4, [('land', 1), ('sand', -4)])):
                contour, reason = heightmap.contour(self.repo, self.map, 5, 5, [0, 5])
            self.assertIsNone(reason)
            wash, reason = heightmap.sample_field(self.repo, self.map, 5, 5, [0, 5], 2)
            self.assertIsNone(reason)
            field, reason = heightmap.sample_field_full(self.repo, self.map, 5, 5, [0, 5])
            self.assertIsNone(reason)
            sampler = heightmap.Sampler(self.repo, self.map)
            points = [sampler.at(x + .5, y + .5) for y in range(5) for x in range(5)]
            outputs.append((contour, wash, field, points, sampler.quantum))
        a, b = outputs
        self.assertEqual(a[:2], b[:2])
        self.assertEqual(a[2]['values'], b[2]['values'])
        self.assertEqual(a[3], b[3])
        self.assertEqual(a[4] / b[4], 257.0)
        # The package stores six decimals, so only the unrounded source quantum has an exact ratio.
        self.assertEqual(a[2]['quantumMeters'], round(a[4], 6))
        self.assertEqual(b[2]['quantumMeters'], round(b[4], 6))

    def test_substeps_reach_contour_wash_full_field_and_sampler(self):
        self.write(png_bytes([[32800] * 5] * 5))
        self.map.update(minElevation=0., maxElevation=10.)
        expected = round(32800 / 65535.0 * 10., 3)
        self.assertGreater(expected, 5.)
        self.assertLess(expected, 128 / 255.0 * 10.)
        sampler = heightmap.Sampler(self.repo, self.map)
        self.assertTrue(sampler.ready, sampler.reason)
        self.assertEqual(sampler.at(.5, .5), expected)
        wash, reason = heightmap.sample_field(self.repo, self.map, 5, 5, [0, 5], 1)
        self.assertIsNone(reason)
        self.assertEqual(wash['values'], [expected] * 25)
        field, reason = heightmap.sample_field_full(self.repo, self.map, 5, 5, [0, 5])
        self.assertIsNone(reason)
        self.assertEqual(field['values'], [[expected, 25]])
        with patch.object(heightmap, 'read_bands', return_value=(0, [('high', 5.01), ('low', 0)])):
            grid, reason = heightmap.contour(self.repo, self.map, 5, 5, [0, 5])
        self.assertIsNone(reason)
        self.assertEqual(grid, ['low'] * 25)

    def test_16_bit_notes_separate_source_quantum_from_output_step(self):
        self.write(png_bytes([[32768] * 5] * 5))
        field, reason = heightmap.sample_field_full(self.repo, self.map, 5, 5, [0, 5])
        self.assertIsNone(reason)
        self.assertIn('16-bit quantisation', field['x-note'])
        self.assertIn('0.001 m output step', field['x-note'])
        self.assertNotIn('which is finer than it', field['x-note'])
        sampler = heightmap.Sampler(self.repo, self.map)
        _, note = tide._bed_of(sampler, (.5, .5))
        self.assertIn('16 bits', note)
        self.assertIn('0.001 m output step', note)

    def test_lip_tolerance_respects_rounding_but_does_not_hide_widening_disagreement(self):
        terms = SimpleNamespace(underfoot=lambda point, seaward: (.5, .5))
        def sample(sampler, declared):
            return tide._lip_sample(terms, {'pos': [0, 0]}, 0, (0, -1), (0, 0), sampler, declared)
        self.write(png_bytes([[32768] * 5] * 5))
        sampler = heightmap.Sampler(self.repo, self.map)
        self.assertTrue(sampler.ready, sampler.reason)
        raw_metres = -4 + 32768 / 65535.0 * 10
        self.assertTrue(sample(sampler, raw_metres)['agreesWithDeclared'])
        self.assertTrue(sample(sampler, 1.0009)['agreesWithDeclared'])
        self.assertFalse(sample(sampler, 1.0011)['agreesWithDeclared'])
        self.assertIn('larger of one source quantum', sample(sampler, 1)['x-note'])
        verdicts = []
        for depth, code in [(8, 128), (16, 128 * 257)]:
            self.write(png_bytes([[code] * 5] * 5, depth))
            sampler = heightmap.Sampler(self.repo, self.map)
            verdicts.append(sample(sampler, 1)['agreesWithDeclared'])
            self.assertEqual(sampler.tolerance, max(sampler.quantum, .001))
        self.assertEqual(verdicts, [True, False])
        self.map.update(minElevation=0., maxElevation=1000.)
        sampler = heightmap.Sampler(self.repo, self.map)
        self.assertEqual(sampler.tolerance, sampler.quantum)

    def refusal(self, data, reason):
        self.write(data)
        readers = [lambda: heightmap.contour(self.repo, self.map, 5, 5, [0, 5]),
                   lambda: heightmap.sample_field(self.repo, self.map, 5, 5, [0, 5], 1),
                   lambda: heightmap.sample_field_full(self.repo, self.map, 5, 5, [0, 5])]
        for reader in readers:
            value, why = reader()
            self.assertIsNone(value)
            self.assertIn(reason, why)
        sampler = heightmap.Sampler(self.repo, self.map)
        self.assertFalse(sampler.ready)
        self.assertIn(reason, sampler.reason)
        self.assertIsNone(heightmap.decode_r8(data))
        # The strict entry point gives standalone decoder users the same diagnostic.
        with self.assertRaisesRegex(ValueError, reason):
            heightmap.decode_greyscale(data)


def refusal_test(data, reason):
    def test(self):
        self.refusal(data, reason)
    return test


for colour in (2, 3, 4, 6):
    setattr(Height16Tests, 'test_refuses_colour_type_' + str(colour),
            refusal_test(png_bytes([[0]], colour=colour), 'colour type ' + str(colour)))
for depth in (1, 2, 4):
    setattr(Height16Tests, 'test_refuses_bit_depth_' + str(depth),
            refusal_test(png_bytes([[0]], depth=depth, raw=b'\0\0'), 'bit depth ' + str(depth)))
for name, data, reason in [
    ('interlace', png_bytes([[0]], interlace=1), 'interlaced PNG'),
    ('truncated_chunk', png_bytes([[0]])[:-5], 'truncated PNG chunk'),
    ('truncated_scanline', png_bytes([[0]], raw=b'\0\0'), 'incorrect PNG scanline'),
    ('missing_iend', png_bytes([[0]])[:-12], 'missing IEND'),
    ('checksum', png_bytes([[0]])[:-1] + b'\0', 'invalid PNG chunk checksum'),
    ('unknown_filter', png_bytes([[0]], raw=b'\5\0\0'), 'unknown PNG filter 5'),
    ('lfs_pointer', b'version https://git-lfs.github.com/spec/v1\n', 'Git LFS pointer'),
    ('non_png', b'not an image', 'not a PNG'),
]:
    setattr(Height16Tests, 'test_refuses_' + name, refusal_test(data, reason))


if __name__ == '__main__':
    unittest.main()
