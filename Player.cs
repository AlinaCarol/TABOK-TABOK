using Godot;

namespace CrossyRoadBase;

public enum PlayerGender { Boy, Girl }

/// <summary>
/// The player: a USC student, boy or girl (picked on the character select screen).
///
/// 3D ASSETS: drop boy.glb and girl.glb (or .gltf / .fbx / .obj) into res://Assets/Characters/ and they replace
/// the built-in voxel student automatically. They are scaled to AssetHeight and centered on their feet.
/// If your model faces the wrong way, change AssetYawOffsetDegrees.
/// </summary>
public partial class Player : Node3D
{
    public static PlayerGender Gender = PlayerGender.Girl;      // kept between restarts

    const float HopTime = 0.12f;
    const float StudentScale = 0.8f;                             // size of the built-in voxel student
    const float AssetHeight = 1.15f;                             // 3D assets are scaled to this height (a tile is 1.0 wide)
    const float AssetYawOffsetDegrees = 180f;                    // glTF / Meshy models face +Z, this game faces -Z
    static readonly string[] AssetExtensions = { "glb", "gltf", "fbx", "obj" };

    public int Row;
    public bool Hopping;
    public bool Alive = true;
    public Node3D Model;          // rotated to face the hop direction; the visual (voxels or 3D asset) lives inside it

    Vector3 from, to;
    float t;
    Node3D held;

    public override void _Ready()
    {
        Model = new Node3D();
        AddChild(Model);

        bool girl = Gender == PlayerGender.Girl;
        Model.AddChild(TryLoadAsset(girl ? "girl" : "boy") ?? BuildStudent(girl));
    }

    // =====================================================================
    //  3D asset loading (Meshy exports etc.)
    // =====================================================================

    static Node3D TryLoadAsset(string baseName)
    {
        foreach (string ext in AssetExtensions)
        {
            string path = $"res://Assets/Characters/{baseName}.{ext}";
            if (!ResourceLoader.Exists(path)) continue;

            var res = GD.Load(path);
            Node3D inst = null;
            if (res is PackedScene scene) inst = scene.Instantiate<Node3D>();
            else if (res is Mesh mesh) inst = new MeshInstance3D { Mesh = mesh };
            if (inst == null) continue;

            GD.Print($"[Player] using 3D asset {path}");
            return FitAsset(inst);
        }
        return null;
    }

    /// <summary>Scales the asset to AssetHeight, stands it on y = 0, centers it, and turns it to face -Z.</summary>
    static Node3D FitAsset(Node3D inst)
    {
        Aabb? bounds = null;
        CollectBounds(inst, Transform3D.Identity, ref bounds);

        var holder = new Node3D { RotationDegrees = new Vector3(0, AssetYawOffsetDegrees, 0) };
        if (bounds.HasValue && bounds.Value.Size.Y > 0.001f)
        {
            Aabb box = bounds.Value;
            float scale = AssetHeight / box.Size.Y;
            Vector3 center = box.Position + box.Size / 2f;
            inst.Scale = inst.Scale * scale;
            inst.Position = new Vector3(-center.X * scale, -box.Position.Y * scale, -center.Z * scale);
        }
        holder.AddChild(inst);
        return holder;
    }

    static void CollectBounds(Node node, Transform3D parent, ref Aabb? bounds)
    {
        Transform3D xf = parent;
        if (node is Node3D n3) xf = parent * n3.Transform;

        if (node is MeshInstance3D mi && mi.Mesh != null)
        {
            Aabb box = xf * mi.GetAabb();
            bounds = bounds.HasValue ? bounds.Value.Merge(box) : box;
        }
        foreach (Node child in node.GetChildren())
            CollectBounds(child, xf, ref bounds);
    }

    // =====================================================================
    //  The built-in voxel USC student (matches the Meshy drafts)
    // =====================================================================

