using Godot;
using System;

namespace CrossyRoadBase;

/// <summary>
/// A pedestrian that crosses the street exactly like the player: one hop per lane, straight across
/// from one sidewalk to the other. It only starts crossing when it can see a safe gap in every lane,
/// then waves at the far curb (flagging a jeepney) before crossing back.
/// </summary>
public partial class Pedestrian : Node3D
{
    const float HopTime = 0.22f;
    static readonly Random Rng = new();

    public int LowRow, HighRow;   // the two sidewalk rows with road lanes in between
    public int Row;

    int dir;                      // +1 = heading to HighRow, -1 = heading to LowRow
    float x;
    Func<int, Lane> getLane;

    bool hopping, waving, gossiping;
    float waitTimer, t, time, phase, gossipTargetX, gossipTimer;
    Vector3 from, to;

    Node3D model, legL, legR, armL, armR;
    Label3D gossipLabel;

    public bool CanGossip => !hopping && !gossiping && (Row == LowRow || Row == HighRow);
    public float WorldX => Position.X;

    public Func<Player> GetPlayer;                       // so pedestrians never walk into the player
    public int OccupiedRow => Mathf.RoundToInt(-Position.Z);
    public int TargetRow => hopping ? Row + dir : Row;

    /// <summary>True if this pedestrian is standing in (or hopping into) that tile.</summary>
    public bool BlocksCell(int row, int cellX) =>
        Math.Abs(Position.X - cellX) < 0.95f && (OccupiedRow == row || TargetRow == row);

    bool PlayerOnCell(int row)
    {
        var pl = GetPlayer?.Invoke();
        return pl != null
            && (pl.Row == row || Mathf.RoundToInt(-pl.Position.Z) == row)
            && Math.Abs(pl.Position.X - x) < 0.95f;
    }

    public void StartGossip(float targetX, string dialogue, float duration)
    {
        if (!CanGossip) return;
        gossipTargetX = targetX;
        gossipTimer = duration;
        gossiping = true;
        waving = false;
        gossipLabel.Text = dialogue;
        gossipLabel.Visible = true;
    }

    public void Init(int lowRow, int highRow, int cellX, bool startAtLow, Func<int, Lane> laneLookup)
    {
        LowRow = lowRow;
        HighRow = highRow;
        x = cellX;
        getLane = laneLookup;
        Row = startAtLow ? lowRow : highRow;
        dir = startAtLow ? 1 : -1;
        Position = new Vector3(x, 0, -Row);
        waitTimer = 1f + (float)Rng.NextDouble() * 4f;
        phase = (float)Rng.NextDouble() * 6.28f;
        BuildModel();
        FaceDirection();
    }

    static Node3D Limb(Node3D root, Vector3 pivotPos, Vector3 size, Color c)
    {
        var pivot = new Node3D { Position = pivotPos };
        pivot.AddChild(Util.Box(size, c, new Vector3(0, -size.Y / 2, 0)));
        root.AddChild(pivot);
        return pivot;
    }

