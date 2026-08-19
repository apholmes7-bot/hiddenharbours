"""The committed ``.unity`` read as a graph: hierarchy, world transforms, placed sprites.

The scene is a **derived copy** — both region builders call
``EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, …)`` and rebuild from zero, so the
source of truth for every placement is builder C# plus Defs. Outside Unity that C# cannot be
run, which makes the committed scene the only machine-readable materialisation of it. So this
module reads placements from the scene and the exporter stamps exactly which commit that copy
came from, and how far the builders have moved since.
"""

import math

from . import unityyaml as U

CLASS_GAMEOBJECT = 1
CLASS_TRANSFORM = 4
CLASS_SPRITE_RENDERER = 212


class Scene:
    def __init__(self, docs):
        self.docs = {d.file_id: d for d in docs}
        self.game_objects = {}
        self.transforms = {}
        self.renderers = {}
        self.behaviours = {}
        self._by_game_object = {}
        self._world_cache = {}
        self._index()

    def _index(self):
        for file_id, doc in self.docs.items():
            if doc.type_name == "GameObject":
                self.game_objects[file_id] = doc
            elif doc.type_name in ("Transform", "RectTransform"):
                self.transforms[file_id] = doc
            elif doc.type_name == "SpriteRenderer":
                self.renderers[file_id] = doc
            elif doc.type_name == "MonoBehaviour":
                self.behaviours[file_id] = doc
            owner = U.ref(doc.data.get("m_GameObject"))
            if owner:
                self._by_game_object.setdefault(owner, []).append(doc)

    # --- hierarchy ------------------------------------------------------------------------

    def transform_of(self, game_object_id):
        for doc in self._by_game_object.get(game_object_id, ()):
            if doc.type_name in ("Transform", "RectTransform"):
                return doc
        return None

    def components_of(self, game_object_id):
        return self._by_game_object.get(game_object_id, [])

    def name_of(self, game_object_id):
        doc = self.game_objects.get(game_object_id)
        return doc.data.get("m_Name", "") if doc else ""

    def is_active(self, game_object_id):
        doc = self.game_objects.get(game_object_id)
        if not doc:
            return False
        return U.as_int(doc.data.get("m_IsActive"), 1) != 0

    def hierarchy_path(self, game_object_id):
        """``Root/Child/Leaf`` for a GameObject, walking transform parents."""
        parts = []
        transform = self.transform_of(game_object_id)
        guard = 0
        while transform is not None and guard < 512:
            guard += 1
            owner = U.ref(transform.data.get("m_GameObject"))
            parts.append(self.name_of(owner))
            parent_id = U.ref(transform.data.get("m_Father"))
            transform = self.transforms.get(parent_id)
        return "/".join(reversed(parts))

    def active_in_hierarchy(self, game_object_id):
        transform = self.transform_of(game_object_id)
        guard = 0
        while transform is not None and guard < 512:
            guard += 1
            owner = U.ref(transform.data.get("m_GameObject"))
            if not self.is_active(owner):
                return False
            transform = self.transforms.get(U.ref(transform.data.get("m_Father")))
        return True

    # --- transforms -----------------------------------------------------------------------

    def world_of(self, transform_id):
        """``(x, y, z, rotationDegrees, scaleX, scaleY)`` in world space.

        Composed up the parent chain as a 2D similarity: the regions are ¾ top-down and every
        placement in both scenes is a translate/scale with at most a Z rotation, so a full 4x4
        would carry no information a 2D affine does not.
        """
        if transform_id in self._world_cache:
            return self._world_cache[transform_id]
        doc = self.transforms.get(transform_id)
        if doc is None:
            return (0.0, 0.0, 0.0, 0.0, 1.0, 1.0)
        local_position = U.vec(doc.data.get("m_LocalPosition"), "x", "y", "z")
        local_scale = U.vec(doc.data.get("m_LocalScale"), "x", "y", "z")
        local_rotation = _z_degrees(doc.data.get("m_LocalRotation"))
        parent_id = U.ref(doc.data.get("m_Father"))
        if parent_id and parent_id in self.transforms:
            px, py, pz, protation, psx, psy = self.world_of(parent_id)
            radians = math.radians(protation)
            cos, sin = math.cos(radians), math.sin(radians)
            sx, sy = local_position[0] * psx, local_position[1] * psy
            world = (
                px + sx * cos - sy * sin,
                py + sx * sin + sy * cos,
                pz + local_position[2],
                protation + local_rotation,
                psx * local_scale[0],
                psy * local_scale[1],
            )
        else:
            world = (
                local_position[0], local_position[1], local_position[2],
                local_rotation, local_scale[0], local_scale[1],
            )
        self._world_cache[transform_id] = world
        return world

    def world_of_game_object(self, game_object_id):
        transform = self.transform_of(game_object_id)
        if transform is None:
            return (0.0, 0.0, 0.0, 0.0, 1.0, 1.0)
        return self.world_of(transform.file_id)

    # --- roots ----------------------------------------------------------------------------

    def root_order(self):
        """Root GameObject ids in the scene's own ``SceneRoots`` order, so the export is stable."""
        for doc in self.docs.values():
            if doc.type_name == "SceneRoots":
                roots = []
                for entry in doc.data.get("m_Roots") or []:
                    transform = self.transforms.get(U.ref(entry))
                    if transform is not None:
                        roots.append(U.ref(transform.data.get("m_GameObject")))
                return roots
        return sorted(
            U.ref(t.data.get("m_GameObject"))
            for t in self.transforms.values()
            if not U.ref(t.data.get("m_Father"))
        )

    def walk(self):
        """Every GameObject in depth-first ``SceneRoots`` order — the scene's own ordering.

        Ordering off the scene rather than off a dict is what makes the export byte-identical
        run to run: fileIDs are stable in the committed file, and so is this walk.
        """
        seen = set()
        for root in self.root_order():
            yield from self._walk_from(root, seen)
        for game_object_id in sorted(self.game_objects):
            if game_object_id not in seen:
                yield from self._walk_from(game_object_id, seen)

    def _walk_from(self, game_object_id, seen):
        if game_object_id in seen or game_object_id not in self.game_objects:
            return
        seen.add(game_object_id)
        yield game_object_id
        transform = self.transform_of(game_object_id)
        if transform is None:
            return
        for child in transform.data.get("m_Children") or []:
            child_transform = self.transforms.get(U.ref(child))
            if child_transform is not None:
                yield from self._walk_from(U.ref(child_transform.data.get("m_GameObject")), seen)


def _z_degrees(quaternion):
    """Z rotation in degrees from a serialized quaternion."""
    if not isinstance(quaternion, dict):
        return 0.0
    x, y, z, w = U.vec(quaternion, "x", "y", "z", "w")
    if x == 0.0 and y == 0.0 and z == 0.0:
        return 0.0
    return math.degrees(math.atan2(2.0 * (w * z + x * y), 1.0 - 2.0 * (y * y + z * z)))
