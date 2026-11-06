using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// Procedural models for every obstacle and scenery prop. All props face +Z, sit on y = 0 and are
    /// centred on x = 0. Obstacles fit inside one 2.6 m lane.
    /// These are built from primitives, then baked to one mesh each. They are original geometry, so there
    /// are no licensing questions, but they are stylised compared to scanned assets: import real models
    /// through the Setup Wizard for photoreal results (see ASSET_SOURCES.md).
    /// </summary>
    public static class PropFactory
    {
        // ------------------------------------------------------------------ OBSTACLES
        public static GameObject Obstacle(PropKind kind, int seed)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            switch (kind)
            {
                // ---------- forest
                case PropKind.FallenTree:
                    b.Cyl(new Vector3(0, 0.42f, 0), 0.42f, 2.7f, Palette.Bark, new Vector3(0, r.Range(-8f, 8f), 90), 14);
                    b.Sphere(new Vector3(-1.45f, 0.55f, 0), new Vector3(0.8f, 1.2f, 1.3f), Palette.Bark, 0.35f, seed);
                    b.Cyl(new Vector3(0.4f, 0.75f, 0.25f), 0.08f, 0.8f, Palette.Bark, new Vector3(30, 0, -30));
                    b.Cyl(new Vector3(-0.5f, 0.8f, -0.2f), 0.07f, 0.7f, Palette.Bark, new Vector3(-35, 0, 20));
                    b.Sphere(new Vector3(0.2f, 0.82f, 0), new Vector3(1.3f, 0.12f, 0.6f), Palette.Moss, 0.3f, seed + 1);
                    b.Sphere(new Vector3(1.3f, 0.4f, 0), new Vector3(0.1f, 0.84f, 0.84f), Palette.Wood, 0.1f, seed); // cut face
                    break;
                case PropKind.LargeRock:
                    b.Sphere(new Vector3(0, 1.0f, 0), new Vector3(2.3f, 2.3f, 2.0f), Palette.Rock, 0.35f, seed, new Vector3(0, r.Range(0, 360f), 0), true, 12, 16);
                    b.Sphere(new Vector3(0.9f, 0.3f, -0.8f), new Vector3(0.7f, 0.5f, 0.6f), Palette.Rock, 0.4f, seed + 3);
                    b.Sphere(new Vector3(0, 1.95f, 0.1f), new Vector3(1.3f, 0.25f, 1.1f), Palette.Moss, 0.4f, seed + 4);
                    break;
                case PropKind.WoodenBarricade:
                    for (int i = -1; i <= 1; i += 2)
                    {
                        b.Box(new Vector3(i * 1.0f, 0.5f, 0), new Vector3(0.12f, 1.25f, 0.12f), Palette.OldWood, new Vector3(25, 0, 0));
                        b.Box(new Vector3(i * 1.0f, 0.5f, 0), new Vector3(0.12f, 1.25f, 0.12f), Palette.OldWood, new Vector3(-25, 0, 0));
                    }
                    b.Box(new Vector3(0, 0.85f, 0), new Vector3(2.4f, 0.2f, 0.06f), Palette.OldWood, new Vector3(0, 0, r.Range(-4f, 4f)));
                    b.Box(new Vector3(0, 0.5f, 0.02f), new Vector3(2.3f, 0.18f, 0.06f), Palette.OldWood, new Vector3(0, 0, r.Range(-6f, 6f)));
                    b.Box(new Vector3(0, 0.6f, -0.04f), new Vector3(2.5f, 0.12f, 0.05f), Palette.OldWood, new Vector3(0, 0, 18));
                    b.Cyl(new Vector3(0.7f, 0.95f, 0), 0.015f, 0.6f, Palette.Rust, new Vector3(0, 0, 80), 4); // barbed wire
                    break;
                case PropKind.ForestWreck:
                    b.Append(Car(seed, Palette.Rust, true, false), Vector3.zero, Quaternion.Euler(0, r.Range(-14f, 14f), r.Range(-4f, 4f)), Vector3.one);
                    b.Sphere(new Vector3(0, 1.32f, -0.2f), new Vector3(1.2f, 0.12f, 1.6f), Palette.Moss, 0.4f, seed);
                    break;
                case PropKind.MudPit:
                    b.Sphere(new Vector3(0, 0.0f, 0), new Vector3(2.4f, 0.1f, 2.8f), Palette.MudWet, 0.25f, seed);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        b.Sphere(new Vector3(Mathf.Cos(a) * 1.15f, 0.04f, Mathf.Sin(a) * 1.35f), new Vector3(0.6f, 0.18f, 0.5f), MaterialLibrary.Lit("mudlump", new Color(0.3f, 0.23f, 0.16f), 0.35f, 0, TexKind.Mud), 0.5f, seed + i);
                    }
                    break;
                case PropKind.LowBranch:
                    b.Cyl(new Vector3(-1.4f, 0.8f, 0), 0.3f, 1.6f, Palette.Bark, default, 10, 0.24f);
                    b.Cyl(new Vector3(1.45f, 0.75f, 0), 0.32f, 1.5f, Palette.Bark, default, 10, 0.26f);
                    b.Cyl(new Vector3(0, 1.75f, 0), 0.26f, 3.4f, Palette.Bark, new Vector3(0, 0, 90 + r.Range(-3f, 3f)), 12);
                    b.Cyl(new Vector3(0.6f, 2.05f, 0.1f), 0.06f, 0.8f, Palette.Bark, new Vector3(0, 0, -50));
                    b.Sphere(new Vector3(-0.3f, 2.1f, 0), new Vector3(1.4f, 0.6f, 0.9f), Palette.Leaves, 0.4f, seed);
                    b.Sphere(new Vector3(0.9f, 1.95f, 0.1f), new Vector3(0.9f, 0.5f, 0.7f), Palette.Leaves, 0.4f, seed + 2);
                    b.Sphere(new Vector3(0, 1.98f, 0), new Vector3(2.6f, 0.08f, 0.4f), Palette.Moss, 0.3f, seed + 5);
                    break;

                // ---------- desert
                case PropKind.DestroyedCar:
                    b.Append(Car(seed, MaterialLibrary.Lit("fadedpaint" + (seed % 3), r.Vary(new Color(0.55f, 0.45f, 0.35f), 0.15f), 0.25f, 0.2f, TexKind.Rust), true, r.Chance(0.4f)),
                        Vector3.zero, Quaternion.Euler(0, r.Range(-12f, 12f), 0), Vector3.one);
                    b.Sphere(new Vector3(0.9f, 0.1f, 0.3f), new Vector3(1.0f, 0.6f, 2.2f), Palette.Sand, 0.3f, seed);
                    break;
                case PropKind.SandPit:
                    b.Sphere(new Vector3(0, -0.02f, 0), new Vector3(2.4f, 0.06f, 2.8f), MaterialLibrary.Lit("sandpit", new Color(0.55f, 0.42f, 0.28f), 0.05f, 0, TexKind.Sand, 3f), 0.3f, seed);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 9f * Mathf.PI * 2f;
                        b.Sphere(new Vector3(Mathf.Cos(a) * 1.2f, 0.02f, Mathf.Sin(a) * 1.4f), new Vector3(0.7f, 0.22f, 0.6f), Palette.Sand, 0.4f, seed + i);
                    }
                    break;
                case PropKind.RockFormation:
                    b.Sphere(new Vector3(0, 0.9f, 0), new Vector3(2.3f, 1.9f, 2.1f), Palette.DesertRock, 0.35f, seed, default, true, 12, 16);
                    b.Sphere(new Vector3(0.2f, 2.1f, 0.1f), new Vector3(1.5f, 1.2f, 1.4f), Palette.DesertRock, 0.4f, seed + 1);
                    b.Sphere(new Vector3(-0.3f, 2.8f, -0.1f), new Vector3(0.9f, 0.7f, 0.9f), Palette.DesertRock, 0.4f, seed + 2);
                    b.Sphere(new Vector3(0.9f, 0.1f, 0.6f), new Vector3(1.2f, 0.3f, 1f), Palette.Sand, 0.3f, seed + 3);
                    break;
                case PropKind.MilitaryBarricade:
                    for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 4 - (row == 2 ? 1 : 0); i++)
                    {
                        float x = -0.9f + i * 0.6f + (row % 2) * 0.3f;
                        b.Capsule(new Vector3(x, 0.16f + row * 0.27f, 0), 0.16f, 0.62f, Palette.Sandbag, new Vector3(0, r.Range(-8f, 8f), 90));
                    }
                    b.Box(new Vector3(1.15f, 0.55f, -0.25f), new Vector3(0.08f, 1.1f, 0.08f), Palette.Rust);
                    b.Cyl(new Vector3(0, 0.95f, -0.25f), 0.012f, 2.4f, Palette.Rust, new Vector3(0, 0, 90), 4);
                    break;
                case PropKind.AbandonedTruck:
                    b.Append(MilitaryTruck(seed, Palette.DesertMilitary), Vector3.zero, Quaternion.Euler(0, r.Range(-2f, 2f), 0), Vector3.one);
                    break;
                case PropKind.CollapsedArch:
                    b.Box(new Vector3(-1.35f, 1.3f, 0), new Vector3(0.45f, 2.6f, 0.6f), Palette.Concrete, new Vector3(0, 0, 3));
                    b.Box(new Vector3(1.35f, 1.1f, 0), new Vector3(0.45f, 2.2f, 0.6f), Palette.Concrete, new Vector3(0, 0, -4));
                    b.Box(new Vector3(0, 1.95f, 0), new Vector3(3.3f, 0.5f, 0.9f), Palette.Concrete, new Vector3(0, 0, -5));
                    b.Cyl(new Vector3(-0.8f, 2.25f, 0.1f), 0.02f, 0.9f, Palette.Rust, new Vector3(0, 0, 60), 4);
                    b.Cyl(new Vector3(0.6f, 1.7f, -0.2f), 0.02f, 0.7f, Palette.Rust, new Vector3(30, 0, 120), 4);
                    b.Sphere(new Vector3(-1.35f, 0.1f, 0.3f), new Vector3(1.0f, 0.4f, 0.8f), Palette.Sand, 0.3f, seed);
                    break;

                // ---------- dark city
                case PropKind.AbandonedCar:
                    b.Append(Car(seed, Palette.CarPaint(seed), true, false), Vector3.zero, Quaternion.Euler(0, r.Range(-12f, 12f), 0), Vector3.one);
                    break;
                case PropKind.PoliceRoadblock:
                    for (int i = -1; i <= 1; i += 2)
                    {
                        b.Box(new Vector3(i * 1.0f, 0.45f, 0.15f), new Vector3(0.08f, 0.95f, 0.08f), Palette.DarkSteel, new Vector3(-18, 0, 0));
                        b.Box(new Vector3(i * 1.0f, 0.45f, -0.15f), new Vector3(0.08f, 0.95f, 0.08f), Palette.DarkSteel, new Vector3(18, 0, 0));
                    }
                    for (int s = 0; s < 6; s++)
                        b.Box(new Vector3(-1.05f + s * 0.42f, 0.82f, 0), new Vector3(0.42f, 0.22f, 0.05f), s % 2 == 0 ? Palette.HazardWhite : Palette.PoliceBlue);
                    b.Box(new Vector3(-1.0f, 1.0f, 0), new Vector3(0.12f, 0.1f, 0.1f), Palette.Neon(new Color(1f, 0.1f, 0.1f)));
                    b.Box(new Vector3(1.0f, 1.0f, 0), new Vector3(0.12f, 0.1f, 0.1f), Palette.Neon(new Color(0.15f, 0.3f, 1f)));
                    break;
                case PropKind.FallenStreetlight:
                    b.Cyl(new Vector3(-1.5f, 0.78f, 0), 0.14f, 1.56f, Palette.DarkSteel, default, 10);
                    b.Box(new Vector3(1.45f, 0.7f, 0), new Vector3(0.7f, 1.4f, 0.8f), Palette.Wood); // crate stack
                    b.Box(new Vector3(1.45f, 0.35f, 0.1f), new Vector3(0.72f, 0.05f, 0.82f), Palette.DarkSteel);
                    b.Cyl(new Vector3(0, 1.62f, 0), 0.1f, 3.6f, Palette.DarkSteel, new Vector3(0, 0, 88), 10);
                    b.Box(new Vector3(1.85f, 1.72f, 0), new Vector3(0.6f, 0.15f, 0.3f), Palette.DarkSteel);
                    b.Box(new Vector3(1.85f, 1.64f, 0), new Vector3(0.5f, 0.04f, 0.22f), Palette.Lamp);
                    break;
                case PropKind.Scaffolding:
                    for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        b.Cyl(new Vector3(i * 1.35f, 1.4f, j * 0.6f), 0.04f, 2.8f, Palette.Steel, default, 6);
                    b.Box(new Vector3(0, 1.62f, 0), new Vector3(3.0f, 0.08f, 1.4f), Palette.OldWood);
                    b.Cyl(new Vector3(0, 1.55f, 0.6f), 0.035f, 2.8f, Palette.Steel, new Vector3(0, 0, 90), 6);
                    b.Cyl(new Vector3(0, 1.55f, -0.6f), 0.035f, 2.8f, Palette.Steel, new Vector3(0, 0, 90), 6);
                    b.Cyl(new Vector3(0, 2.6f, 0.6f), 0.035f, 2.8f, Palette.Steel, new Vector3(0, 0, 90), 6);
                    b.Box(new Vector3(0.2f, 2.0f, 0.66f), new Vector3(2.4f, 0.75f, 0.02f), MaterialLibrary.Lit("tarp", new Color(0.18f, 0.28f, 0.4f), 0.2f, 0, TexKind.Fabric, 2f), new Vector3(0, 0, 3));
                    break;
                case PropKind.ConstructionBarrier:
                    for (int i = -1; i <= 1; i += 2)
                    {
                        b.Box(new Vector3(i * 0.6f, 0.4f, 0), new Vector3(1.15f, 0.8f, 0.5f), i < 0 ? Palette.HazardOrange : Palette.HazardWhite);
                        b.Box(new Vector3(i * 0.6f, 0.86f, 0), new Vector3(1.1f, 0.14f, 0.24f), i < 0 ? Palette.HazardWhite : Palette.HazardOrange);
                    }
                    b.Cyl(new Vector3(0.9f, 1.0f, 0), 0.07f, 0.06f, Palette.Neon(new Color(1f, 0.6f, 0.05f)), new Vector3(90, 0, 0), 8);
                    break;
                case PropKind.BurningCar:
                    b.Append(Car(seed, Palette.Burnt, true, true), Vector3.zero, Quaternion.Euler(0, r.Range(-10f, 10f), 0), Vector3.one);
                    break;

                // ---------- abandoned city
                case PropKind.BrokenBus:
                    b.Append(Bus(seed, MaterialLibrary.Lit("busrust", new Color(0.55f, 0.45f, 0.25f), 0.25f, 0.3f, TexKind.Rust), true, true), Vector3.zero, Quaternion.Euler(0, 0, r.Range(-3f, 3f)), new Vector3(0.88f, 1f, 0.9f));
                    break;
                case PropKind.RubblePile:
                    for (int i = 0; i < 9; i++)
                    {
                        var p = new Vector3(r.Range(-0.9f, 0.9f), r.Range(0.1f, 0.4f), r.Range(-0.5f, 0.5f));
                        b.Box(p, new Vector3(r.Range(0.4f, 0.9f), r.Range(0.25f, 0.5f), r.Range(0.4f, 0.8f)), r.Chance(0.3f) ? Palette.Brick : Palette.Concrete, new Vector3(r.Range(-30f, 30f), r.Range(0, 360f), r.Range(-30f, 30f)));
                    }
                    b.Sphere(new Vector3(0, 0.2f, 0), new Vector3(2.4f, 0.8f, 1.4f), Palette.Rubble, 0.4f, seed, default, true);
                    b.Cyl(new Vector3(0.4f, 0.75f, 0), 0.02f, 0.9f, Palette.Rust, new Vector3(20, 0, -35), 4);
                    b.Cyl(new Vector3(-0.5f, 0.7f, 0.1f), 0.02f, 0.8f, Palette.Rust, new Vector3(-15, 0, 40), 4);
                    break;
                case PropKind.ConcreteBarrier:
                    b.Box(new Vector3(0, 0.12f, 0), new Vector3(2.4f, 0.24f, 0.62f), Palette.Concrete);
                    b.Box(new Vector3(0, 0.36f, 0), new Vector3(2.4f, 0.26f, 0.42f), Palette.Concrete);
                    b.Box(new Vector3(0, 0.66f, 0), new Vector3(2.4f, 0.36f, 0.24f), Palette.Concrete);
                    b.Box(new Vector3(-0.4f, 0.5f, -0.16f), new Vector3(0.9f, 0.3f, 0.02f), MaterialLibrary.Lit("graffiti", new Color(0.65f, 0.15f, 0.12f), 0.3f, 0f, TexKind.Concrete), new Vector3(0, 0, 4));
                    break;
                case PropKind.OverturnedCar:
                    b.Append(Car(seed, Palette.CarPaint(seed + 2), true, false), new Vector3(0, 1.45f, 0), Quaternion.Euler(0, r.Range(-10f, 10f), 180), Vector3.one);
                    break;
                case PropKind.RoadHole:
                    b.Cyl(new Vector3(0, -0.02f, 0), 1.2f, 0.06f, Palette.Hole, default, 16);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        b.Box(new Vector3(Mathf.Cos(a) * 1.25f, 0.05f, Mathf.Sin(a) * 1.25f), new Vector3(0.6f, 0.12f, 0.35f), Palette.Asphalt, new Vector3(r.Range(-20f, 20f), -a * Mathf.Rad2Deg + 90f, r.Range(-15f, 15f)));
                    }
                    break;
                case PropKind.FallenGirder:
                    b.Sphere(new Vector3(-1.45f, 0.4f, 0), new Vector3(1.0f, 1.6f, 1.0f), Palette.Rubble, 0.4f, seed, default, true);
                    b.Sphere(new Vector3(1.45f, 0.4f, 0), new Vector3(1.0f, 1.5f, 1.0f), Palette.Rubble, 0.4f, seed + 1, default, true);
                    var rot = new Vector3(0, 0, r.Range(-3f, 3f));
                    b.Box(new Vector3(0, 1.62f, 0), new Vector3(3.6f, 0.06f, 0.4f), Palette.Rust, rot);
                    b.Box(new Vector3(0, 1.88f, 0), new Vector3(3.6f, 0.06f, 0.4f), Palette.Rust, rot);
                    b.Box(new Vector3(0, 1.75f, 0), new Vector3(3.6f, 0.26f, 0.05f), Palette.Rust, rot);
                    break;
            }
            return b.Build(kind.ToString());
        }

        // ------------------------------------------------------------------ VEHICLES
        public static PropBuilder Car(int seed, Material paint, bool damaged, bool burnt)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            if (burnt) paint = Palette.Burnt;
            b.Box(new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.55f, 4.2f), paint);
            b.Box(new Vector3(0, 1.08f, -0.25f), new Vector3(1.56f, 0.44f, 2.1f), paint);
            var glass = burnt ? Palette.Hole : Palette.Glass;
            b.Box(new Vector3(0, 1.1f, -0.25f), new Vector3(1.6f, 0.32f, 1.8f), glass);
            b.Box(new Vector3(0, 1.06f, 0.86f), new Vector3(1.5f, 0.38f, 0.05f), glass, new Vector3(-50, 0, 0));
            b.Box(new Vector3(0, 0.58f, 2.12f), new Vector3(1.82f, 0.22f, 0.12f), Palette.DarkSteel);
            b.Box(new Vector3(0, 0.58f, -2.12f), new Vector3(1.82f, 0.22f, 0.12f), Palette.DarkSteel);
            b.Box(new Vector3(0.62f, 0.72f, 2.11f), new Vector3(0.32f, 0.12f, 0.04f), burnt ? Palette.Burnt : Palette.Steel);
            b.Box(new Vector3(-0.62f, 0.72f, 2.11f), new Vector3(0.32f, 0.12f, 0.04f), burnt ? Palette.Burnt : Palette.Steel);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? 0.82f : -0.82f, z = i < 2 ? 1.35f : -1.35f;
                if (burnt && r.Chance(0.5f)) b.Cyl(new Vector3(x, 0.32f, z), 0.24f, 0.2f, Palette.Rust, new Vector3(0, 0, 90), 10);
                else b.Wheel(new Vector3(x, 0.36f, z), 0.36f, 0.24f, damaged && r.Chance(0.3f));
            }
            if (damaged)
            {
                b.Box(new Vector3(0, 0.92f, 1.45f), new Vector3(1.6f, 0.06f, 1.1f), paint, new Vector3(-r.Range(5f, 25f), r.Range(-8f, 8f), 0)); // popped hood
                b.Box(new Vector3(r.Sign * 0.92f, 0.65f, r.Range(-1f, 1f)), new Vector3(0.05f, 0.35f, 0.9f), Palette.Rust);
                if (r.Chance(0.5f)) b.Box(new Vector3(1.25f, 0.75f, -0.1f), new Vector3(0.06f, 0.6f, 1.0f), paint, new Vector3(0, -40, 0)); // open door
            }
            return b;
        }

        public static PropBuilder Bus(int seed, Material paint, bool damaged, bool overgrown)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            b.Box(new Vector3(0, 1.0f, 0), new Vector3(2.5f, 1.2f, 10f), paint);
            b.Box(new Vector3(0, 2.1f, 0), new Vector3(2.5f, 1.0f, 10f), damaged ? Palette.Rust : paint);
            b.Box(new Vector3(0, 2.15f, 0), new Vector3(2.54f, 0.65f, 9.4f), damaged ? Palette.Hole : Palette.Glass);
            b.Box(new Vector3(0, 2.65f, 0), new Vector3(2.4f, 0.12f, 9.8f), Palette.Steel);
            b.Box(new Vector3(0, 1.9f, 5.01f), new Vector3(2.3f, 1.1f, 0.04f), damaged ? Palette.Hole : Palette.Glass);
            b.Box(new Vector3(0, 0.45f, 5.05f), new Vector3(2.5f, 0.3f, 0.15f), Palette.DarkSteel);
            float[] zs = { 3.4f, -2.6f, -3.6f };
            foreach (var z in zs)
            {
                b.Wheel(new Vector3(1.15f, 0.5f, z), 0.5f, 0.3f, damaged && r.Chance(0.4f));
                b.Wheel(new Vector3(-1.15f, 0.5f, z), 0.5f, 0.3f, damaged && r.Chance(0.4f));
            }
            if (overgrown)
            {
                for (int i = 0; i < 4; i++)
                    b.Sphere(new Vector3(r.Range(-0.9f, 0.9f), 2.75f, r.Range(-4f, 4f)), new Vector3(r.Range(0.8f, 1.6f), 0.4f, r.Range(1f, 2.2f)), Palette.Vine, 0.4f, seed + i);
                b.Box(new Vector3(1.27f, 1.5f, 1f), new Vector3(0.04f, 1.6f, 2.5f), Palette.Vine);
            }
            return b;
        }

        public static PropBuilder MilitaryTruck(int seed, Material paint)
        {
            var b = new PropBuilder();
            b.Box(new Vector3(0, 1.2f, 2.5f), new Vector3(2.2f, 1.5f, 1.8f), paint);
            b.Box(new Vector3(0, 1.55f, 3.42f), new Vector3(1.9f, 0.6f, 0.04f), Palette.Glass);
            b.Box(new Vector3(0, 0.75f, 3.45f), new Vector3(2.0f, 0.5f, 0.12f), Palette.DarkSteel);
            b.Box(new Vector3(0, 0.95f, -1.0f), new Vector3(2.3f, 0.25f, 5.0f), paint);
            b.Box(new Vector3(1.12f, 1.4f, -1.0f), new Vector3(0.06f, 0.7f, 5.0f), paint);
            b.Box(new Vector3(-1.12f, 1.4f, -1.0f), new Vector3(0.06f, 0.7f, 5.0f), paint);
            b.Add(MeshGen.Cylinder(1.15f, 1.15f, 5f, 16, false), new Vector3(0, 1.75f, -1.0f), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 0.75f), Palette.Canvas);
            b.Wheel(new Vector3(1.05f, 0.55f, 2.4f), 0.55f, 0.35f);
            b.Wheel(new Vector3(-1.05f, 0.55f, 2.4f), 0.55f, 0.35f);
            b.Wheel(new Vector3(1.05f, 0.55f, -1.6f), 0.55f, 0.35f, true);
            b.Wheel(new Vector3(-1.05f, 0.55f, -1.6f), 0.55f, 0.35f);
            b.Wheel(new Vector3(1.05f, 0.55f, -2.8f), 0.55f, 0.35f);
            b.Wheel(new Vector3(-1.05f, 0.55f, -2.8f), 0.55f, 0.35f);
            return b;
        }

        public static PropBuilder Tank(int seed, Material paint)
        {
            var b = new PropBuilder();
            b.Box(new Vector3(0, 0.75f, 0), new Vector3(3.2f, 0.9f, 6.5f), paint);
            b.Box(new Vector3(1.45f, 0.5f, 0), new Vector3(0.6f, 0.9f, 6.8f), Palette.DarkSteel);
            b.Box(new Vector3(-1.45f, 0.5f, 0), new Vector3(0.6f, 0.9f, 6.8f), Palette.DarkSteel);
            b.Cyl(new Vector3(0, 1.5f, -0.3f), 1.25f, 0.65f, paint, default, 14, 1.0f);
            b.Cyl(new Vector3(0, 1.55f, 2.4f), 0.11f, 3.8f, paint, new Vector3(88, 0, 0), 8);
            b.Box(new Vector3(0, 1.9f, -0.8f), new Vector3(0.5f, 0.2f, 0.5f), Palette.DarkSteel);
            return b;
        }

        // ------------------------------------------------------------------ NATURE
        public static PropBuilder PineTree(int seed, float height)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            float trunkR = height * 0.025f;
            b.Cyl(new Vector3(0, height * 0.4f, 0), trunkR, height * 0.8f, Palette.Bark, default, 8, trunkR * 0.5f);
            int layers = 5;
            for (int i = 0; i < layers; i++)
            {
                float t = i / (float)layers;
                float y = height * (0.22f + t * 0.66f);
                float rad = height * 0.2f * (1f - t * 0.75f) * r.Range(0.85f, 1.1f);
                b.Cone(new Vector3(r.Range(-0.1f, 0.1f), y, r.Range(-0.1f, 0.1f)), rad, height * 0.3f, Palette.Pine, 9, new Vector3(r.Range(-4f, 4f), r.Range(0, 360f), r.Range(-4f, 4f)));
            }
            return b;
        }

        public static PropBuilder BroadTree(int seed, float height, bool dead = false)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            float tr = height * 0.035f;
            var bark = dead ? Palette.DeadBark : Palette.Bark;
            b.Cyl(new Vector3(0, height * 0.3f, 0), tr, height * 0.6f, bark, new Vector3(r.Range(-4f, 4f), 0, r.Range(-4f, 4f)), 8, tr * 0.7f);
            int branches = r.Range(3, 6);
            for (int i = 0; i < branches; i++)
            {
                float a = r.Range(0, 360f);
                var dir = Quaternion.Euler(0, a, 0) * new Vector3(0, 1, 0.8f);
                var start = new Vector3(0, height * r.Range(0.45f, 0.6f), 0);
                float len = height * r.Range(0.25f, 0.4f);
                b.Cyl(start + dir.normalized * len * 0.5f, tr * 0.45f, len, bark, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, 6, tr * 0.2f);
                if (!dead)
                    b.Sphere(start + dir.normalized * len, Vector3.one * height * r.Range(0.28f, 0.4f), Palette.Leaves, 0.45f, seed + i, new Vector3(0, r.Range(0, 360f), 0));
            }
            if (!dead) b.Sphere(new Vector3(0, height * 0.82f, 0), Vector3.one * height * 0.45f, Palette.Leaves, 0.45f, seed + 9);
            return b;
        }

        public static PropBuilder Bush(int seed, float size, Material mat = null)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            mat = mat ?? Palette.Leaves;
            int n = r.Range(2, 5);
            for (int i = 0; i < n; i++)
                b.Sphere(new Vector3(r.Range(-0.4f, 0.4f) * size, size * r.Range(0.25f, 0.45f), r.Range(-0.4f, 0.4f) * size), Vector3.one * size * r.Range(0.6f, 0.9f), mat, 0.5f, seed + i);
            return b;
        }

        public static PropBuilder Rock(int seed, float size, Material mat = null)
        {
            var b = new PropBuilder();
            b.Sphere(new Vector3(0, size * 0.3f, 0), new Vector3(size, size * 0.75f, size * 0.9f), mat ?? Palette.Rock, 0.4f, seed, new Vector3(0, seed * 37 % 360, 0), true);
            return b;
        }

        public static PropBuilder Cactus(int seed, float h)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            var m = MaterialLibrary.Lit("cactus", new Color(0.33f, 0.42f, 0.27f), 0.2f, 0f, TexKind.Bark, 2f);
            b.Capsule(new Vector3(0, h * 0.5f, 0), h * 0.08f, h, m);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                float y = h * r.Range(0.35f, 0.6f);
                b.Capsule(new Vector3(s * h * 0.15f, y, 0), h * 0.055f, h * 0.22f, m, new Vector3(0, 0, 90));
                b.Capsule(new Vector3(s * h * 0.25f, y + h * 0.12f, 0), h * 0.055f, h * 0.3f, m);
            }
            return b;
        }

        public static PropBuilder Dune(int seed, Vector3 size)
        {
            var b = new PropBuilder();
            b.Sphere(Vector3.zero, size, Palette.Sand, 0.18f, seed, new Vector3(0, seed * 23 % 360, 0), true, 10, 16);
            return b;
        }

        public static PropBuilder Mountain(int seed, float radius, float height, Material mat)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            b.Sphere(new Vector3(0, 0, 0), new Vector3(radius * 2f, height * 2f, radius * 1.6f), mat, 0.35f, seed, new Vector3(0, r.Range(0, 360f), 0), true, 10, 16);
            b.Sphere(new Vector3(radius * 0.5f, 0, radius * 0.3f), new Vector3(radius, height * 1.3f, radius), mat, 0.4f, seed + 1, default, true);
            return b;
        }

        // ------------------------------------------------------------------ CAMP / SETTLEMENT
        public static PropBuilder Tent(int seed)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            var cloth = MaterialLibrary.Lit("tent" + (seed % 3), r.Pick(new[] { new Color(0.3f, 0.36f, 0.25f), new Color(0.45f, 0.3f, 0.2f), new Color(0.25f, 0.3f, 0.38f) }), 0.1f, 0f, TexKind.Fabric, 2f);
            b.Box(new Vector3(-0.55f, 0.6f, 0), new Vector3(0.05f, 1.6f, 2.2f), cloth, new Vector3(0, 0, -40));
            b.Box(new Vector3(0.55f, 0.6f, 0), new Vector3(0.05f, 1.6f, 2.2f), cloth, new Vector3(0, 0, 40 + r.Range(-10f, 5f)));
            b.Cyl(new Vector3(0, 1.2f, 0), 0.03f, 2.4f, Palette.Wood, new Vector3(90, 0, 0), 6);
            return b;
        }

        public static PropBuilder Campfire(int seed)
        {
            var b = new PropBuilder();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                b.Sphere(new Vector3(Mathf.Cos(a) * 0.5f, 0.08f, Mathf.Sin(a) * 0.5f), new Vector3(0.25f, 0.18f, 0.22f), Palette.Rock, 0.4f, seed + i);
            }
            b.Cyl(new Vector3(0, 0.1f, 0), 0.05f, 0.8f, Palette.Burnt, new Vector3(80, 30, 0), 6);
            b.Cyl(new Vector3(0, 0.1f, 0), 0.05f, 0.8f, Palette.Burnt, new Vector3(80, -40, 0), 6);
            b.Sphere(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.08f, 0.5f), Palette.Fire, 0.4f, seed);
            return b;
        }

        public static PropBuilder Shack(int seed)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            float w = r.Range(3f, 5f), d = r.Range(3f, 4.5f), h = r.Range(2.4f, 3f);
            var wall = r.Chance(0.5f) ? Palette.Rust : MaterialLibrary.Lit("adobe", new Color(0.7f, 0.56f, 0.42f), 0.1f, 0f, TexKind.Concrete, 1f);
            b.Box(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), wall);
            b.Box(new Vector3(0, h + 0.1f, 0), new Vector3(w + 0.5f, 0.08f, d + 0.5f), Palette.Rust, new Vector3(r.Range(-6f, 6f), 0, r.Range(-4f, 4f)));
            b.Box(new Vector3(w * 0.2f, 1.0f, d * 0.5f + 0.01f), new Vector3(0.9f, 2.0f, 0.04f), Palette.Hole);
            b.Box(new Vector3(-w * 0.25f, 1.5f, d * 0.5f + 0.01f), new Vector3(0.8f, 0.6f, 0.04f), Palette.Hole);
            return b;
        }

        public static PropBuilder RuinWall(int seed, float len, Material mat)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            int seg = Mathf.Max(2, Mathf.RoundToInt(len / 1.2f));
            for (int i = 0; i < seg; i++)
            {
                float h = r.Range(0.6f, 3.2f);
                b.Box(new Vector3(-len * 0.5f + (i + 0.5f) * len / seg, h * 0.5f, 0), new Vector3(len / seg, h, 0.4f), mat);
            }
            b.Sphere(new Vector3(0, 0.1f, 0.6f), new Vector3(len * 0.6f, 0.6f, 1.2f), Palette.Rubble, 0.4f, seed, default, true);
            return b;
        }

        public static PropBuilder Barrels(int seed)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            int n = r.Range(1, 4);
            for (int i = 0; i < n; i++)
            {
                bool fallen = r.Chance(0.3f);
                var m = r.Chance(0.5f) ? Palette.Rust : MaterialLibrary.Lit("barrelpaint", new Color(0.25f, 0.35f, 0.22f), 0.4f, 0.4f, TexKind.Rust);
                b.Cyl(new Vector3(i * 0.7f, fallen ? 0.3f : 0.45f, r.Range(-0.3f, 0.3f)), 0.3f, 0.9f, m, fallen ? new Vector3(0, r.Range(0, 180f), 90) : default, 12);
            }
            return b;
        }

        public static PropBuilder PowerPole(int seed)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            b.Cyl(new Vector3(0, 4f, 0), 0.14f, 8f, Palette.OldWood, new Vector3(r.Range(-6f, 6f), 0, r.Range(-6f, 6f)), 8, 0.11f);
            b.Box(new Vector3(0, 7.4f, 0), new Vector3(2.2f, 0.12f, 0.12f), Palette.OldWood);
            return b;
        }

        public static PropBuilder Sandbags(int seed, int len)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            for (int row = 0; row < 3; row++)
            for (int i = 0; i < len; i++)
                b.Capsule(new Vector3(i * 0.6f + (row % 2) * 0.3f, 0.16f + row * 0.27f, 0), 0.16f, 0.62f, Palette.Sandbag, new Vector3(0, r.Range(-8f, 8f), 90));
            return b;
        }

        public static PropBuilder Watchtower(int seed)
        {
            var b = new PropBuilder();
            for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                b.Box(new Vector3(i * 1.1f, 2.5f, j * 1.1f), new Vector3(0.15f, 5f, 0.15f), Palette.OldWood);
            b.Box(new Vector3(0, 4.2f, 0), new Vector3(2.6f, 0.12f, 2.6f), Palette.OldWood);
            b.Box(new Vector3(0, 4.7f, 1.25f), new Vector3(2.6f, 0.9f, 0.08f), Palette.OldWood);
            b.Box(new Vector3(0, 5.9f, 0), new Vector3(2.9f, 0.1f, 2.9f), Palette.Rust, new Vector3(5, 0, 0));
            b.Box(new Vector3(-1.1f, 5.4f, -1.1f), new Vector3(0.1f, 1f, 0.1f), Palette.OldWood);
            b.Box(new Vector3(1.1f, 5.4f, -1.1f), new Vector3(0.1f, 1f, 0.1f), Palette.OldWood);
            return b;
        }

        public static PropBuilder MilitaryTent(int seed, Material mat)
        {
            var b = new PropBuilder();
            b.Add(MeshGen.Cylinder(2f, 2f, 5f, 14, false), new Vector3(0, 0.2f, 0), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 0.9f), mat);
            b.Box(new Vector3(0, 0.9f, 2.5f), new Vector3(3.6f, 1.8f, 0.05f), mat);
            b.Box(new Vector3(0, 0.75f, 2.53f), new Vector3(1.0f, 1.5f, 0.04f), Palette.Hole);
            return b;
        }

        // ------------------------------------------------------------------ CITY
        /// <summary>A building facade block. damage 0..1 knocks chunks out of the top and exposes floor slabs.</summary>
        public static PropBuilder Building(int seed, float w, float h, float d, bool lit, float damage, bool vines)
        {
            var r = new Rng(seed);
            var b = new PropBuilder();
            var facade = Palette.Facade(seed, lit);
            float floor = 3.2f;
            if (damage <= 0.01f)
            {
                b.Box(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), facade, default, 1f / (floor * 4f));
            }
            else
            {
                // Intact lower part, then a ragged top made of columns of different heights.
                float intact = h * (1f - damage * 0.6f);
                b.Box(new Vector3(0, intact * 0.5f, 0), new Vector3(w, intact, d), facade, default, 1f / (floor * 4f));
                int cols = Mathf.Max(2, Mathf.RoundToInt(w / 4f));
                for (int c = 0; c < cols; c++)
                {
                    float cw = w / cols;
                    float extra = (h - intact) * r.Range(0f, 1f);
                    if (extra < 1f) continue;
                    b.Box(new Vector3(-w * 0.5f + cw * (c + 0.5f), intact + extra * 0.5f, r.Range(-0.5f, 0.5f)), new Vector3(cw, extra, d * r.Range(0.6f, 1f)), facade, default, 1f / (floor * 4f));
                }
                // exposed slabs
                int slabs = r.Range(2, 5);
                for (int s = 0; s < slabs; s++)
                {
                    float y = intact + s * floor;
                    b.Box(new Vector3(r.Range(-w * 0.2f, w * 0.2f), y, d * 0.1f), new Vector3(w * r.Range(0.4f, 0.9f), 0.25f, d * 0.9f), Palette.Concrete, new Vector3(r.Range(-4f, 4f), 0, r.Range(-6f, 6f)));
                }
                b.Sphere(new Vector3(r.Range(-w * 0.3f, w * 0.3f), 0.2f, d * 0.55f), new Vector3(w * 0.5f, 1.4f, 2.2f), Palette.Rubble, 0.4f, seed, default, true);
            }
            // rooftop details
            if (damage < 0.3f)
            {
                if (r.Chance(0.5f)) b.Cyl(new Vector3(r.Range(-w * 0.3f, w * 0.3f), h + 1.4f, r.Range(-d * 0.3f, d * 0.3f)), 1.1f, 2.0f, Palette.OldWood, default, 10);
                for (int i = 0; i < r.Range(1, 4); i++)
                    b.Box(new Vector3(r.Range(-w * 0.35f, w * 0.35f), h + 0.5f, r.Range(-d * 0.35f, d * 0.35f)), new Vector3(1.2f, 1f, 1.2f), Palette.Steel);
                b.Box(new Vector3(0, h + 0.3f, 0), new Vector3(w + 0.2f, 0.6f, d + 0.2f), Palette.DarkConcrete);
            }
            // ground floor: shop front
            b.Box(new Vector3(0, 1.6f, -d * 0.5f - 0.01f), new Vector3(w * 0.8f, 2.6f, 0.05f), lit && r.Chance(0.4f) ? MaterialLibrary.Emissive("shopglow", new Color(0.9f, 0.6f, 0.3f), 0.6f) : Palette.Hole);
            b.Box(new Vector3(0, 3.2f, -d * 0.5f - 0.6f), new Vector3(w * 0.85f, 0.12f, 1.2f), r.Chance(0.5f) ? Palette.Rust : MaterialLibrary.Lit("awning" + (seed % 4), r.Vary(new Color(0.4f, 0.15f, 0.12f), 0.3f), 0.2f, 0f, TexKind.Fabric), new Vector3(-12, 0, 0));
            if (vines)
            {
                int n = r.Range(2, 5);
                for (int i = 0; i < n; i++)
                {
                    float vh = r.Range(3f, h * 0.6f);
                    b.Box(new Vector3(r.Range(-w * 0.45f, w * 0.45f), vh * 0.5f, -d * 0.5f - 0.05f), new Vector3(r.Range(1f, 3f), vh, 0.12f), Palette.Vine);
                }
                b.Sphere(new Vector3(0, 0.3f, -d * 0.5f - 0.3f), new Vector3(w * 0.7f, 1f, 1.2f), Palette.Leaves, 0.5f, seed, default, true);
            }
            return b;
        }

        public static PropBuilder StreetLight(int seed, bool broken)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            var tilt = broken ? new Vector3(r.Range(-12f, 12f), 0, r.Range(5f, 18f)) : Vector3.zero;
            var rot = Quaternion.Euler(tilt);
            b.Add(MeshGen.Cylinder(0.12f, 0.08f, 6f, 8), rot * new Vector3(0, 3f, 0), rot, Vector3.one, Palette.DarkSteel);
            b.Add(MeshGen.Box(new Vector3(1.6f, 0.08f, 0.08f)), rot * new Vector3(-0.8f, 5.9f, 0), rot, Vector3.one, Palette.DarkSteel);
            b.Add(MeshGen.Box(new Vector3(0.6f, 0.12f, 0.3f)), rot * new Vector3(-1.5f, 5.82f, 0), rot, Vector3.one, Palette.DarkSteel);
            b.Add(MeshGen.Box(new Vector3(0.5f, 0.03f, 0.22f)), rot * new Vector3(-1.5f, 5.75f, 0), rot, Vector3.one, broken ? Palette.Hole : Palette.Lamp);
            return b;
        }

        public static PropBuilder TrafficSignal(int seed)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            var rot = Quaternion.Euler(r.Range(-8f, 8f), 0, r.Range(4f, 14f));
            b.Add(MeshGen.Cylinder(0.1f, 0.1f, 5.5f, 8), rot * new Vector3(0, 2.75f, 0), rot, Vector3.one, Palette.DarkSteel);
            b.Add(MeshGen.Box(new Vector3(3.2f, 0.1f, 0.1f)), rot * new Vector3(-1.6f, 5.4f, 0), rot, Vector3.one, Palette.DarkSteel);
            b.Add(MeshGen.Box(new Vector3(0.35f, 1.0f, 0.3f)), rot * Quaternion.Euler(0, 0, r.Range(-30f, 30f)) * new Vector3(-2.8f, 4.8f, 0), rot * Quaternion.Euler(0, 0, r.Range(-30f, 30f)), Vector3.one, MaterialLibrary.Lit("signalbox", new Color(0.25f, 0.24f, 0.12f), 0.3f, 0.3f, TexKind.Rust));
            return b;
        }

        public static PropBuilder Billboard(int seed, Material face)
        {
            var b = new PropBuilder();
            b.Cyl(new Vector3(-2f, 3f, 0), 0.15f, 6f, Palette.Rust, default, 8);
            b.Cyl(new Vector3(2f, 3f, 0), 0.15f, 6f, Palette.Rust, default, 8);
            b.Box(new Vector3(0, 6.5f, 0), new Vector3(6.5f, 3f, 0.15f), Palette.Rust, new Vector3(0, 0, (seed % 7) - 3));
            b.Box(new Vector3(0, 6.5f, -0.09f), new Vector3(6.2f, 2.7f, 0.02f), face, new Vector3(0, 0, (seed % 7) - 3));
            return b;
        }

        /// <summary>Abstract neon sign: emissive tube frame with bars (no text, no real brands).</summary>
        public static PropBuilder NeonSign(int seed, Color c, bool vertical)
        {
            var b = new PropBuilder();
            var r = new Rng(seed);
            var neon = Palette.Neon(c);
            float w = vertical ? 0.9f : r.Range(2.2f, 3.6f), h = vertical ? r.Range(2.5f, 4f) : 0.9f;
            b.Box(Vector3.zero, new Vector3(w, h, 0.15f), Palette.DarkSteel);
            b.Box(new Vector3(0, h * 0.5f - 0.06f, -0.09f), new Vector3(w - 0.1f, 0.05f, 0.05f), neon);
            b.Box(new Vector3(0, -h * 0.5f + 0.06f, -0.09f), new Vector3(w - 0.1f, 0.05f, 0.05f), neon);
            b.Box(new Vector3(w * 0.5f - 0.06f, 0, -0.09f), new Vector3(0.05f, h - 0.1f, 0.05f), neon);
            b.Box(new Vector3(-w * 0.5f + 0.06f, 0, -0.09f), new Vector3(0.05f, h - 0.1f, 0.05f), neon);
            int bars = r.Range(3, 6);
            for (int i = 0; i < bars; i++)
            {
                if (vertical) b.Box(new Vector3(0, -h * 0.35f + i * h * 0.7f / bars, -0.09f), new Vector3(w * r.Range(0.3f, 0.6f), 0.06f, 0.05f), neon, new Vector3(0, 0, r.Range(-20f, 20f)));
                else b.Box(new Vector3(-w * 0.35f + i * w * 0.7f / bars, 0, -0.09f), new Vector3(0.06f, h * r.Range(0.3f, 0.6f), 0.05f), neon, new Vector3(0, 0, r.Range(-25f, 25f)));
            }
            return b;
        }

        public static PropBuilder Dumpster(int seed)
        {
            var b = new PropBuilder();
            var m = MaterialLibrary.Lit("dumpster", new Color(0.15f, 0.28f, 0.2f), 0.35f, 0.4f, TexKind.Rust);
            b.Box(new Vector3(0, 0.65f, 0), new Vector3(1.8f, 1.1f, 1.1f), m);
            b.Box(new Vector3(0, 1.25f, -0.1f), new Vector3(1.85f, 0.06f, 1.2f), m, new Vector3(-15, 0, 0));
            b.Sphere(new Vector3(1.2f, 0.25f, 0.2f), new Vector3(0.6f, 0.5f, 0.5f), MaterialLibrary.Lit("trashbag", new Color(0.03f, 0.03f, 0.03f), 0.75f, 0f), 0.3f, seed);
            b.Sphere(new Vector3(1.5f, 0.2f, -0.3f), new Vector3(0.5f, 0.4f, 0.5f), MaterialLibrary.Lit("trashbag", new Color(0.03f, 0.03f, 0.03f), 0.75f, 0f), 0.3f, seed + 1);
            return b;
        }

        public static PropBuilder Hydrant()
        {
            var b = new PropBuilder();
            var m = MaterialLibrary.Lit("hydrant", new Color(0.55f, 0.12f, 0.08f), 0.4f, 0.3f, TexKind.Rust);
            b.Cyl(new Vector3(0, 0.35f, 0), 0.13f, 0.7f, m, default, 10);
            b.Sphere(new Vector3(0, 0.72f, 0), new Vector3(0.26f, 0.2f, 0.26f), m);
            b.Cyl(new Vector3(0, 0.45f, 0), 0.05f, 0.42f, m, new Vector3(0, 0, 90), 8);
            return b;
        }

        public static PropBuilder TrafficCone()
        {
            var b = new PropBuilder();
            b.Box(new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.04f, 0.4f), Palette.HazardOrange);
            b.Cone(new Vector3(0, 0.04f, 0), 0.16f, 0.7f, Palette.HazardOrange, 10);
            b.Cyl(new Vector3(0, 0.38f, 0), 0.105f, 0.1f, Palette.HazardWhite, default, 10, 0.09f);
            return b;
        }
    }
}