    void BuildModel()
    {
        Color[] shirts = { new("#e74c3c"), new("#3498db"), new("#f1c40f"), new("#9b59b6"), new("#1abc9c"), new("#e67e22"), new("#ecf0f1") };
        Color[] skins = { new("#f1c27d"), new("#c68642"), new("#8d5524"), new("#e0ac69") };
        Color[] pants = { new("#2c3e50"), new("#34495e"), new("#5d4037"), new("#1b2631") };
        Color shirt = shirts[Rng.Next(shirts.Length)];
        Color skin = skins[Rng.Next(skins.Length)];
        Color pant = pants[Rng.Next(pants.Length)];

        model = new Node3D();
        AddChild(model);
        legL = Limb(model, new Vector3(-0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
        legR = Limb(model, new Vector3(0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
        model.AddChild(Util.Box(new Vector3(0.45f, 0.4f, 0.28f), shirt, new Vector3(0, 0.55f, 0)));
        armL = Limb(model, new Vector3(-0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
        armR = Limb(model, new Vector3(0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
        model.AddChild(Util.Box(new Vector3(0.3f, 0.3f, 0.3f), skin, new Vector3(0, 0.9f, 0)));
        model.AddChild(Util.Box(new Vector3(0.32f, 0.1f, 0.32f), new Color("#2b1d14"), new Vector3(0, 1.07f, 0)));
        model.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(-0.07f, 0.93f, -0.15f)));
        model.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(0.07f, 0.93f, -0.15f)));

        gossipLabel = new Label3D
        {
            FontSize = 24,
            PixelSize = 0.008f,
            OutlineSize = 5,
            OutlineModulate = Colors.Black,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = false,
            Position = new Vector3(0, 1.75f, 0)
        };
        AddChild(gossipLabel);
    }

    void FaceDirection() => model.Rotation = new Vector3(0, dir > 0 ? 0 : Mathf.Pi, 0);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        time += dt;

        if (gossiping)
        {
            gossipTimer -= dt;
            float dx = gossipTargetX - Position.X;
            float step = Math.Min(Math.Abs(dx), 1.2f * dt);
            var p = Position;
            p.X += Math.Sign(dx) * step;
            Position = p;
            x = p.X;
            model.Rotation = new Vector3(0, dx > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2, 0);

            if (gossipTimer <= 0f)
            {
                gossiping = false;
                gossipLabel.Visible = false;
                waitTimer = 2f + (float)Rng.NextDouble() * 3f;
                FaceDirection();
            }
            Animate();
            return;
        }

        if (hopping)
        {
            t += dt / HopTime;
            if (t >= 1f)
            {
                Position = to;
                Row += dir;
                hopping = false;

                if (Row == LowRow || Row == HighRow) // reached the far sidewalk
                {
                    dir = Row == LowRow ? 1 : -1;
                    waitTimer = 2f + (float)Rng.NextDouble() * 5f;
                    waving = true;
                    FaceDirection();
                }
                else
                {
                    if (PlayerOnCell(Row + dir)) waitTimer = 0.25f; else StartHop(); // keep going, unless the player is in the way
                }
            }
            else
            {
                var p = from.Lerp(to, t);
                p.Y = Mathf.Sin(t * Mathf.Pi) * 0.3f;
                Position = p;
            }
        }
        else
        {
            waitTimer -= dt;
            if (waitTimer <= 0f && CanCross())
            {
                waving = false;
                FaceDirection();
                StartHop();
            }
        }

        Animate();
    }

    void StartHop()
    {
        from = Position;
        to = new Vector3(x, 0, -(Row + dir));
        t = 0;
        hopping = true;
    }

    /// <summary>True only if no vehicle will be near this x in any road lane at the moment we'd be there.</summary>
    bool CanCross()
    {
        int destRow = dir > 0 ? HighRow : LowRow;
        var destLane = getLane(destRow);
        if (destLane == null) return false;

        for (int j = 1; j <= Math.Abs(destRow - Row); j++)
            if (PlayerOnCell(Row + dir * j)) return false;   // the player is standing in the way

        int hops = Math.Abs(destRow - Row);
        for (int j = 1; j < hops; j++)
        {
            var lane = getLane(Row + dir * j);
            if (lane == null) return false;
            float t0 = (j - 0.5f) * HopTime - 0.1f;
            float t1 = (j + 0.5f) * HopTime + 0.1f;
            if (!lane.IsClear(x, t0, t1)) return false;
        }
        return true;
    }

    void Animate()
    {
        float s = Mathf.Sin(time * 16f + phase);
        if (hopping)
        {
            legL.RotationDegrees = new Vector3(s * 35f, 0, 0);
            legR.RotationDegrees = new Vector3(-s * 35f, 0, 0);
            armL.RotationDegrees = new Vector3(-s * 30f, 0, 0);
            armR.RotationDegrees = new Vector3(s * 30f, 0, 0);
        }
        else
        {
            legL.RotationDegrees = Vector3.Zero;
            legR.RotationDegrees = Vector3.Zero;
            armL.RotationDegrees = gossiping ? new Vector3(0, 0, 35f + Mathf.Sin(time * 5f + phase) * 12f) : Vector3.Zero;
            armR.RotationDegrees = gossiping
                ? new Vector3(0, 0, -35f + Mathf.Sin(time * 5f + phase) * 12f)
                : waving ? new Vector3(0, 0, 150f + Mathf.Sin(time * 6f + phase) * 18f) // flagging a jeepney
                : Vector3.Zero;
        }
    }
}
