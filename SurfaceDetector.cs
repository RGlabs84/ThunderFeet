using System.Collections.Generic;
using UnityEngine;

namespace ThunderFeet
{
    /// <summary>
    /// Works out what the local player is standing on. Order matters:
    ///   1. liquid (wading in water / tar)
    ///   2. terrain (Heightmap): lava, then the PAINT mask (paved / dirt / cultivated wins over the biome), then steep rock, then the biome
    ///   3. a ship, a FootStepCollider's own material, a built piece's WearNTear material, natural rock / wood, dungeon floor
    /// Built stone (a piece) and the paved road (terrain paint) are separate branches, so they can never be mixed up.
    /// Read from Valheim 1.0.21: FootStep.GetGroundMaterial, Heightmap.GetGroundMaterial / IsCleared / PaintCleared.
    /// </summary>
    internal static class SurfaceDetector
    {
        // Non-terrain colliders never change what they are made of, so the answer is kept per collider.
        private static readonly Dictionary<int, Surface> s_colliderCache = new Dictionary<int, Surface>();

        internal static Surface Detect(Player player)
        {
            if (player.InLiquid())
                return player.InWater() ? Surface.ShallowWater : Surface.Tar;

            Collider ground = player.GetLastGroundCollider();
            if (ground == null)
                return Surface.None;

            Heightmap hm = ground.GetComponent<Heightmap>();
            if (hm != null)
                return Terrain(hm, player.transform.position, player.GetLastGroundNormal());

            return Cached(ground, player.InInterior());
        }

        private static int s_groundMask = -1;

        /// <summary>
        /// The surface under a world point (a footstep of ANY player, including remote ones, whose ground contact this client does not
        /// track). Casts a short ray down onto the same layers characters stand on.
        /// </summary>
        internal static Surface DetectAt(Vector3 point, bool inInterior)
        {
            if (s_groundMask == -1)
                s_groundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

            if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, s_groundMask, QueryTriggerInteraction.Ignore))
                return Surface.None;

