using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrossyRoadBase;

public partial class Main : Node3D
{
	const float EagleWarnTime = 5f; // seconds without progress before the eagle appears

	static int best;

	readonly Dictionary<int, Lane> lanes = new();
	readonly Random rng = new();
	readonly List<Pedestrian> pedestrians = new();
	readonly List<TahoMan> tahoMen = new();

	Player player;
	Eagle eagle;
	Camera3D camera;
	Label scoreLabel, warnLabel, deathTitle, deathScore;
	Control deathBox;
	Vector3 camFocus = new(0, 0, -2);
	int nextRow = -4, maxRow, riverStreak;
	float idle;
	int nextLandmarkRow = 6, landmarkEnd = -1, accidentBackup;
	readonly List<string> landmarkBag = new();
	readonly List<(Node3D Node, int Row)> landmarks = new();

	public override void _Ready()
	{
		GetViewport().Msaa3D = Viewport.Msaa.Msaa4X; // smooths thin stripes so they stop shimmering
		City.Current = City.PickNext(rng);           // a new place in Cebu every run
		BuildEnvironment();
		GenerateTo(20);

		player = new Player();
		AddChild(player);

		eagle = new Eagle();
		AddChild(eagle);
		eagle.Grabbed += () => Die("The Philippine Eagle\ngot you!", squash: false, byEagle: true);

		BuildHud();
		UpdateHud();
		PlaceCamera();
	}

	// ---------- setup ----------

