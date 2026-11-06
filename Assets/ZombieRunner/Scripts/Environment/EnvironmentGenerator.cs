using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieRunner
{
    /// <summary>
    /// Builds and streams the endless track for a theme.
    /// A pool of pre-built chunk variants is recycled in front of the player, so after start-up the
    /// environment costs no allocations. Each chunk's static scenery is merged into one renderer per
    /// material. A far backdrop and the weather follow the player.
    /// </summary>
    public class EnvironmentGenerator : MonoBehaviour
    {
        public const float ChunkLength = 40f;
        public const float RoadHalfWidth = 4.3f;

        public static float DecorDensity = 1f; // set by QualityManager

        ThemeProfile theme;
        Transform follow;
        readonly List<Transform> variants = new List<Transform>();
        readonly List<Transform> active = new List<Transform>();
        float nextZ;
        Transform backdrop, weather;
        int variantCount = 8, visibleChunks = 6;
        public bool Ready { get; private set; }

        /// <summary>Builds everything for a theme. 'follow' is usually the player.</summary>
        /// <summary>How far behind the follow target chunks are kept (menus look backwards, so they keep more).</summary>
        public float keepBehind = 25f;

        public void Build(ThemeProfile profile, Transform followTarget, int variantsToBuild = 8, int visible = 6, bool withWeather = true)
        {
            theme = profile;
            follow = followTarget;
            variantCount = variantsToBuild;
            visibleChunks = visible;
            int baseSeed = profile.musicSeed * 1000;
            for (int i = 0; i < variantCount; i++)
            {
                var c = BuildChunk(profile, baseSeed + i * 17, i);
                c.gameObject.SetActive(false);
                variants.Add(c);
            }
            nextZ = -ChunkLength;
            for (int i = 0; i < visibleChunks; i++) PlaceNext();
            backdrop = BuildBackdrop(profile);
            if (withWeather) weather = BuildWeather(profile);
            Ready = true;
        }

        void PlaceNext()
        {
            // pick a variant that is not currently placed
            Transform pick = null;
            for (int tries = 0; tries < 20 && pick == null; tries++)
            {
                var c = variants[Random.Range(0, variants.Count)];
                if (!active.Contains(c)) pick = c;
            }
            if (pick == null) foreach (var c in variants) if (!active.Contains(c)) { pick = c; break; }
            if (pick == null) return;
            pick.localPosition = new Vector3(0, 0, nextZ);
            pick.gameObject.SetActive(true);
            active.Add(pick);
            nextZ += ChunkLength;
        }

        void LateUpdate()
        {
            if (!Ready || follow == null) return;
            var local = transform.InverseTransformPoint(follow.position);
            float pz = local.z;
            while (active.Count > 0 && active[0].localPosition.z + ChunkLength < pz - keepBehind)
            {
                active[0].gameObject.SetActive(false);
                active.RemoveAt(0);
                PlaceNext();
            }
            if (backdrop) backdrop.localPosition = new Vector3(0, 0, pz);
            if (weather) weather.localPosition = new Vector3(local.x * 0.5f, 0, pz);
        }

        /// <summary>Shifts everything back when the floating origin moves.</summary>
        public void Shift(float dz)
        {
            foreach (var c in variants) c.localPosition += new Vector3(0, 0, dz);
            nextZ += dz;
        }

        // =====================================================================================
        // CHUNKS
        // =====================================================================================
        Transform BuildChunk(ThemeProfile p, int seed, int index)
        {
            var root = new GameObject($"Chunk_{p.id}_{index}").transform;
            root.SetParent(transform, false);
            var r = new Rng(seed);
            var statics = ZRUtil.Child(root, "Static");
            var dynamics = ZRUtil.Child(root, "Dynamic");

            // Road surface
            var road = new GameObject("Road");
            road.transform.SetParent(statics, false);
            road.AddComponent<MeshFilter>().sharedMesh = MeshGen.Ground(RoadHalfWidth * 2f, ChunkLength, 6, 8, 0.25f, p.road == RoadStyle.Mud ? 0.08f : 0f, seed % 7);
            road.AddComponent<MeshRenderer>().sharedMaterial = Palette.Road(p);

            switch (p.id)
            {
                case ThemeId.Forest: ForestChunk(p, statics, dynamics, r); break;
                case ThemeId.Desert: DesertChunk(p, statics, dynamics, r); break;
                case ThemeId.DarkCity: DarkCityChunk(p, statics, dynamics, r); break;
                case ThemeId.AbandonedCity: AbandonedChunk(p, statics, dynamics, r); break;
            }
            ImportedDecor(p, statics, r);

            // Batch the static scenery.
            var merged = MeshMerge.MergeChildren(statics, "Merged", true, true);
            merged.isStatic = false;
            // Small details fade out with distance (chunk-level LOD).
            return root;
        }

        static GameObject Place(PropBuilder b, string name, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            var go = b.Build(name, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static int Count(float n) => Mathf.Max(0, Mathf.RoundToInt(n * DecorDensity));

        static float SideX(Rng r, float min, float max) => r.Sign * r.Range(min, max);

        void Verges(ThemeProfile p, Transform parent, int seed, float innerX, float width, Material mat, float y = -0.03f, float noise = 0.4f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var g = new GameObject("Verge");
                g.transform.SetParent(parent, false);
                g.transform.localPosition = new Vector3(s * (innerX + width * 0.5f), y, 0);
                g.AddComponent<MeshFilter>().sharedMesh = MeshGen.Ground(width, ChunkLength, 8, 8, 0.2f, noise, seed + s);
                g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        void LaneLines(Transform parent, Material mat, bool dashed, bool edges)
        {
            var b = new PropBuilder();
            for (int lane = -1; lane <= 1; lane += 2)
            {
                float x = lane * ZRUtil.LaneWidth * 0.5f;
                if (dashed)
                    for (float z = 1f; z < ChunkLength; z += 8f) b.Box(new Vector3(x, 0.012f, z + 1.5f), new Vector3(0.14f, 0.02f, 3f), mat);
                else b.Box(new Vector3(x, 0.012f, ChunkLength * 0.5f), new Vector3(0.14f, 0.02f, ChunkLength), mat);
            }
            if (edges)
                for (int s = -1; s <= 1; s += 2)
                    b.Box(new Vector3(s * (RoadHalfWidth - 0.25f), 0.012f, ChunkLength * 0.5f), new Vector3(0.16f, 0.02f, ChunkLength), mat);
            b.Build("LaneLines", parent, false);
        }

        void Sidewalks(Transform parent, Material mat, Material curb)
        {
            var b = new PropBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(s * (RoadHalfWidth + 2.1f), 0.09f, ChunkLength * 0.5f), new Vector3(4.2f, 0.18f, ChunkLength), mat, default, 0.5f);
                b.Box(new Vector3(s * (RoadHalfWidth + 0.08f), 0.1f, ChunkLength * 0.5f), new Vector3(0.18f, 0.2f, ChunkLength), curb);
            }
            b.Build("Sidewalks", parent);
        }

        // ------------------------------------------------------------------ FOREST
        void ForestChunk(ThemeProfile p, Transform s, Transform d, Rng r)
        {
            Verges(p, s, r.Range(0, 99), RoadHalfWidth - 0.2f, 60f, Palette.Verge(p), -0.03f, 0.6f);
            // muddy road edges / puddles
            for (int i = 0; i < Count(4); i++)
                Place(new PropBuilder().Sphere(Vector3.zero, new Vector3(r.Range(1f, 2.5f), 0.04f, r.Range(1.5f, 4f)), Palette.Puddle, 0.3f, i), "Puddle", s,
                    new Vector3(r.Sign * r.Range(3.6f, 4.8f), 0.0f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            // Dense tree wall: near layer and back layers
            for (int i = 0; i < Count(26); i++)
            {
                float x = SideX(r, 6.5f, 34f), z = r.Range(0, ChunkLength);
                float h = r.Range(11f, 22f) * (Mathf.Abs(x) > 15f ? 1.15f : 1f);
                if (r.Chance(0.7f)) Place(PropFactory.PineTree(r.Range(0, 9999), h), "Pine", s, new Vector3(x, 0, z), r.Range(0, 360f));
                else Place(PropFactory.BroadTree(r.Range(0, 9999), h * 0.7f, r.Chance(0.2f)), "Tree", s, new Vector3(x, 0, z), r.Range(0, 360f));
            }
            for (int i = 0; i < Count(14); i++)
                Place(PropFactory.Bush(r.Range(0, 999), r.Range(0.8f, 1.8f)), "Bush", s, new Vector3(SideX(r, 5f, 14f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            for (int i = 0; i < Count(6); i++)
                Place(PropFactory.Rock(r.Range(0, 999), r.Range(0.6f, 2.2f)), "Rock", s, new Vector3(SideX(r, 5.5f, 18f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            for (int i = 0; i < Count(3); i++)
            {
                var log = new PropBuilder().Cyl(new Vector3(0, 0.35f, 0), 0.35f, r.Range(3f, 6f), Palette.DeadBark, new Vector3(0, 0, 90), 10)
                    .Sphere(new Vector3(0, 0.66f, 0), new Vector3(2f, 0.1f, 0.5f), Palette.Moss, 0.3f, i);
                Place(log, "Log", s, new Vector3(SideX(r, 6f, 16f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            }
            for (int i = 0; i < Count(4); i++)
                Place(new PropBuilder().Cyl(new Vector3(0, 0.3f, 0), 0.4f, 0.6f, Palette.Bark, default, 10, 0.32f).Sphere(new Vector3(0, 0.6f, 0), new Vector3(0.7f, 0.06f, 0.7f), Palette.Wood), "Stump", s,
                    new Vector3(SideX(r, 5.5f, 12f), 0, r.Range(0, ChunkLength)), 0);
            // Abandoned camp
            if (r.Chance(0.45f))
            {
                float side = r.Sign, z = r.Range(8f, 32f), x = side * r.Range(8f, 11f);
                Place(PropFactory.Tent(r.Range(0, 99)), "Tent", s, new Vector3(x + side * 2.5f, 0, z), r.Range(-30f, 30f) + 90f * side);
                Place(PropFactory.Tent(r.Range(0, 99)), "Tent", s, new Vector3(x + side * 1.5f, 0, z + 4f), r.Range(-30f, 30f));
                Place(PropFactory.Campfire(r.Range(0, 99)), "Campfire", s, new Vector3(x - side * 0.5f, 0, z + 2f), 0);
                Place(PropFactory.Barrels(r.Range(0, 99)), "Barrels", s, new Vector3(x + side * 3.5f, 0, z - 2f), r.Range(0, 360f));
                if (r.Chance(0.6f)) FX.Fire(d, new Vector3(x - side * 0.5f, 0.1f, z + 2f), 0.35f);
            }
            // Broken vehicle in the trees
            if (r.Chance(0.35f))
                Place(PropFactory.Car(r.Range(0, 99), Palette.Rust, true, r.Chance(0.3f)), "Wreck", s, new Vector3(SideX(r, 7f, 12f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            // Sun shafts through the canopy
            for (int i = 0; i < Count(2); i++)
                FX.LightShaft(d, new Vector3(SideX(r, 3f, 9f), 9f, r.Range(0, ChunkLength)), p.sunEuler, 22f, r.Range(1.2f, 2.2f), new Color(1f, 0.92f, 0.7f, 0.07f));
        }

        // ------------------------------------------------------------------ DESERT
        void DesertChunk(ThemeProfile p, Transform s, Transform d, Rng r)
        {
            Verges(p, s, r.Range(0, 99), RoadHalfWidth - 0.1f, 70f, Palette.Verge(p), -0.04f, 0.8f);
            LaneLines(s, Palette.LaneLine, true, true);
            // sand drifting over road edges
            for (int i = 0; i < Count(5); i++)
                Place(new PropBuilder().Sphere(Vector3.zero, new Vector3(r.Range(1.5f, 3f), 0.25f, r.Range(3f, 7f)), Palette.Sand, 0.3f, i, default, true), "Drift", s,
                    new Vector3(r.Sign * r.Range(4.2f, 5.2f), 0, r.Range(0, ChunkLength)), r.Range(-10f, 10f));
            for (int i = 0; i < Count(5); i++)
            {
                float x = SideX(r, 14f, 45f);
                Place(PropFactory.Dune(r.Range(0, 99), new Vector3(r.Range(12f, 26f), r.Range(4f, 9f), r.Range(14f, 30f))), "Dune", s, new Vector3(x, -0.5f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            }
            for (int i = 0; i < Count(5); i++)
                Place(PropFactory.Rock(r.Range(0, 999), r.Range(0.8f, 3f), Palette.DesertRock), "Rock", s, new Vector3(SideX(r, 6f, 22f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            for (int i = 0; i < Count(3); i++)
                Place(PropFactory.BroadTree(r.Range(0, 999), r.Range(3f, 6f), true), "DeadTree", s, new Vector3(SideX(r, 7f, 20f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            for (int i = 0; i < Count(2); i++)
                Place(PropFactory.Cactus(r.Range(0, 999), r.Range(2f, 4f)), "Cactus", s, new Vector3(SideX(r, 7f, 20f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));
            for (int i = 0; i < Count(2); i++)
                Place(PropFactory.Bush(r.Range(0, 999), r.Range(0.5f, 1f), MaterialLibrary.Lit("drybush", new Color(0.45f, 0.4f, 0.28f), 0.1f, 0f, TexKind.Foliage)), "Shrub", s,
                    new Vector3(SideX(r, 5f, 15f), 0, r.Range(0, ChunkLength)), 0);
            // power line along the road
            Place(PropFactory.PowerPole(r.Range(0, 99)), "Pole", s, new Vector3(-7.5f, 0, r.Range(0f, 10f)), 0);
            if (r.Chance(0.5f)) Place(PropFactory.Barrels(r.Range(0, 99)), "Barrels", s, new Vector3(SideX(r, 5.5f, 8f), 0, r.Range(0, ChunkLength)), r.Range(0, 360f));

            float side = r.Sign;
            switch (r.Range(0, 4))
            {
                case 0: // abandoned military column
                    Place(PropFactory.Tank(r.Range(0, 99), Palette.DesertMilitary), "Tank", s, new Vector3(side * r.Range(9f, 12f), 0, r.Range(5f, 30f)), r.Range(-40f, 40f));
                    Place(PropFactory.MilitaryTruck(r.Range(0, 99), Palette.DesertMilitary), "Truck", s, new Vector3(side * r.Range(7f, 9f), 0, r.Range(5f, 30f)), r.Range(150f, 210f));
                    if (r.Chance(0.5f)) FX.Smoke(d, new Vector3(side * 10f, 2f, 18f), 1f, new Color(0.2f, 0.18f, 0.16f, 0.5f));
                    break;
                case 1: // destroyed buildings
                    Place(PropFactory.RuinWall(r.Range(0, 99), r.Range(6f, 12f), MaterialLibrary.Lit("adobe", new Color(0.7f, 0.56f, 0.42f), 0.1f, 0f, TexKind.Concrete)), "Ruin", s,
                        new Vector3(side * r.Range(9f, 14f), 0, r.Range(5f, 30f)), side * 90f + r.Range(-15f, 15f));
                    Place(PropFactory.RuinWall(r.Range(0, 99), r.Range(4f, 8f), Palette.Brick), "Ruin", s, new Vector3(side * r.Range(14f, 18f), 0, r.Range(5f, 30f)), r.Range(0, 360f));
                    break;
                case 2: // settlement
                    for (int i = 0; i < 3; i++)
                        Place(PropFactory.Shack(r.Range(0, 999)), "Shack", s, new Vector3(side * r.Range(9f, 16f), 0, 6f + i * 11f + r.Range(-2f, 2f)), side * 90f + r.Range(-10f, 10f));
                    Place(PropFactory.Barrels(r.Range(0, 99)), "Barrels", s, new Vector3(side * 7.5f, 0, r.Range(5f, 30f)), 0);
                    break;
                default: // military checkpoint camp
                    Place(PropFactory.MilitaryTent(r.Range(0, 99), Palette.Canvas), "MilTent", s, new Vector3(side * r.Range(11f, 14f), 0, r.Range(8f, 30f)), r.Range(0, 360f));
                    Place(PropFactory.Sandbags(r.Range(0, 99), 6), "Sandbags", s, new Vector3(side * 6.5f, 0, r.Range(5f, 30f)), 90f);
                    Place(PropFactory.Watchtower(r.Range(0, 99)), "Tower", s, new Vector3(side * r.Range(10f, 13f), 0, r.Range(5f, 30f)), r.Range(0, 360f));
                    break;
            }
        }

        // ------------------------------------------------------------------ DARK CITY
        void DarkCityChunk(ThemeProfile p, Transform s, Transform d, Rng r)
        {
            Sidewalks(s, Palette.Verge(p), Palette.DarkConcrete);
            LaneLines(s, Palette.LaneLine, true, true);
            Verges(p, s, r.Range(0, 99), RoadHalfWidth + 4.2f, 50f, Palette.VergeAlt(p), 0.0f, 0f);
            for (int i = 0; i < Count(5); i++)
                Place(new PropBuilder().Sphere(Vector3.zero, new Vector3(r.Range(1f, 3f), 0.02f, r.Range(2f, 5f)), Palette.Puddle, 0.4f, i), "Puddle", s,
                    new Vector3(r.Range(-3.8f, 3.8f), 0.005f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            // Building rows
            for (int side = -1; side <= 1; side += 2)
            {
                float z = r.Range(-2f, 2f);
                while (z < ChunkLength)
                {
                    float w = r.Range(9f, 15f), depth = r.Range(10f, 16f), h = r.Range(14f, 48f);
                    Place(PropFactory.Building(r.Range(0, 9999), w, h, depth, r.Chance(0.7f), r.Chance(0.25f) ? r.Range(0.1f, 0.35f) : 0f, false), "Building", s,
                        new Vector3(side * (RoadHalfWidth + 4.4f + depth * 0.5f), 0, z + w * 0.5f), side * 90f);
                    // Neon sign on the facade
                    if (r.Chance(0.45f))
                    {
                        var c = r.Pick(new[] { new Color(1f, 0.1f, 0.35f), new Color(0.1f, 0.9f, 1f), new Color(0.6f, 0.2f, 1f), new Color(1f, 0.55f, 0.1f) });
                        bool vertical = r.Chance(0.5f);
                        var sign = Place(PropFactory.NeonSign(r.Range(0, 999), c, vertical), "Neon", d,
                            new Vector3(side * (RoadHalfWidth + 4.2f), vertical ? r.Range(5f, 9f) : r.Range(3.8f, 5f), z + w * 0.5f + r.Range(-2f, 2f)), side * 90f);
                        sign.AddComponent<NoMerge>();
                        if (QualityManager.AllowExtraLights)
                        {
                            var lg = new GameObject("NeonLight");
                            lg.transform.SetParent(sign.transform, false);
                            lg.transform.localPosition = new Vector3(0, 0, -1.2f);
                            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = 8f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                            var f = lg.AddComponent<FlickerLight>(); f.baseIntensity = 2.2f; f.hardFlicker = r.Chance(0.5f);
                            f.toggles = new Renderer[] { sign.GetComponent<Renderer>() };
                        }
                    }
                    // Alley
                    float gap = r.Range(1f, 5f);
                    if (gap > 3f)
                    {
                        Place(PropFactory.Dumpster(r.Range(0, 99)), "Dumpster", s, new Vector3(side * (RoadHalfWidth + 7f), 0.18f, z + w + gap * 0.5f), side * 90f);
                        if (r.Chance(0.5f)) FX.Smoke(d, new Vector3(side * (RoadHalfWidth + 9f), 0.2f, z + w + gap * 0.5f), 0.6f, new Color(0.4f, 0.42f, 0.48f, 0.35f), 4f, 5f);
                    }
                    z += w + gap;
                }
            }
            // Street lights (some broken, some flickering)
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                float z = i * 20f + r.Range(2f, 8f);
                bool broken = r.Chance(0.35f);
                Place(PropFactory.StreetLight(r.Range(0, 99), broken), "StreetLight", s, new Vector3(side * (RoadHalfWidth + 0.7f), 0.18f, z), side > 0 ? 0f : 180f);
                if (!broken && QualityManager.AllowExtraLights)
                {
                    var lg = new GameObject("StreetLamp");
                    lg.transform.SetParent(d, false);
                    lg.transform.localPosition = new Vector3(side * (RoadHalfWidth + 0.7f - 1.5f), 5.5f, z);
                    var l = lg.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 110f; l.range = 16f; l.intensity = 9f;
                    l.color = new Color(1f, 0.78f, 0.5f); l.shadows = LightShadows.None;
                    lg.transform.rotation = Quaternion.Euler(90, 0, 0);
                    var f = lg.AddComponent<FlickerLight>(); f.baseIntensity = 9f; f.hardFlicker = r.Chance(0.3f); f.amount = 0.05f;
                }
            }
            // Parked / abandoned vehicles at the curb
            for (int i = 0; i < Count(2); i++)
            {
                float side = r.Sign;
                if (r.Chance(0.2f))
                    Place(PropFactory.Bus(r.Range(0, 99), MaterialLibrary.Lit("citybus", new Color(0.55f, 0.5f, 0.2f), 0.4f, 0.3f, TexKind.Rust), true, false), "Bus", s, new Vector3(side * 5.6f, 0.0f, r.Range(6f, 34f)), r.Range(-5f, 5f));
                else
                    Place(PropFactory.Car(r.Range(0, 99), Palette.CarPaint(r.Range(0, 9)), r.Chance(0.6f), false), "Car", s, new Vector3(side * 5.4f, 0.0f, r.Range(3f, 37f)), r.Range(-12f, 12f) + (r.Chance(0.5f) ? 180f : 0f));
            }
            for (int i = 0; i < Count(2); i++)
                Place(PropFactory.Hydrant(), "Hydrant", s, new Vector3(r.Sign * (RoadHalfWidth + 0.6f), 0.18f, r.Range(0, ChunkLength)), 0);
            for (int i = 0; i < Count(3); i++)
                Place(PropFactory.TrafficCone(), "Cone", s, new Vector3(r.Sign * r.Range(4.4f, 6f), 0.18f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            if (r.Chance(0.5f)) FX.Smoke(d, new Vector3(r.Range(-3f, 3f), 0.05f, r.Range(5f, 35f)), 0.5f, new Color(0.5f, 0.52f, 0.58f, 0.3f), 5f, 4f); // steam vent
        }

        // ------------------------------------------------------------------ ABANDONED CITY
        void AbandonedChunk(ThemeProfile p, Transform s, Transform d, Rng r)
        {
            Sidewalks(s, Palette.Verge(p), Palette.Concrete);
            LaneLines(s, MaterialLibrary.Lit("fadedline", new Color(0.62f, 0.6f, 0.52f), 0.2f, 0f, TexKind.Concrete), true, false);
            Verges(p, s, r.Range(0, 99), RoadHalfWidth + 4.2f, 50f, Palette.VergeAlt(p), 0.0f, 0.2f);
            // grass and weeds through cracks
            for (int i = 0; i < Count(10); i++)
                Place(PropFactory.Bush(r.Range(0, 999), r.Range(0.3f, 0.8f), Palette.Vine), "Weeds", s, new Vector3(r.Sign * r.Range(3.9f, 8f), 0.1f, r.Range(0, ChunkLength)), 0);
            for (int i = 0; i < Count(2); i++)
                Place(PropFactory.BroadTree(r.Range(0, 999), r.Range(6f, 10f)), "CrackTree", s, new Vector3(r.Sign * r.Range(5.5f, 7.5f), 0.1f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            // Damaged skyscrapers
            for (int side = -1; side <= 1; side += 2)
            {
                float z = r.Range(-3f, 3f);
                while (z < ChunkLength)
                {
                    float w = r.Range(10f, 16f), depth = r.Range(12f, 18f), h = r.Range(22f, 70f);
                    Place(PropFactory.Building(r.Range(0, 9999), w, h, depth, false, r.Range(0.25f, 0.85f), r.Chance(0.7f)), "Ruin", s,
                        new Vector3(side * (RoadHalfWidth + 4.6f + depth * 0.5f), 0, z + w * 0.5f), side * 90f);
                    z += w + r.Range(1f, 4f);
                }
            }
            for (int i = 0; i < Count(2); i++)
                Place(PropFactory.TrafficSignal(r.Range(0, 99)), "Signal", s, new Vector3(r.Sign * (RoadHalfWidth + 0.6f), 0.18f, r.Range(0, ChunkLength)), r.Chance(0.5f) ? 0f : 180f);
            for (int i = 0; i < Count(2); i++)
            {
                bool flip = r.Chance(0.4f);
                Place(PropFactory.Car(r.Range(0, 99), Palette.CarPaint(r.Range(0, 9)), true, r.Chance(0.3f)), "Car", s,
                    new Vector3(r.Sign * r.Range(5.2f, 7f), flip ? 1.45f : 0f, r.Range(0, ChunkLength)), r.Range(0, 360f)).transform.localRotation *= Quaternion.Euler(0, 0, flip ? 180f : 0f);
            }
            for (int i = 0; i < Count(3); i++)
                Place(new PropBuilder().Sphere(Vector3.zero, new Vector3(r.Range(2f, 5f), r.Range(0.8f, 2f), r.Range(2f, 5f)), Palette.Rubble, 0.45f, i, default, true), "Rubble", s,
                    new Vector3(r.Sign * r.Range(5f, 9f), 0.1f, r.Range(0, ChunkLength)), r.Range(0, 360f));
            float sd = r.Sign;
            switch (r.Range(0, 3))
            {
                case 0: // destroyed military checkpoint
                    Place(PropFactory.Sandbags(r.Range(0, 99), 5), "Sandbags", s, new Vector3(sd * 6.2f, 0.18f, r.Range(5f, 30f)), 90f);
                    Place(PropFactory.Watchtower(r.Range(0, 99)), "Tower", s, new Vector3(sd * 7.5f, 0.18f, r.Range(5f, 30f)), r.Range(-20f, 20f));
                    Place(PropFactory.MilitaryTruck(r.Range(0, 99), Palette.Military), "Truck", s, new Vector3(sd * 6.6f, 0.0f, r.Range(5f, 30f)), r.Range(160f, 200f));
                    break;
                case 1: // burned bus
                    Place(PropFactory.Bus(r.Range(0, 99), Palette.Burnt, true, true), "BurnedBus", s, new Vector3(sd * 6.2f, 0, r.Range(8f, 30f)), r.Range(-15f, 15f));
                    break;
                default:
                    Place(PropFactory.Billboard(r.Range(0, 99), MaterialLibrary.Lit("billboard" + r.Range(0, 3), r.Pick(new[] { new Color(0.6f, 0.5f, 0.35f), new Color(0.35f, 0.45f, 0.55f), new Color(0.55f, 0.35f, 0.3f) }), 0.2f, 0f, TexKind.Rust)), "Billboard", s,
                        new Vector3(sd * 7.5f, 0.18f, r.Range(5f, 30f)), sd * 90f);
                    break;
            }
            if (r.Chance(0.35f)) FX.Fire(d, new Vector3(r.Sign * r.Range(6f, 8f), 0.2f, r.Range(5f, 35f)), 0.5f);
            for (int i = 0; i < Count(1); i++)
                FX.LightShaft(d, new Vector3(SideX(r, 2f, 7f), 14f, r.Range(0, ChunkLength)), p.sunEuler, 30f, r.Range(2f, 3f), new Color(1f, 0.95f, 0.8f, 0.05f));
        }

        void ImportedDecor(ThemeProfile p, Transform s, Rng r)
        {
            var reg = ZRAssetRegistry.Instance;
            var set = reg != null ? reg.Theme(p.id) : null;
            if (set == null || set.decor == null || set.decor.Count == 0) return;
            for (int i = 0; i < Count(6); i++)
            {
                var prefab = set.decor[r.Range(0, set.decor.Count)];
                if (prefab == null) continue;
                var go = Instantiate(prefab, s);
                go.transform.localPosition = new Vector3(SideX(r, 6.5f, 20f), 0, r.Range(0, ChunkLength));
                go.transform.localRotation = Quaternion.Euler(0, r.Range(0, 360f), 0);
                foreach (var c in go.GetComponentsInChildren<Collider>()) ZRUtil.SafeDestroy(c);
            }
        }

        // =====================================================================================
        // BACKDROP (far scenery that follows the player)
        // =====================================================================================
        Transform BuildBackdrop(ThemeProfile p)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(transform, false);
            var r = new Rng(p.musicSeed + 5);
            var holder = ZRUtil.Child(root, "Parts");
            switch (p.id)
            {
                case ThemeId.Forest:
                    for (int i = 0; i < 14; i++)
                        Place(PropFactory.Mountain(r.Range(0, 99), r.Range(40f, 80f), r.Range(18f, 40f), MaterialLibrary.Lit("farhill", new Color(0.2f, 0.26f, 0.2f), 0.05f, 0f, TexKind.Foliage, 8f)), "Hill", holder,
                            new Vector3(SideX(r, 70f, 160f), -2f, r.Range(-80f, 300f)), r.Range(0, 360f));
                    for (int i = 0; i < 40; i++)
                        Place(PropFactory.PineTree(r.Range(0, 99), r.Range(20f, 30f)), "FarPine", holder, new Vector3(SideX(r, 36f, 70f), 0, r.Range(-60f, 280f)), 0);
                    break;
                case ThemeId.Desert:
                    var mesaMat = MaterialLibrary.Lit("mesa", new Color(0.6f, 0.4f, 0.28f), 0.05f, 0f, TexKind.Rock, 4f);
                    for (int i = 0; i < 12; i++)
                        Place(PropFactory.Mountain(r.Range(0, 99), r.Range(50f, 110f), r.Range(25f, 70f), mesaMat), "Mountain", holder, new Vector3(SideX(r, 90f, 220f), -3f, r.Range(-50f, 400f)), r.Range(0, 360f));
                    for (int i = 0; i < 10; i++)
                        Place(PropFactory.Dune(r.Range(0, 99), new Vector3(r.Range(40f, 80f), r.Range(8f, 18f), r.Range(40f, 90f))), "FarDune", holder, new Vector3(SideX(r, 50f, 100f), -2f, r.Range(-40f, 300f)), r.Range(0, 360f));
                    break;
                case ThemeId.DarkCity:
                    for (int i = 0; i < 46; i++)
                    {
                        float h = r.Range(40f, 140f);
                        Place(PropFactory.Building(r.Range(0, 999), r.Range(14f, 30f), h, r.Range(14f, 30f), true, 0f, false), "Skyline", holder,
                            new Vector3(SideX(r, 45f, 180f), 0, r.Range(-40f, 420f)), r.Range(0, 4) * 90f);
                    }
                    // a few tall antenna lights
                    for (int i = 0; i < 6; i++)
                        Place(new PropBuilder().Box(Vector3.zero, Vector3.one * 1.2f, Palette.Neon(new Color(1f, 0.1f, 0.1f))), "Beacon", holder,
                            new Vector3(SideX(r, 60f, 160f), r.Range(110f, 150f), r.Range(0f, 400f)), 0);
                    break;
                case ThemeId.AbandonedCity:
                    for (int i = 0; i < 40; i++)
                    {
                        float h = r.Range(45f, 160f);
                        Place(PropFactory.Building(r.Range(0, 999), r.Range(16f, 32f), h, r.Range(16f, 32f), false, r.Range(0.2f, 0.8f), r.Chance(0.5f)), "Skyline", holder,
                            new Vector3(SideX(r, 45f, 180f), 0, r.Range(-40f, 420f)), r.Range(0, 4) * 90f);
                    }
                    for (int i = 0; i < 4; i++)
                        FX.SmokeColumn(root, new Vector3(SideX(r, 60f, 140f), 10f, r.Range(80f, 350f)), r.Range(2f, 3.5f));
                    break;
            }
            MeshMerge.MergeChildren(holder, "BackdropMerged", true, false);
            return root;
        }

        // =====================================================================================
        // WEATHER (follows the player)
        // =====================================================================================
        Transform BuildWeather(ThemeProfile p)
        {
            var root = new GameObject("Weather").transform;
            root.SetParent(transform, false);
            switch (p.weather)
            {
                case WeatherStyle.ForestFog:
                    FX.GroundFog(root, new Color(0.75f, 0.8f, 0.76f), 1.0f, 0.16f);
                    FX.Motes(root, new Color(0.45f, 0.38f, 0.2f, 0.9f), 0.12f, 1.2f, 80, false); // falling leaves
                    FX.Motes(root, new Color(1f, 0.95f, 0.75f, 0.5f), 0.05f, 0.05f, 60, true);  // dust in sunlight
                    break;
                case WeatherStyle.Sandstorm:
                    FX.Sandstorm(root);
                    FX.GroundFog(root, new Color(0.85f, 0.68f, 0.48f), 0.6f, 0.2f);
                    break;
                case WeatherStyle.NightRain:
                    FX.Rain(root);
                    FX.GroundFog(root, new Color(0.35f, 0.4f, 0.5f), 0.8f, 0.14f);
                    break;
                case WeatherStyle.DustAndSmoke:
                    FX.Motes(root, new Color(0.35f, 0.33f, 0.3f, 0.8f), 0.08f, 0.6f, 120, false); // ash
                    FX.Motes(root, new Color(1f, 0.95f, 0.8f, 0.4f), 0.05f, 0.05f, 60, true);
                    FX.GroundFog(root, new Color(0.7f, 0.68f, 0.6f), 1.2f, 0.12f);
                    break;
            }
            return root;
        }
    }
}