            Heightmap hm = hit.collider.GetComponent<Heightmap>();
            if (hm != null)
                return Terrain(hm, hit.point, hit.normal);
            return Cached(hit.collider, inInterior);
        }

        private static Surface Cached(Collider ground, bool inInterior)
        {
            int id = ground.GetInstanceID();
            if (!s_colliderCache.TryGetValue(id, out Surface s))
            {
                s = Object(ground, inInterior);
                if (s_colliderCache.Count > 4096) s_colliderCache.Clear();
                s_colliderCache[id] = s;
            }
            return s;
        }

        private static Surface Terrain(Heightmap hm, Vector3 point, Vector3 normal)
        {
            Heightmap.Biome biome = hm.GetBiome(point);

            if (biome == Heightmap.Biome.AshLands && hm.IsLava(point))
                return Surface.Lava;

            Surface painted = Paint(hm, point, biome);
            if (painted != Surface.None)
                return painted;

            float angle = Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
            if (angle >= ThunderConfig.RockSlopeAngle.Value)
                return Surface.RockSlope;

            switch (biome)
            {
                case Heightmap.Biome.Meadows: return Surface.MeadowsGrass;
                case Heightmap.Biome.BlackForest: return Surface.ForestFloor;
                case Heightmap.Biome.Plains: return Surface.PlainsGrass;
                case Heightmap.Biome.Swamp: return Surface.SwampMud;
                case Heightmap.Biome.Mountain: return Surface.Snow;
                case Heightmap.Biome.DeepNorth: return Surface.Snow;
                case Heightmap.Biome.Mistlands: return Surface.MistlandsGround;
                case Heightmap.Biome.AshLands: return Surface.AshlandsGround;
                case Heightmap.Biome.Ocean: return Surface.Seabed;
                default: return Surface.Other;
            }
        }

        // Paint mask channels (Heightmap.m_paintMask*): Dirt (1,0,0), Cultivated (0,1,0), Paved (0,0,1), DeepSnow (1,1,1).
        // In the Deep North the GREEN channel is the deep-snow amount, not cultivation (vanilla: 0.12 = deep, 0.6 = very deep).
        private static Surface Paint(Heightmap hm, Vector3 point, Heightmap.Biome biome)
        {
            if (hm.GetPaintMask() == null)
                return Surface.None;

            // Same half-cell offset the game uses in IsCleared / PaintCleared.
            point.x -= 0.5f;
            point.z -= 0.5f;
            Color c = hm.GetPaintMask(point);
            float t = ThunderConfig.PaintThreshold.Value;
            bool r = c.r > t, g = c.g > t, b = c.b > t;

            if (biome == Heightmap.Biome.DeepNorth)
            {
                if (b && !r && !g) return Surface.PavedRoad;
                if (r && !g && !b) return Surface.DirtPath;
                if (c.g > 0.12f) return Surface.DeepSnow;
                return Surface.None;
            }

            if (r && g && b) return Surface.DeepSnow;
            if (!r && !g && !b) return Surface.None;

            // Where two paints blend (a road's edge into a path), the stronger one wins.
            if (b && c.b >= c.r && c.b >= c.g) return Surface.PavedRoad;
            if (r && c.r >= c.g) return Surface.DirtPath;
            if (g) return Surface.Cultivated;
            return r ? Surface.DirtPath : Surface.PavedRoad;
        }

        private static Surface Object(Collider ground, bool inInterior)
        {
            if (ground.GetComponentInParent<Ship>() != null)
                return Surface.ShipDeck;

            FootStepCollider fsc = ground.GetComponent<FootStepCollider>();
            if (fsc != null)
            {
                Surface s = FromFootStep(fsc.m_material);
                if (s != Surface.None) return s;
            }

            WearNTear wnt = ground.GetComponentInParent<WearNTear>();
            if (wnt != null)
                return FromMaterial(wnt.m_materialType);

            if (ground.GetComponentInParent<MineRock>() != null || ground.GetComponentInParent<MineRock5>() != null)
                return Surface.NaturalRock;

            if (ground.GetComponentInParent<TreeLog>() != null || ground.GetComponentInParent<TreeBase>() != null)
                return Surface.NaturalWood;

            if (inInterior)
                return Surface.DungeonFloor;

            // Static boulders and cliffs carry no component, only a name.
            string n = ground.transform.root.name.ToLowerInvariant();
            if (n.Contains("rock") || n.Contains("cliff") || n.Contains("stone") || n.Contains("boulder"))
                return Surface.NaturalRock;

            return Surface.Other;
        }

        private static Surface FromMaterial(WearNTear.MaterialType m)
        {
            switch (m)
            {
                case WearNTear.MaterialType.Wood: return Surface.BuiltWood;
                case WearNTear.MaterialType.HardWood: return Surface.BuiltCoreWood;
                case WearNTear.MaterialType.Timberwood: return Surface.BuiltTimberwood;
                case WearNTear.MaterialType.Stone: return Surface.BuiltStone;
                case WearNTear.MaterialType.Marble: return Surface.BuiltBlackMarble;
                case WearNTear.MaterialType.Ashstone: return Surface.BuiltGrausten;
                case WearNTear.MaterialType.Ancient: return Surface.BuiltAncient;
                case WearNTear.MaterialType.Iron: return Surface.BuiltIron;
                case WearNTear.MaterialType.Ice: return Surface.BuiltIce;
                default: return Surface.Other;
            }
        }

        private static Surface FromFootStep(FootStep.GroundMaterial m)
        {
            switch (m)
            {
                case FootStep.GroundMaterial.Stone: return Surface.NaturalRock;
                case FootStep.GroundMaterial.Wood: return Surface.NaturalWood;
                case FootStep.GroundMaterial.Metal: return Surface.BuiltIron;
                case FootStep.GroundMaterial.Ice: return Surface.Ice;
                case FootStep.GroundMaterial.Snow: return Surface.Snow;
                case FootStep.GroundMaterial.SnowDeep:
                case FootStep.GroundMaterial.SnowVeryDeep: return Surface.DeepSnow;
                case FootStep.GroundMaterial.Mud: return Surface.SwampMud;
                case FootStep.GroundMaterial.Grass: return Surface.MeadowsGrass;
                case FootStep.GroundMaterial.Ashlands: return Surface.AshlandsGround;
                case FootStep.GroundMaterial.Lava: return Surface.Lava;
                case FootStep.GroundMaterial.Water: return Surface.ShallowWater;
                case FootStep.GroundMaterial.Tar: return Surface.Tar;
                default: return Surface.None; // Default / GenericGround: fall through to the piece / object checks
            }
        }
    }
}
