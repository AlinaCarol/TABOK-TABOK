using Godot;

namespace CrossyRoadBase;

/// <summary>The chicken. Hops one tile at a time with a little arc.</summary>
public partial class Player : Node3D
{
    const float HopTime = 0.12f;

    public int Row;
    public bool Hopping;
    public bool Alive = true;
    public Node3D Model;

    Vector3 from, to;
    float t;

    public override void _Ready()
    {
        Model = new Node3D();
        AddChild(Model);
        Model.AddChild(Util.Box(new Vector3(0.6f, 0.6f, 0.6f), Colors.White, new Vector3(0, 0.3f, 0)));
        Model.AddChild(Util.Box(new Vector3(0.12f, 0.15f, 0.25f), new Color("#e74c3c"), new Vector3(0, 0.67f, -0.05f)));
        Model.AddChild(Util.Box(new Vector3(0.18f, 0.12f, 0.2f), new Color("#f39c12"), new Vector3(0, 0.35f, -0.4f)));
        Model.AddChild(Util.Box(new Vector3(0.08f, 0.08f, 0.04f), Colors.Black, new Vector3(-0.2f, 0.45f, -0.31f)));
        Model.AddChild(Util.Box(new Vector3(0.08f, 0.08f, 0.04f), Colors.Black, new Vector3(0.2f, 0.45f, -0.31f)));
    }

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
}