    static Node3D BuildStudent(bool girl)
    {
        var v = new Node3D { Scale = new Vector3(StudentScale, StudentScale, StudentScale) };
        Color skin = new("#f0ac66"), hair = new("#2b2022"), white = new("#f6f6f6"), black = new("#1d1d21"), green = new("#1b6b3a");

        // black school shoes
        foreach (int sx in new[] { -1, 1 })
            v.AddChild(Util.Box(new Vector3(0.17f, 0.08f, 0.27f), black, new Vector3(sx * 0.1f, 0.04f, -0.04f)));

        if (girl)
        {
            foreach (int sx in new[] { -1, 1 })
            {
                v.AddChild(Util.Box(new Vector3(0.14f, 0.1f, 0.15f), white, new Vector3(sx * 0.1f, 0.13f, 0)));    // white socks
                v.AddChild(Util.Box(new Vector3(0.13f, 0.2f, 0.14f), skin, new Vector3(sx * 0.1f, 0.28f, 0)));     // legs
            }

            // green plaid skirt: olive base with darker pleat lines
            v.AddChild(Util.Box(new Vector3(0.5f, 0.24f, 0.34f), new Color("#868f52"), new Vector3(0, 0.48f, 0)));
            for (int i = -2; i <= 2; i++)
                v.AddChild(Util.Box(new Vector3(0.03f, 0.22f, 0.38f), new Color("#5f6a37"), new Vector3(i * 0.1f, 0.48f, 0)));

            v.AddChild(Util.Box(new Vector3(0.46f, 0.35f, 0.26f), white, new Vector3(0, 0.775f, 0)));              // white blouse
        }
        else
        {
            foreach (int sx in new[] { -1, 1 })
                v.AddChild(Util.Box(new Vector3(0.18f, 0.47f, 0.2f), new Color("#222228"), new Vector3(sx * 0.105f, 0.315f, 0))); // black pants

            v.AddChild(Util.Box(new Vector3(0.46f, 0.4f, 0.26f), white, new Vector3(0, 0.75f, 0)));                // white polo
            v.AddChild(Util.Box(new Vector3(0.1f, 0.1f, 0.02f), new Color("#e3e3e3"), new Vector3(0.13f, 0.82f, -0.14f))); // chest pocket
        }

        // arms: short white sleeves, bare forearms
        foreach (int sx in new[] { -1, 1 })
        {
            v.AddChild(Util.Box(new Vector3(0.14f, 0.17f, 0.18f), white, new Vector3(sx * 0.31f, 0.86f, 0)));
            v.AddChild(Util.Box(new Vector3(0.11f, 0.27f, 0.14f), skin, new Vector3(sx * 0.31f, 0.64f, 0)));
        }

        // collar
        foreach (int sx in new[] { -1, 1 })
        {
            var flap = Util.Box(new Vector3(0.12f, 0.08f, 0.03f), white, new Vector3(sx * 0.08f, 0.94f, -0.14f));
            flap.RotationDegrees = new Vector3(0, 0, -25 * sx);
            v.AddChild(flap);
        }

        // USC lanyard and ID card
        foreach (int sx in new[] { -1, 1 })
        {
            var strap = Util.Box(new Vector3(0.035f, 0.27f, 0.02f), green, new Vector3(sx * 0.06f, 0.81f, -0.14f));
            strap.RotationDegrees = new Vector3(0, 0, -27 * sx);
            v.AddChild(strap);
        }
        v.AddChild(Util.Box(new Vector3(0.04f, 0.05f, 0.02f), black, new Vector3(0, 0.7f, -0.14f)));               // clip
        v.AddChild(Util.Box(new Vector3(0.13f, 0.17f, 0.02f), green, new Vector3(0, 0.62f, -0.145f)));              // ID card
        v.AddChild(Util.Box(new Vector3(0.05f, 0.05f, 0.02f), Colors.White, new Vector3(0, 0.65f, -0.155f)));       // photo
        v.AddChild(Util.Box(new Vector3(0.09f, 0.015f, 0.02f), Colors.White, new Vector3(0, 0.58f, -0.155f)));      // text line

        // head and face
        v.AddChild(Util.Box(new Vector3(0.4f, 0.38f, 0.38f), skin, new Vector3(0, 1.14f, 0)));
        foreach (int sx in new[] { -1, 1 })
        {
            v.AddChild(Util.Box(new Vector3(0.05f, 0.1f, 0.06f), skin, new Vector3(sx * 0.225f, 1.12f, 0)));        // ears
            v.AddChild(Util.Box(new Vector3(0.06f, 0.1f, 0.03f), black, new Vector3(sx * 0.1f, 1.16f, -0.2f)));     // eyes
            v.AddChild(Util.Box(new Vector3(0.1f, 0.03f, 0.03f), new Color("#1a1415"), new Vector3(sx * 0.1f, 1.25f, -0.2f))); // brows
        }
        v.AddChild(Util.Box(new Vector3(0.1f, 0.05f, 0.03f), new Color("#c8504f"), new Vector3(0, 1.04f, -0.2f)));  // mouth

        // hair
        v.AddChild(Util.Box(new Vector3(0.44f, 0.12f, 0.44f), hair, new Vector3(0, 1.35f, 0)));                     // top
        v.AddChild(Util.Box(new Vector3(0.44f, 0.1f, 0.06f), hair, new Vector3(0, 1.3f, -0.2f)));                   // fringe
        if (girl)
        {
            v.AddChild(Util.Box(new Vector3(0.46f, 0.62f, 0.12f), hair, new Vector3(0, 0.98f, 0.2f)));              // long hair down the back
            foreach (int sx in new[] { -1, 1 })
                v.AddChild(Util.Box(new Vector3(0.06f, 0.36f, 0.3f), hair, new Vector3(sx * 0.23f, 1.15f, 0.04f))); // side locks
        }
        else
        {
            v.AddChild(Util.Box(new Vector3(0.44f, 0.34f, 0.1f), hair, new Vector3(0, 1.17f, 0.2f)));               // short back and sides
        }

        return v;
    }

