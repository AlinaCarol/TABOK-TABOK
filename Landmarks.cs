using Godot;

namespace CrossyRoadBase;

/// <summary>
/// Cebu City landmarks. The local origin is the front-center of each building: the front faces -X (toward the
/// street) and the building extends toward +X, about 2.5 units either side in Z (five lanes wide).
/// </summary>
public static class Landmarks
{
    static Label3D Sign(string text, int fontSize, Color color, Color outline, Vector3 pos)
    {
        return new Label3D
        {
            Text = text,
            FontSize = fontSize,
            PixelSize = 0.01f,
            OutlineSize = 14,
            Modulate = color,
            OutlineModulate = outline,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = pos
        };
    }

    static MeshInstance3D Rot(MeshInstance3D m, float x, float y, float z)
    {
        m.RotationDegrees = new Vector3(x, y, z);
        return m;
    }

    static Node3D Tree(Vector3 pos)
    {
        var t = new Node3D { Position = pos };
        t.AddChild(Util.Box(new Vector3(0.15f, 0.5f, 0.15f), new Color("#6d4c41"), new Vector3(0, 0.25f, 0)));
        t.AddChild(Util.Box(new Vector3(0.75f, 0.75f, 0.75f), new Color("#2e7d32"), new Vector3(0, 0.85f, 0)));
        return t;
    }