	void BuildEnvironment()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = City.Current.Sky,
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = Colors.White,
			AmbientLightEnergy = 0.55f,
			GlowEnabled = true,        // makes the warning lights, brake lights and flashers bloom
			GlowIntensity = 0.7f,
			GlowBloom = 0.05f,
			GlowHdrThreshold = 1.6f
		};
		AddChild(new WorldEnvironment { Environment = env });
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, 35, 0), ShadowEnabled = true });

		camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 13, Far = 120 };
		AddChild(camera);
	}

	void BuildHud()
	{
		var layer = new CanvasLayer();
		AddChild(layer);

		scoreLabel = new Label { Position = new Vector2(20, 12) };
		scoreLabel.AddThemeFontSizeOverride("font_size", 32);
		layer.AddChild(scoreLabel);

		// which place are we in?
		var cityLabel = new Label { Text = City.Current.Name, HorizontalAlignment = HorizontalAlignment.Right };
		cityLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		cityLabel.OffsetTop = 12;
		cityLabel.OffsetBottom = 52;
		cityLabel.OffsetRight = -20;
		cityLabel.AddThemeFontSizeOverride("font_size", 28);
		layer.AddChild(cityLabel);

		var banner = new Label
		{
			Text = $"WELCOME TO {City.Current.Name.ToUpper()}!\n{City.Current.Tagline}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		banner.OffsetTop = 120;
		banner.OffsetBottom = 240;
		banner.AddThemeFontSizeOverride("font_size", 40);
		banner.AddThemeColorOverride("font_color", Colors.White);
		banner.AddThemeColorOverride("font_outline_color", Colors.Black);
		banner.AddThemeConstantOverride("outline_size", 10);
		layer.AddChild(banner);

		var fade = CreateTween();
		fade.TweenInterval(3.0);
		fade.TweenProperty(banner, "modulate:a", 0f, 1.0f);

		// warning shown while the eagle is on its way
		warnLabel = new Label
		{
			Text = "Keep moving forward! The Philippine Eagle is coming!",
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		warnLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		warnLabel.OffsetTop = 60;
		warnLabel.OffsetBottom = 110;
		warnLabel.AddThemeFontSizeOverride("font_size", 30);
		warnLabel.AddThemeColorOverride("font_color", new Color("#ff5a4d"));
		warnLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		warnLabel.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(warnLabel);

		// game over box
		var center = new CenterContainer { Visible = false };
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(center);
		deathBox = center;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.13f, 0.93f),
			BorderColor = Colors.White
		};
		style.SetBorderWidthAll(4);
		style.SetCornerRadiusAll(18);
		style.SetContentMarginAll(30);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
		panel.AddThemeStyleboxOverride("panel", style);
		center.AddChild(panel);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 14);
		panel.AddChild(box);

		deathTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		deathTitle.AddThemeFontSizeOverride("font_size", 40);
		box.AddChild(deathTitle);

		deathScore = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		deathScore.AddThemeFontSizeOverride("font_size", 26);
		box.AddChild(deathScore);

		var button = new Button
		{
			Text = "Play Again",
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(0, 56)
		};
		button.AddThemeFontSizeOverride("font_size", 28);
		button.Pressed += Restart;
		box.AddChild(button);

		var hint = new Label { Text = "or press R / Space", HorizontalAlignment = HorizontalAlignment.Center };
		hint.AddThemeFontSizeOverride("font_size", 18);
		hint.Modulate = new Color(1, 1, 1, 0.6f);
		box.AddChild(hint);
	}

	void UpdateHud() => scoreLabel.Text = $"Score: {maxRow}    Best: {best}";

	void Restart() => GetTree().ReloadCurrentScene(); // no limit on restarts

	// ---------- world generation ----------

	void GenerateTo(int targetRow)
	{
		while (nextRow <= targetRow)
		{
			int row = nextRow;

			// every so often, reserve five rows on the right side for a Cebu landmark
			if (row >= 6 && row >= nextLandmarkRow && row > landmarkEnd)
			{
				landmarkEnd = row + 4;
				nextLandmarkRow = row + 5 + rng.Next(8, 18);
				SpawnLandmark(row + 2);
			}
			bool inLandmarkZone = row <= landmarkEnd;

			var type = PickType(row);
			if (inLandmarkZone) { type = LaneType.Grass; riverStreak = 0; } // sidewalk plaza in front of every landmark: no traffic runs through it
			bool clear = type == LaneType.Grass && row >= 4 && rng.NextDouble() < 0.15;               // some sidewalks are the taho man's street

            // roads: normal traffic, a jam, or an accident (jams back up behind accidents)
            var mode = RoadMode.Flow;
            if (type == LaneType.Road)
            {
                double m = rng.NextDouble();
                if (accidentBackup > 0 && m < 0.85) { mode = RoadMode.Jam; accidentBackup--; }
                else if (m < City.Current.AccidentChance) { mode = RoadMode.Accident; accidentBackup = rng.Next(1, 3); }
                else if (m < City.Current.AccidentChance + City.Current.JamChance) mode = RoadMode.Jam;
            }

            // keep jam gaps passable: the row after a jam must leave the gap open, and jam rows line up
            lanes.TryGetValue(row - 1, out var prev);
            var jamGaps = prev != null && prev.Type == LaneType.Road && prev.Mode == RoadMode.Jam ? prev.JamGaps : null;
            int hint = int.MinValue;
            if (jamGaps != null) foreach (int g in jamGaps) { hint = g; break; }

            var lane = new Lane { Mode = mode, KeepFree = jamGaps, AvoidCells = prev?.Blocked, GapHint = hint };
            lane.Init(type, row, clear, inLandmarkZone);
            AddChild(lane);
            lanes[row] = lane;
            nextRow++;

            if (type == LaneType.Grass && row >= 3) TrySpawnPedestrians(row);
            if (clear) SpawnTaho(row);
        }
    }

    void SpawnTaho(int row)
    {
        var taho = new TahoMan();
        AddChild(taho);
        taho.Init(row, () => player);
        tahoMen.Add(taho);
    }

    void SpawnLandmark(int centerRow)
    {
        if (landmarkBag.Count == 0)
			landmarkBag.AddRange(City.Current.Landmarks.OrderBy(_ => rng.Next())); // shuffled so they don't repeat back to back

		string key = landmarkBag[^1];
		landmarkBag.RemoveAt(landmarkBag.Count - 1);

		var building = Landmarks.Build(key);
		building.Position = new Vector3(Lane.MaxX + 1.5f, 0, -centerRow);
		AddChild(building);
		landmarks.Add((building, centerRow));
	}

	LaneType PickType(int row)
	{
		if (row <= 2) { riverStreak = 0; return LaneType.Grass; }

		var c = City.Current;
		float total = c.WGrass + c.WRoad + c.WConstruction + c.WRiver;
		float r = (float)rng.NextDouble() * total;
		var t = r < c.WGrass ? LaneType.Grass
			  : r < c.WGrass + c.WRoad ? LaneType.Road
			  : r < c.WGrass + c.WRoad + c.WConstruction ? LaneType.Construction
			  : LaneType.River;
		if (t == LaneType.River && riverStreak >= 3) t = LaneType.Grass;
		riverStreak = t == LaneType.River ? riverStreak + 1 : 0;
		return t;
	}

	/// <summary>
	/// Called for each new sidewalk. If there is a stretch of road right behind it (between this
	/// sidewalk and the previous one), maybe put pedestrians there who will cross it.
	/// </summary>
	void TrySpawnPedestrians(int highRow)
	{
		int low = highRow - 1;
		while (lanes.TryGetValue(low, out var l) && l.Type == LaneType.Road) low--;
		if (low == highRow - 1) return;                                                  // no road in between
		if (!lanes.TryGetValue(low, out var lowLane) || lowLane.Type != LaneType.Grass) return; // canal or missing
		if (rng.NextDouble() > 0.6) return;

		var highLane = lanes[highRow];
		int count = rng.Next(1, 3);
		var usedX = new HashSet<int>();
		for (int i = 0; i < count; i++)
		{
			for (int attempt = 0; attempt < 24; attempt++)
			{
				int cx = rng.Next(-6, 7);
				if (usedX.Contains(cx) || lowLane.Blocked.Contains(cx) || highLane.Blocked.Contains(cx) || !PathClear(low, highRow, cx)) continue;

				usedX.Add(cx);
				var ped = new Pedestrian();
				AddChild(ped);
				ped.Init(low, highRow, cx, rng.Next(2) == 0, r => lanes.TryGetValue(r, out var ln) ? ln : null);
				pedestrians.Add(ped);
				break;
			}
		}
	}

	bool PathClear(int low, int high, int cx)
	{
		for (int r = low + 1; r < high; r++)
			if (lanes.TryGetValue(r, out var l) && l.Blocked.Contains(cx)) return false;
		return true;
	}

	void CullLanes()
	{
		foreach (var lm in landmarks.Where(l => l.Row < maxRow - 12).ToList())
		{
			lm.Node.QueueFree();
			landmarks.Remove(lm);
		}
		foreach (var taho in tahoMen.Where(t => t.Row < maxRow - 10).ToList())
		{
			taho.QueueFree();
			tahoMen.Remove(taho);
		}
		foreach (var ped in pedestrians.Where(p => p.LowRow < maxRow - 10).ToList())
		{
			ped.QueueFree();
			pedestrians.Remove(ped);
		}

		foreach (int row in lanes.Keys.Where(r => r < maxRow - 10).ToList())
		{
			lanes[row].QueueFree();
			lanes.Remove(row);
		}
	}

	// ---------- input ----------

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } k) return;

		if (k.Keycode == Key.R) { Restart(); return; } // restart any time

		if (!player.Alive)
		{
			if (k.Keycode is Key.Space or Key.Enter) Restart();
			return;
		}

		switch (k.Keycode)
		{
			case Key.W: case Key.Up:    TryMove(0, 1); break;
			case Key.S: case Key.Down:  TryMove(0, -1); break;
			case Key.A: case Key.Left:  TryMove(-1, 0); break;
			case Key.D: case Key.Right: TryMove(1, 0); break;
		}
	}

	void TryMove(int dx, int dr)
	{
		if (!player.Alive || player.Hopping) return;

		int newRow = player.Row + dr;
		int newX = Mathf.RoundToInt(player.Position.X) + dx;

		if (Math.Abs(newX) > Lane.MaxX) return;
		if (newRow < maxRow - 4) return;                       // can't retreat too far
        if (!lanes.TryGetValue(newRow, out var lane)) return;
        if (lane.Blocked.Contains(newX)) return;               // person / sign / bin / hut
        foreach (var taho in tahoMen)                          // the taho man is solid too
            if (taho.Row == newRow && Math.Abs(taho.Position.X - newX) < 1.9f) return;

        float yaw = dr > 0 ? 0 : dr < 0 ? Mathf.Pi : dx < 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2;
        player.StartHop(new Vector3(newX, 0, -newRow), yaw, newRow);

        if (newRow > maxRow)
        {
            maxRow = newRow;
            best = Math.Max(best, maxRow);
            idle = 0;
            eagle.Stop(); // moving forward scares the eagle off
            UpdateHud();
            GenerateTo(maxRow + 20);
            CullLanes();
        }
    }

    // ---------- game loop ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (player.Alive)
        {
            CheckHazards(dt);

            if (player.Alive) idle += dt;
            if (player.Alive && idle > EagleWarnTime && !eagle.Active)
                eagle.Swoop(player.Position + new Vector3(8, 10, -12), () => player.Position + new Vector3(0, 0.5f, 0));
        }

        if (eagle.Carrying) player.Position = eagle.Position + new Vector3(0, -0.7f, 0);
        warnLabel.Visible = player.Alive && eagle.Swooping;

        // smooth camera follow
        var focus = new Vector3(player.Position.X * 0.5f, 0, player.Position.Z - 2f);
        camFocus = camFocus.Lerp(focus, 1f - Mathf.Exp(-6f * dt));
        PlaceCamera();
    }

    void CheckHazards(float dt)
    {
        int row = Mathf.RoundToInt(-player.Position.Z);
        if (!lanes.TryGetValue(row, out var lane)) return;

        if (lane.Type == LaneType.Road)
        {
            string hit = lane.HitBy(player.Position.X);
            if (hit != null)
                Die($"Hit by a {hit}!", squash: true);
        }
        else if (lane.Type == LaneType.River && !player.Hopping)
        {
            if (lane.TryGetRaftVelocity(player.Position.X, out float vel))
            {
                player.Position += new Vector3(vel * dt, 0, 0); // ride the floating trash
                if (Math.Abs(player.Position.X) > Lane.MaxX + 0.5f)
                    Die(City.Current.CleanWater ? "Swept away by\nthe current!" : "Swept away by\nthe estero!", squash: false);
            }
            else
            {
                Die(City.Current.CleanWater ? "Splash! You fell\nin the water!" : "Yuck! You fell in\nthe estero!", squash: false);
                CreateTween().TweenProperty(player, "position:y", -0.8f, 0.4f);
            }
        }
    }

    void Die(string title, bool squash, bool byEagle = false)
    {
        if (!player.Alive) return;
        player.Alive = false;
        if (!byEagle) eagle.Stop();
        if (squash) player.Scale = new Vector3(1.3f, 0.08f, 1.3f);

        deathTitle.Text = title;
        deathScore.Text = $"{City.Current.Name}\nScore: {maxRow}    Best: {best}";
        deathBox.Visible = true;
    }

    void PlaceCamera()
    {
        camera.Position = camFocus + new Vector3(-7, 11, 7);
        camera.LookAt(camFocus, Vector3.Up);
    }
}