    // =====================================================================
    //  Movement and carrying things
    // =====================================================================

    public void StartHop(Vector3 target, float yaw, int newRow)
    {
        from = Position;
        to = target;
        t = 0;
        Row = newRow;
        Hopping = true;
        Model.Rotation = new Vector3(0, yaw, 0);
    }

    public override void _Process(double delta)
    {
        if (!Alive || !Hopping) return;

        t += (float)delta / HopTime;
        if (t >= 1f)
        {
            Position = to;
            Hopping = false;
            return;
        }
        var p = from.Lerp(to, t);
        p.Y = Mathf.Sin(t * Mathf.Pi) * 0.45f;
        Position = p;
    }

    /// <summary>The student carries whatever the vendor gave them: a cup of taho, chicharon or a buko.</summary>
    public void HoldItem(string kind)
    {
        held?.QueueFree();
        held = new Node3D { Position = new Vector3(0.34f, 0.42f, -0.12f) };

        switch (kind)
        {
            case "chicharon":
                held.AddChild(Util.Box(new Vector3(0.22f, 0.1f, 0.2f), new Color("#d4902a"), Vector3.Zero));
                held.AddChild(Util.Box(new Vector3(0.14f, 0.08f, 0.16f), new Color("#b8741e"), new Vector3(0.05f, 0.08f, 0.02f)));
                break;
            case "buko":
                held.AddChild(Util.Cyl(0.12f, 0.12f, 0.22f, new Color("#7cb342"), Vector3.Zero, 10));   // green coconut
                var straw = Util.Box(new Vector3(0.03f, 0.22f, 0.03f), Colors.White, new Vector3(0.03f, 0.2f, 0));
                straw.RotationDegrees = new Vector3(0, 0, -15);
                held.AddChild(straw);
                break;
            default:
                held.AddChild(Util.Cyl(0.1f, 0.075f, 0.2f, new Color("#f1f1f1"), Vector3.Zero, 10));    // taho cup
                held.AddChild(Util.Cyl(0.1f, 0.1f, 0.03f, new Color("#7b4a1e"), new Vector3(0, 0.1f, 0), 10)); // arnibal on top
                break;
        }
        Model.AddChild(held);
    }
}