    // ------------------------------------------------------------------
    //  Basilica Minore del Santo Niño: ochre colonial facade, bell tower, dome
    // ------------------------------------------------------------------
    public static Node3D Basilica(string sign = "BASILICA DEL SANTO NIÑO", string wallHex = "#d9a35b", string roofHex = "#8d4b2f")
    {
        var b = new Node3D();
        Color ochre = new(wallHex), trim = new("#f5ecd7"), roof = new(roofHex), dark = new("#4a3322");

        b.AddChild(Util.Box(new Vector3(1.2f, 3.4f, 3.2f), ochre, new Vector3(0.6f, 1.7f, 0)));     // facade
        b.AddChild(Util.Box(new Vector3(1.3f, 0.2f, 3.3f), trim, new Vector3(0.6f, 3.5f, 0)));      // cornice
        b.AddChild(Util.Box(new Vector3(5f, 2.6f, 2.8f), ochre, new Vector3(3.7f, 1.3f, 0)));       // nave
        b.AddChild(Util.Box(new Vector3(5.1f, 0.25f, 3.0f), roof, new Vector3(3.7f, 2.72f, 0)));    // nave roof

        // bell tower
        b.AddChild(Util.Box(new Vector3(1.2f, 4.8f, 1.2f), ochre, new Vector3(0.6f, 2.4f, 1.85f)));
        b.AddChild(Util.Box(new Vector3(1.3f, 0.15f, 1.3f), trim, new Vector3(0.6f, 3.3f, 1.85f)));
        b.AddChild(Util.Box(new Vector3(1.26f, 0.6f, 0.5f), dark, new Vector3(0.6f, 4.2f, 1.85f)));  // belfry opening
        b.AddChild(Rot(Util.Cyl(0f, 0.9f, 1.1f, roof, new Vector3(0.6f, 5.35f, 1.85f), 4), 0, 45, 0));

        // dome with a cross
        b.AddChild(Util.Cyl(0.3f, 1.15f, 1.0f, new Color("#efe3c8"), new Vector3(3.2f, 3.3f, 0), 16));
        b.AddChild(Util.Box(new Vector3(0.1f, 0.6f, 0.1f), trim, new Vector3(3.2f, 4.1f, 0)));
        b.AddChild(Util.Box(new Vector3(0.1f, 0.1f, 0.4f), trim, new Vector3(3.2f, 4.2f, 0)));

        // door and windows
        b.AddChild(Util.Box(new Vector3(0.05f, 1.5f, 0.9f), dark, new Vector3(-0.02f, 0.75f, -0.2f)));
        b.AddChild(Util.Box(new Vector3(0.05f, 0.9f, 0.4f), dark, new Vector3(-0.02f, 2.3f, -0.9f)));
        b.AddChild(Util.Box(new Vector3(0.05f, 0.9f, 0.4f), dark, new Vector3(-0.02f, 2.3f, 0.5f)));

        b.AddChild(Sign(sign, 56, Colors.White, Colors.Black, new Vector3(1.5f, 6.7f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Cebu Metropolitan Cathedral: cream facade with twin towers
    // ------------------------------------------------------------------
    public static Node3D Cathedral(string sign = "CEBU METROPOLITAN CATHEDRAL", string wallHex = "#efe6d0", string roofHex = "#c0602f")
    {
        var b = new Node3D();
        Color cream = new(wallHex), trim = new("#c9b896"), roof = new(roofHex), dark = new("#3e2f23");

        b.AddChild(Util.Box(new Vector3(1.2f, 3.0f, 3.4f), cream, new Vector3(0.6f, 1.5f, 0)));     // facade
        b.AddChild(Util.Box(new Vector3(5.2f, 2.4f, 3.0f), cream, new Vector3(3.8f, 1.2f, 0)));     // nave
        b.AddChild(Util.Box(new Vector3(5.3f, 0.3f, 3.2f), roof, new Vector3(3.8f, 2.55f, 0)));     // roof

        foreach (int sz in new[] { -1, 1 })
        {
            b.AddChild(Util.Box(new Vector3(1.3f, 5.0f, 1.1f), cream, new Vector3(0.65f, 2.5f, sz * 1.15f)));   // tower
            b.AddChild(Util.Box(new Vector3(1.35f, 0.15f, 1.15f), trim, new Vector3(0.65f, 3.6f, sz * 1.15f)));
            b.AddChild(Util.Box(new Vector3(1.36f, 0.6f, 0.4f), dark, new Vector3(0.65f, 4.4f, sz * 1.15f)));   // belfry
            b.AddChild(Rot(Util.Cyl(0f, 0.85f, 1.3f, roof, new Vector3(0.65f, 5.65f, sz * 1.15f), 4), 0, 45, 0));
            b.AddChild(Util.Box(new Vector3(0.08f, 0.5f, 0.08f), trim, new Vector3(0.65f, 6.55f, sz * 1.15f)));
        }

        var rose = Util.Cyl(0.4f, 0.4f, 0.05f, new Color("#4fc3f7"), new Vector3(-0.02f, 2.3f, 0), 16); // rose window
        b.AddChild(Rot(rose, 0, 0, 90));
        b.AddChild(Util.Box(new Vector3(0.05f, 1.4f, 1.0f), dark, new Vector3(-0.02f, 0.7f, 0)));        // door

        b.AddChild(Sign(sign, 52, Colors.White, Colors.Black, new Vector3(1.5f, 7.3f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  SM City Cebu: big white mall with a blue band and glass entrance
    // ------------------------------------------------------------------
    public static Node3D Mall(string name, Color blue, Color wall)
    {
        var b = new Node3D();

        b.AddChild(Util.Box(new Vector3(6.5f, 2.8f, 4.6f), wall, new Vector3(3.6f, 1.4f, 0)));
        b.AddChild(Util.Box(new Vector3(6.55f, 0.5f, 4.65f), blue, new Vector3(3.6f, 2.45f, 0)));
        b.AddChild(Util.Box(new Vector3(0.1f, 1.5f, 3.0f), new Color("#81d4fa"), new Vector3(0.33f, 0.75f, 0)));   // glass entrance
        b.AddChild(Util.Box(new Vector3(0.9f, 0.1f, 3.4f), new Color("#b0bec5"), new Vector3(0.0f, 1.8f, 0)));      // canopy
        b.AddChild(Util.Box(new Vector3(0.3f, 1.0f, 3.2f), blue, new Vector3(0.2f, 3.4f, 0)));                      // sign board

        for (int i = 0; i < 3; i++)                                                                                  // rooftop AC units
            b.AddChild(Util.Box(new Vector3(0.8f, 0.4f, 0.8f), new Color("#cfd8dc"), new Vector3(2.5f + i * 1.5f, 3.2f, i % 2 == 0 ? -1.0f : 1.0f)));

        b.AddChild(Sign(name, 80, Colors.White, blue, new Vector3(0.2f, 3.5f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Ayala Center Cebu: mall with a rooftop garden and glass towers
    // ------------------------------------------------------------------
    public static Node3D AyalaCenter()
    {
        var b = new Node3D();
        Color blue = new("#0057b8");

        b.AddChild(Util.Box(new Vector3(5.2f, 1.9f, 4.6f), new Color("#fafafa"), new Vector3(2.8f, 0.95f, 0)));           // mall
        b.AddChild(Util.Box(new Vector3(5.0f, 0.2f, 4.4f), new Color("#66bb6a"), new Vector3(2.8f, 2.0f, 0)));             // roof garden
        b.AddChild(Util.Box(new Vector3(0.1f, 1.3f, 3.2f), new Color("#80deea"), new Vector3(0.23f, 0.65f, 0)));           // glass entrance
        b.AddChild(Util.Box(new Vector3(0.3f, 0.55f, 3.4f), blue, new Vector3(0.15f, 1.575f, 0)));                            // blue sign band

        // glass towers with window stripes
        var tower1 = new Node3D { Position = new Vector3(2.0f, 0, -1.3f) };
        tower1.AddChild(Util.Box(new Vector3(1.8f, 5.6f, 1.8f), new Color("#26c6da"), new Vector3(0, 2.8f, 0)));
        for (int i = 0; i < 9; i++)
            tower1.AddChild(Util.Box(new Vector3(1.86f, 0.1f, 1.86f), new Color("#263238"), new Vector3(0, 0.9f + i * 0.5f, 0)));
        b.AddChild(tower1);

        var tower2 = new Node3D { Position = new Vector3(4.0f, 0, 1.3f) };
        tower2.AddChild(Util.Box(new Vector3(1.6f, 4.4f, 1.6f), new Color("#4db6ac"), new Vector3(0, 2.2f, 0)));
        for (int i = 0; i < 7; i++)
            tower2.AddChild(Util.Box(new Vector3(1.66f, 0.1f, 1.66f), new Color("#263238"), new Vector3(0, 0.9f + i * 0.5f, 0)));
        b.AddChild(tower2);

        b.AddChild(Tree(new Vector3(1.0f, 2.1f, 1.4f)));
        b.AddChild(Tree(new Vector3(3.0f, 2.1f, -0.2f)));

        b.AddChild(Sign("AYALA CENTER CEBU", 64, Colors.White, blue, new Vector3(1.5f, 6.4f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  University of San Carlos campus: green and gold, clock tower, gate
    // ------------------------------------------------------------------
    public static Node3D UscCampus()
    {
        var b = new Node3D();
        Color cream = new("#f3e9c6"), green = new("#1b5e20"), gold = new("#fbc02d");

        b.AddChild(Util.Box(new Vector3(4.4f, 2.2f, 3.2f), cream, new Vector3(3.2f, 1.1f, 0.2f)));            // main hall
        b.AddChild(Util.Box(new Vector3(4.6f, 0.25f, 3.4f), green, new Vector3(3.2f, 2.3f, 0.2f)));           // green roof
        b.AddChild(Util.Box(new Vector3(2.6f, 1.6f, 1.2f), cream, new Vector3(2.4f, 0.8f, -1.9f)));           // side wing
        b.AddChild(Util.Box(new Vector3(2.8f, 0.2f, 1.3f), green, new Vector3(2.4f, 1.7f, -1.9f)));

        // clock tower
        b.AddChild(Util.Box(new Vector3(0.9f, 3.8f, 0.9f), cream, new Vector3(1.2f, 1.9f, 1.7f)));
        b.AddChild(Util.Box(new Vector3(0.05f, 0.5f, 0.5f), Colors.White, new Vector3(0.72f, 3.2f, 1.7f)));   // clock face
        b.AddChild(Rot(Util.Cyl(0f, 0.65f, 0.8f, gold, new Vector3(1.2f, 4.2f, 1.7f), 4), 0, 45, 0));

        // windows
        for (int i = 0; i < 4; i++)
            b.AddChild(Util.Box(new Vector3(0.05f, 0.7f, 0.5f), new Color("#455a64"), new Vector3(1.0f, 1.2f, -1.0f + i * 0.7f)));

        // gate arch
        foreach (int sz in new[] { -1, 1 })
            b.AddChild(Util.Box(new Vector3(0.35f, 2.0f, 0.35f), green, new Vector3(-0.2f, 1.0f, sz * 1.2f)));
        b.AddChild(Util.Box(new Vector3(0.4f, 0.5f, 2.9f), gold, new Vector3(-0.2f, 2.1f, 0)));

        b.AddChild(Tree(new Vector3(1.2f, 0, -0.2f)));
        b.AddChild(Tree(new Vector3(5.6f, 0, -1.6f)));

        b.AddChild(Sign("UNIVERSITY OF SAN CARLOS", 52, gold, green, new Vector3(1.0f, 3.1f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Registry: the city picks landmarks by name
    // ------------------------------------------------------------------
    static int stabilizeCounter;

    /// <summary>Builds a landmark and makes it flicker-proof (see Stabilize).</summary>
    public static Node3D Build(string key)
    {
        var landmark = BuildRaw(key);
        Stabilize(landmark);
        return landmark;
    }

    /// <summary>
    /// Safety net against z-fighting: each box grows by a tiny, different amount, so two faces that sit on exactly
    /// the same plane resolve the same way every frame instead of flickering between colors.
    /// </summary>
    static void Stabilize(Node node)
    {
        if (node is MeshInstance3D mi && mi.Mesh is BoxMesh box)
        {
            float eps = 0.002f + (stabilizeCounter++ % 7) * 0.001f;
            box.Size += new Vector3(eps, eps, eps);
        }
        foreach (Node child in node.GetChildren())
            Stabilize(child);
    }

    static Node3D BuildRaw(string key) => key switch
    {
        "basilica" => Basilica(),
        "cathedral" => Cathedral(),
        "sm" => Mall("SM CITY CEBU", new Color("#0d47a1"), new Color("#eceff1")),
        "ayala" => AyalaCenter(),
        "usc" => UscCampus(),
        "parkmall" => Mall("PARKMALL", new Color("#e65100"), new Color("#fff8e1")),
        "furniture" => Furniture(),
        "shrine" => LapuLapuShrine(),
        "airport" => Airport(),
        "carcar_church" => Cathedral("ST. CATHERINE OF ALEXANDRIA CHURCH", "#f2e9d0", "#a8442a"),
        "rotunda" => Rotunda(),
        "whale_shark" => WhaleShark(),
        "oslob_church" => Basilica("OSLOB PARISH CHURCH", "#cfd8dc", "#546e7a"),
        "dive_resort" => DiveResort("MOALBOAL DIVE RESORT"),
        "badian_resort" => DiveResort("BADIAN BEACH RESORT"),
        "sardine_run" => SardineRun(),
        "kawasan_falls" => KawasanFalls(),
        _ => Basilica()
    };

    static Node3D Palm(Vector3 pos)
    {
        var p = new Node3D { Position = pos };
        p.AddChild(Util.Box(new Vector3(0.22f, 1.8f, 0.22f), new Color("#8d6e63"), new Vector3(0, 0.9f, 0)));
        for (int i = 0; i < 4; i++)
        {
            var frond = Util.Box(new Vector3(1.4f, 0.05f, 0.3f), new Color("#2e8b57"), new Vector3(0, 1.85f + i * 0.004f, 0));
            frond.RotationDegrees = new Vector3(0, i * 45, 0);
            p.AddChild(frond);
        }
        return p;
    }

    // ------------------------------------------------------------------
    //  Mandaue: furniture showroom with a giant chair on the roof
    // ------------------------------------------------------------------
    public static Node3D Furniture()
    {
        var b = new Node3D();
        Color wood = new("#d7b98e"), dark = new("#6d4c41"), seat = new("#8d5a2b");

        b.AddChild(Util.Box(new Vector3(5.5f, 2.2f, 4.2f), wood, new Vector3(3.2f, 1.1f, 0)));
        b.AddChild(Util.Box(new Vector3(5.7f, 0.2f, 4.4f), dark, new Vector3(3.2f, 2.3f, 0)));
        b.AddChild(Util.Box(new Vector3(0.1f, 1.4f, 3.4f), new Color("#b3e5fc"), new Vector3(0.43f, 0.8f, 0)));   // showroom window

        // the giant chair (its back faces the street)
        b.AddChild(Util.Box(new Vector3(1.2f, 0.25f, 1.2f), seat, new Vector3(1.6f, 3.2f, 0)));
        b.AddChild(Util.Box(new Vector3(0.2f, 1.3f, 1.2f), seat, new Vector3(1.1f, 3.9f, 0)));
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                b.AddChild(Util.Box(new Vector3(0.18f, 0.7f, 0.18f), seat, new Vector3(1.6f + sx * 0.5f, 2.75f, sz * 0.5f)));

        b.AddChild(Sign("MANDAUE: FURNITURE CAPITAL", 52, new Color("#ffe0b2"), dark, new Vector3(1.5f, 5.3f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Lapu-Lapu City: the warrior statue on his pedestal
    // ------------------------------------------------------------------
    public static Node3D LapuLapuShrine()
    {
        var b = new Node3D();
        Color bronze = new("#8c6b3f"), stone = new("#9e9e9e");

        b.AddChild(Util.Box(new Vector3(5.0f, 0.04f, 4.6f), new Color("#d7ccc8"), new Vector3(2.5f, 0.02f, 0)));     // plaza
        b.AddChild(Util.Box(new Vector3(2.8f, 0.3f, 2.8f), new Color("#bdbdbd"), new Vector3(2.0f, 0.15f, 0)));       // steps
        b.AddChild(Util.Box(new Vector3(2.0f, 1.2f, 2.0f), stone, new Vector3(2.0f, 0.9f, 0)));                       // pedestal

        b.AddChild(Util.Box(new Vector3(0.5f, 0.9f, 0.7f), bronze, new Vector3(2.0f, 1.95f, 0)));                     // legs
        b.AddChild(Util.Box(new Vector3(0.7f, 0.9f, 0.5f), bronze, new Vector3(2.0f, 2.85f, 0)));                     // torso
        b.AddChild(Util.Box(new Vector3(0.4f, 0.4f, 0.4f), bronze, new Vector3(2.0f, 3.5f, 0)));                      // head
        b.AddChild(Util.Box(new Vector3(0.46f, 0.15f, 0.46f), new Color("#5d4037"), new Vector3(2.0f, 3.78f, 0)));    // headband
        b.AddChild(Util.Box(new Vector3(0.2f, 0.7f, 0.2f), bronze, new Vector3(2.0f, 3.0f, 0.5f)));                   // raised arm
        b.AddChild(Util.Box(new Vector3(0.07f, 2.6f, 0.07f), new Color("#6d4c41"), new Vector3(2.0f, 3.6f, 0.62f)));  // spear
        b.AddChild(Util.Box(new Vector3(0.12f, 0.8f, 0.55f), new Color("#5d4037"), new Vector3(1.6f, 2.9f, -0.55f))); // shield

        b.AddChild(Sign("LAPU-LAPU SHRINE", 60, Colors.White, new Color("#3e2723"), new Vector3(1.5f, 5.6f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Lapu-Lapu City: Mactan-Cebu airport with a parked plane
    // ------------------------------------------------------------------
    public static Node3D Airport()
    {
        var b = new Node3D();

        b.AddChild(Util.Box(new Vector3(4.5f, 1.6f, 4.6f), new Color("#eceff1"), new Vector3(5.45f, 0.8f, 0)));       // terminal
        b.AddChild(Util.Box(new Vector3(4.56f, 0.7f, 4.66f), new Color("#4fc3f7"), new Vector3(5.45f, 0.95f, 0)));    // glass band
        b.AddChild(Util.Box(new Vector3(0.9f, 3.6f, 0.9f), new Color("#cfd8dc"), new Vector3(7.0f, 1.8f, -1.6f)));    // control tower
        b.AddChild(Util.Box(new Vector3(1.3f, 0.8f, 1.3f), new Color("#80deea"), new Vector3(7.0f, 3.9f, -1.6f)));
        b.AddChild(Util.Box(new Vector3(1.4f, 0.1f, 1.4f), new Color("#546e7a"), new Vector3(7.0f, 4.35f, -1.6f)));

        // parked plane
        b.AddChild(Util.Box(new Vector3(0.65f, 0.65f, 3.8f), new Color("#fafafa"), new Vector3(1.5f, 0.8f, 0)));       // fuselage
        b.AddChild(Util.Box(new Vector3(0.5f, 0.18f, 0.05f), new Color("#263238"), new Vector3(1.5f, 0.95f, -1.92f)));  // cockpit windows
        b.AddChild(Util.Box(new Vector3(3.2f, 0.1f, 0.8f), new Color("#eceff1"), new Vector3(1.5f, 0.7f, 0.1f)));       // wings
        b.AddChild(Util.Box(new Vector3(0.1f, 1.0f, 0.6f), new Color("#d32f2f"), new Vector3(1.5f, 1.6f, 1.6f)));       // tail fin
        b.AddChild(Util.Box(new Vector3(1.2f, 0.08f, 0.45f), new Color("#eceff1"), new Vector3(1.5f, 1.0f, 1.75f)));    // stabilizer
        foreach (int sx in new[] { -1, 1 })
        {
            var engine = Util.Cyl(0.18f, 0.18f, 0.5f, new Color("#b0bec5"), new Vector3(1.5f + sx * 0.8f, 0.5f, -0.2f), 12);
            engine.RotationDegrees = new Vector3(90, 0, 0);
            b.AddChild(engine);
        }

        b.AddChild(Sign("MACTAN-CEBU AIRPORT", 56, Colors.White, new Color("#01579b"), new Vector3(3.5f, 5.2f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Carcar: the rotunda with its fountain and monument
    // ------------------------------------------------------------------
    public static Node3D Rotunda()
    {
        var b = new Node3D();

        b.AddChild(Util.Cyl(2.2f, 2.2f, 0.08f, new Color("#c8c3b8"), new Vector3(3.0f, 0.04f, 0), 24));              // plaza
        b.AddChild(Util.Cyl(1.3f, 1.3f, 0.45f, new Color("#90a4ae"), new Vector3(3.0f, 0.3f, 0), 24));               // basin
        b.AddChild(Util.Cyl(1.2f, 1.2f, 0.05f, new Color("#4fc3f7"), new Vector3(3.0f, 0.52f, 0), 24));              // water
        b.AddChild(Util.Box(new Vector3(0.5f, 2.0f, 0.5f), new Color("#f5f5f5"), new Vector3(3.0f, 1.5f, 0)));        // monument
        b.AddChild(Util.Box(new Vector3(0.45f, 0.5f, 0.45f), new Color("#8c6b3f"), new Vector3(3.0f, 2.75f, 0)));     // statue

        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                var pos = new Vector3(3.0f + sx * 1.7f, 0, sz * 1.7f);
                b.AddChild(Util.Box(new Vector3(0.06f, 1.2f, 0.06f), new Color("#37474f"), pos + new Vector3(0, 0.6f, 0)));
                b.AddChild(Util.Glow(new Vector3(0.2f, 0.2f, 0.2f), new Color("#fff59d"), pos + new Vector3(0, 1.3f, 0), 1f, out _, 2f));
            }

        b.AddChild(Sign("CARCAR ROTUNDA", 60, Colors.White, new Color("#455a64"), new Vector3(1.5f, 4.0f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Oslob: whale shark statue on a pool pedestal
    // ------------------------------------------------------------------
    public static Node3D WhaleShark()
    {
        var b = new Node3D();
        Color skin = new("#455a64");

        b.AddChild(Util.Cyl(2.6f, 2.6f, 0.25f, new Color("#0288d1"), new Vector3(3.0f, 0.125f, 0), 24));              // pool
        foreach (int sz in new[] { -1, 1 })
            b.AddChild(Util.Box(new Vector3(0.3f, 0.45f, 0.3f), new Color("#90a4ae"), new Vector3(3.0f, 0.475f, sz * 0.7f))); // posts

        b.AddChild(Util.Box(new Vector3(1.2f, 0.8f, 3.2f), skin, new Vector3(3.0f, 1.1f, 0)));                          // body
        b.AddChild(Util.Box(new Vector3(1.6f, 0.45f, 0.9f), skin, new Vector3(3.0f, 1.0f, -1.9f)));                     // wide flat head
        b.AddChild(Util.Box(new Vector3(1.0f, 0.1f, 3.0f), new Color("#eceff1"), new Vector3(3.0f, 0.7f, 0)));          // pale belly
        b.AddChild(Util.Box(new Vector3(0.3f, 0.5f, 1.2f), skin, new Vector3(3.0f, 1.0f, 2.2f)));                       // tail stock
        b.AddChild(Util.Box(new Vector3(0.1f, 1.1f, 0.5f), skin, new Vector3(3.0f, 1.35f, 2.7f)));                      // tail fin
        b.AddChild(Util.Box(new Vector3(0.1f, 0.4f, 0.5f), skin, new Vector3(3.0f, 1.7f, 0.2f)));                       // dorsal fin
        b.AddChild(Util.Box(new Vector3(1.8f, 0.06f, 0.6f), new Color("#37474f"), new Vector3(3.0f, 0.85f, -0.7f)));    // pectoral fins

        // white spots along the back
        for (int i = 0; i < 14; i++)
            b.AddChild(Util.Box(new Vector3(0.12f, 0.03f, 0.12f), Colors.White,
                new Vector3(3.0f + ((i * 37) % 9 - 4) * 0.12f, 1.515f, -1.3f + (i % 7) * 0.42f)));

        b.AddChild(Sign("OSLOB WHALE SHARKS", 56, Colors.White, new Color("#01579b"), new Vector3(1.5f, 3.2f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Moalboal / Badian: thatched beach resort with a dive flag
    // ------------------------------------------------------------------
    public static Node3D DiveResort(string name)
    {
        var b = new Node3D();
        Color wood = new("#7a4b22");

        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                b.AddChild(Util.Box(new Vector3(0.15f, 0.4f, 0.15f), wood, new Vector3(3.4f + sx * 1.1f, 0.2f, sz * 1.0f)));   // stilts

        b.AddChild(Util.Box(new Vector3(2.6f, 1.0f, 2.4f), new Color("#d9b779"), new Vector3(3.4f, 0.9f, 0)));               // walls
        b.AddChild(Util.Box(new Vector3(0.05f, 0.8f, 0.7f), new Color("#4a3322"), new Vector3(2.08f, 0.9f, 0)));             // door
        var roof = Util.Cyl(0f, 2.3f, 1.2f, new Color("#b8912f"), new Vector3(3.4f, 2.0f, 0), 4);                              // thatch roof
        roof.RotationDegrees = new Vector3(0, 45, 0);
        b.AddChild(roof);
        b.AddChild(Util.Box(new Vector3(1.2f, 0.1f, 2.6f), new Color("#b08850"), new Vector3(1.3f, 0.4f, 0)));               // front deck

        b.AddChild(Util.Box(new Vector3(0.05f, 1.4f, 0.05f), new Color("#eceff1"), new Vector3(1.0f, 1.1f, 1.5f)));          // dive flag
        b.AddChild(Util.Box(new Vector3(0.04f, 0.4f, 0.6f), new Color("#d32f2f"), new Vector3(1.0f, 1.6f, 1.8f)));

        b.AddChild(Palm(new Vector3(1.2f, 0, -1.8f)));
        b.AddChild(Palm(new Vector3(5.4f, 0, 1.8f)));

        b.AddChild(Sign(name, 56, Colors.White, new Color("#00695c"), new Vector3(1.5f, 4.0f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Moalboal: the famous sardine run as a swirling ball of fish
    // ------------------------------------------------------------------
    public static Node3D SardineRun()
    {
        var b = new Node3D();
        b.AddChild(Util.Cyl(2.4f, 2.4f, 0.15f, new Color("#0277bd"), new Vector3(3.0f, 0.075f, 0), 24));
        Color[] fish = { new("#cfd8dc"), new("#90a4ae"), new("#eceff1") };

        var rng = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            double theta = rng.NextDouble() * Mathf.Tau;
            double y = rng.NextDouble() * 2 - 1;
            double r = Mathf.Sqrt(1 - y * y);
            var pos = new Vector3(3.0f + (float)(r * Mathf.Cos(theta)) * 1.1f, 1.7f + (float)y * 1.1f, (float)(r * Mathf.Sin(theta)) * 1.1f);
            var f = Util.Box(new Vector3(0.2f, 0.07f, 0.04f), fish[i % fish.Length], pos);
            f.RotationDegrees = new Vector3(0, -(float)theta * 57.2958f + 90f, 0);
            b.AddChild(f);
        }

        b.AddChild(Sign("MOALBOAL SARDINE RUN", 56, Colors.White, new Color("#01579b"), new Vector3(1.5f, 3.9f, 0)));
        return b;
    }

    // ------------------------------------------------------------------
    //  Badian: Kawasan Falls tumbling into a turquoise pool
    // ------------------------------------------------------------------
    public static Node3D KawasanFalls()
    {
        var b = new Node3D();

        b.AddChild(Util.Box(new Vector3(2.5f, 4.0f, 4.2f), new Color("#5d6b4a"), new Vector3(4.0f, 2.0f, 0)));          // cliff
        b.AddChild(Util.Box(new Vector3(2.0f, 1.5f, 3.4f), new Color("#4e6b3a"), new Vector3(4.2f, 4.75f, 0)));         // jungle top
        b.AddChild(Util.Box(new Vector3(0.2f, 1.8f, 1.0f), new Color("#4dd0e1"), new Vector3(2.65f, 3.1f, 0)));         // upper falls
        b.AddChild(Util.Box(new Vector3(0.2f, 1.5f, 1.5f), new Color("#4dd0e1"), new Vector3(2.65f, 1.4f, 0.1f)));      // lower falls

        b.AddChild(Util.Box(new Vector3(2.4f, 0.12f, 3.0f), new Color("#26c6da"), new Vector3(1.4f, 0.06f, 0)));        // pool
        b.AddChild(Util.Box(new Vector3(0.8f, 0.05f, 0.9f), new Color("#e0f7fa"), new Vector3(2.3f, 0.145f, 0.1f)));    // foam
        b.AddChild(Util.Box(new Vector3(1.2f, 0.08f, 0.6f), new Color("#c9b458"), new Vector3(1.2f, 0.16f, 0.7f)));     // bamboo raft

        foreach (int sz in new[] { -1, 1 })
            b.AddChild(Util.Box(new Vector3(0.5f, 0.35f, 0.5f), new Color("#78909c"), new Vector3(0.4f, 0.17f, sz * 1.6f)));

        b.AddChild(Tree(new Vector3(4.0f, 5.5f, -1.0f)));
        b.AddChild(Tree(new Vector3(4.6f, 5.5f, 0.9f)));

        b.AddChild(Sign("KAWASAN FALLS", 60, Colors.White, new Color("#00838f"), new Vector3(1.5f, 7.0f, 0)));
        return b;
    }
}
