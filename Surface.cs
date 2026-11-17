namespace ThunderFeet
{
    /// <summary>
    /// Every ground ThunderFeet can tell apart. Built pieces (WearNTear material) and painted terrain are separate
    /// families on purpose: a stone FLOOR you built is <see cref="BuiltStone"/>, the hoe's paved ROAD is <see cref="PavedRoad"/>.
    /// Never rename one: configs and logs refer to them by name.
    /// </summary>
    public enum Surface
    {
        None = 0,

        // Painted terrain (Heightmap paint mask: r = dirt, g = cultivated, b = paved). Paint wins over the biome.
        PavedRoad,
        DirtPath,
        Cultivated,

        // Natural terrain by biome (unpainted, not steep).
        MeadowsGrass,
        ForestFloor,
        PlainsGrass,
        SwampMud,
        Snow,
        DeepSnow,
        MistlandsGround,
        AshlandsGround,
        Lava,
        Seabed,
        RockSlope,

        // Built pieces by WearNTear.MaterialType.
        BuiltWood,
        BuiltCoreWood,
        BuiltTimberwood,
        BuiltStone,
        BuiltBlackMarble,
        BuiltGrausten,
        BuiltAncient,
        BuiltIron,
        BuiltIce,

        // Everything else you can stand on.
        ShipDeck,
        NaturalRock,
        NaturalWood,
        Ice,
        DungeonFloor,
        ShallowWater,
        Tar,
        Other,
    }

    /// <summary>
    /// The six icons the artist draws (icons/&lt;Name&gt;.png). Many surfaces share one; the surface's own name shows under it.
    /// The paved road has its own, so it never looks like built stone.
    /// </summary>
    public enum IconGroup
    {
        Road,   // paved road
        Stone,  // built stone / marble / grausten / ancient / iron, natural rock, rock slope, dungeon floor
        Wood,   // built wood / core wood / timberwood, ship deck, logs
        Earth,  // dirt path, cultivated, swamp mud, seabed, Mistlands and Ashlands ground, lava, tar, other
        Grass,  // Meadows, Black Forest, Plains
        Frost,  // snow, deep snow, ice, built ice, shallow water
    }

    internal sealed class SurfaceInfo
    {
        public Surface Surface;
        public string Section;
        public string DisplayName;
        public string Description;
        public int DefaultSprint;
        public int DefaultJog;
        /// <summary>Vanilla prefabs whose icon stands in until icons/&lt;Surface&gt;.png exists. Piece prefabs first, then items.</summary>
        public string[] FallbackIcons;
        public IconGroup Icon => GroupOf(Surface);
        /// <summary>Tint for the generated disc used when neither a PNG nor a vanilla icon is found.</summary>
        public UnityEngine.Color32 Tint;

        public static readonly SurfaceInfo[] All =
        {
            I(Surface.PavedRoad,       "01 Paved road",        "Paved road",        "Terrain the hoe has paved (the stone road texture). NOT built stone floors.", 115, 110, new byte[]{150,150,160}, "paved_road_v2", "paved_road", "Stone"),
            I(Surface.DirtPath,        "02 Dirt path",         "Dirt path",         "Terrain painted to dirt: hoe path, level ground, raise ground, dug ground.",    105, 103, new byte[]{130, 90, 55}, "path_v2", "mud_road_v2", "Hoe"),
            I(Surface.Cultivated,      "03 Cultivated soil",   "Cultivated soil",   "Terrain worked by the cultivator (farmland).",                                   90,  95, new byte[]{ 90, 60, 35}, "cultivate_v2", "cultivate", "Cultivator"),
            I(Surface.MeadowsGrass,    "04 Meadows grass",     "Grass",             "Unpainted, gentle Meadows ground.",                                             100, 100, new byte[]{ 90,170, 60}, "Dandelion"),
            I(Surface.ForestFloor,     "05 Black Forest floor","Forest floor",      "Unpainted, gentle Black Forest ground.",                                          95,  97, new byte[]{ 60, 90, 45}, "Thistle", "FirCone", "PineCone"),
            I(Surface.PlainsGrass,     "06 Plains grass",      "Plains grass",      "Unpainted, gentle Plains ground.",                                              100, 100, new byte[]{190,180, 80}, "Barley"),
            I(Surface.SwampMud,        "07 Swamp mud",         "Swamp mud",         "Unpainted, gentle Swamp ground.",                                                85,  90, new byte[]{ 70, 65, 40}, "Guck", "Ooze"),
            I(Surface.Snow,            "08 Snow",              "Snow",              "Unpainted Mountain / Deep North snow cover.",                                    90,  95, new byte[]{230,235,245}, "FreezeGland"),
            I(Surface.DeepSnow,        "09 Deep snow",         "Deep snow",         "Deep North drifts you sink into.",                                               75,  80, new byte[]{200,215,240}, "Frostwood", "FreezeGland"),
            I(Surface.MistlandsGround, "10 Mistlands ground",  "Mistlands ground",  "Unpainted Mistlands ground.",                                                    95,  97, new byte[]{ 90, 80,110}, "MushroomJotunPuffs", "Sap"),
            I(Surface.AshlandsGround,  "11 Ashlands ground",   "Ashlands ground",   "Unpainted Ashlands ground (not lava).",                                           95,  97, new byte[]{ 60, 50, 50}, "CharredBone", "SulfurStone"),
            I(Surface.Lava,            "12 Lava",              "Lava",              "Ashlands lava (vanilla already slows you near lava; this stacks on top).",      100, 100, new byte[]{230, 90, 20}, "SurtlingCore", "Obsidian"),
            I(Surface.Seabed,          "13 Seabed",            "Seabed",            "Ocean-biome terrain: the sea floor and sandbanks.",                              90,  95, new byte[]{210,190,140}, "Chitin", "Coal"),
            I(Surface.RockSlope,       "14 Rock slope",        "Rock slope",        "Unpainted terrain steeper than the rock-slope angle (the game draws rock there).",95,  97, new byte[]{110,110,115}, "Stone"),
            I(Surface.BuiltWood,       "15 Built wood",        "Wood floor",        "Pieces of the Wood material (floors, stairs, docks).",                          105, 105, new byte[]{170,120, 70}, "wood_floor", "Wood"),
            I(Surface.BuiltCoreWood,   "16 Built core wood",   "Core wood",         "Pieces of the HardWood material (core wood poles, beams).",                     105, 105, new byte[]{140, 95, 55}, "RoundLog"),
            I(Surface.BuiltTimberwood, "17 Built timberwood",  "Timberwood",        "Pieces of the Timberwood material (Deep North timber).",                        105, 105, new byte[]{150,130,110}, "Frostwood"),
            I(Surface.BuiltStone,      "18 Built stone",       "Stone floor",       "Pieces of the Stone material (stone floors, stairs, walls). NOT the paved road.",110, 105, new byte[]{160,160,160}, "stone_floor_2x2", "Stone"),
            I(Surface.BuiltBlackMarble,"19 Built black marble","Black marble",      "Pieces of the Marble material (black marble).",                                 110, 105, new byte[]{ 50, 50, 60}, "blackmarble_floor", "BlackMarble"),
            I(Surface.BuiltGrausten,   "20 Built grausten",    "Grausten",          "Pieces of the Ashstone material (grausten).",                                   110, 105, new byte[]{ 80, 75, 75}, "Grausten"),
            I(Surface.BuiltAncient,    "21 Built ancient",     "Ancient structure", "Pieces of the Ancient material.",                                               105, 105, new byte[]{120,110, 90}, "YggdrasilWood", "ElderBark"),
            I(Surface.BuiltIron,       "22 Built iron",        "Iron floor",        "Pieces of the Iron material.",                                                  105, 105, new byte[]{120,125,135}, "Iron"),
            I(Surface.BuiltIce,        "23 Built ice",         "Ice blocks",        "Pieces of the Ice material.",                                                   100, 100, new byte[]{180,220,250}, "Ice", "FreezeGland"),
            I(Surface.ShipDeck,        "24 Ship deck",         "Ship deck",         "Standing on a ship.",                                                           100, 100, new byte[]{150,110, 70}, "Raft", "Karve", "Wood"),
            I(Surface.NaturalRock,     "25 Natural rock",      "Rock",              "Boulders, rock formations, mineable rock.",                                      95,  97, new byte[]{120,120,120}, "Stone"),
            I(Surface.NaturalWood,     "26 Natural wood",      "Log",               "Fallen logs and trees.",                                                        100, 100, new byte[]{120, 85, 50}, "Wood"),
            I(Surface.Ice,             "27 Ice",               "Ice",               "Natural ice (frozen ground marked as ice).",                                    100, 100, new byte[]{170,210,240}, "FreezeGland"),
            I(Surface.DungeonFloor,    "28 Dungeon floor",     "Dungeon floor",     "Floors inside dungeons and interiors.",                                         100, 100, new byte[]{ 90, 85, 80}, "Stone"),
            I(Surface.ShallowWater,    "29 Shallow water",     "Shallow water",     "Wading (on the bottom, not swimming). Vanilla water drag still applies.",      100, 100, new byte[]{ 70,130,200}, "Ooze"),
            I(Surface.Tar,             "30 Tar",               "Tar",               "Wading in tar. Vanilla tar drag still applies.",                               100, 100, new byte[]{ 20, 20, 20}, "Tar"),
            I(Surface.Other,           "31 Other",             "Other ground",      "Anything not listed above.",                                                    100, 100, new byte[]{128,128,128}),
        };

        internal static IconGroup GroupOf(Surface s)
        {
            switch (s)
            {
                case Surface.PavedRoad:
                    return IconGroup.Road;
                case Surface.BuiltStone: case Surface.BuiltBlackMarble: case Surface.BuiltGrausten: case Surface.BuiltAncient:
                case Surface.BuiltIron: case Surface.NaturalRock: case Surface.RockSlope: case Surface.DungeonFloor:
                    return IconGroup.Stone;
                case Surface.BuiltWood: case Surface.BuiltCoreWood: case Surface.BuiltTimberwood: case Surface.ShipDeck: case Surface.NaturalWood:
                    return IconGroup.Wood;
                case Surface.MeadowsGrass: case Surface.ForestFloor: case Surface.PlainsGrass:
                    return IconGroup.Grass;
                case Surface.Snow: case Surface.DeepSnow: case Surface.Ice: case Surface.BuiltIce: case Surface.ShallowWater:
                    return IconGroup.Frost;
                default:
                    return IconGroup.Earth;
            }
        }

        private static SurfaceInfo I(Surface s, string section, string display, string desc, int sprint, int jog, byte[] tint, params string[] icons)
        {
            return new SurfaceInfo
            {
                Surface = s, Section = section, DisplayName = display, Description = desc,
                DefaultSprint = sprint, DefaultJog = jog, FallbackIcons = icons,
                Tint = new UnityEngine.Color32(tint[0], tint[1], tint[2], 255),
            };
        }
    }
}
