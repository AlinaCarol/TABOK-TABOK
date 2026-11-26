using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrossyRoadBase;

public partial class Main : Node3D
{
	const float EagleWarnTime = 5f; // seconds without progress before the eagle appears

	sealed class EventNpc
	{
		public Node3D Node;
		public Lane Lane;
		public int[] Cells;
		public float TimeLeft;
	}

	static int best;

	readonly Dictionary<int, Lane> lanes = new();
	readonly Random rng = new();
	readonly List<Pedestrian> pedestrians = new();
	readonly List<TahoMan> tahoMen = new();
	readonly List<EventNpc> eventNpcs = new();

	Player player;
	Eagle eagle;
	Camera3D camera;
	Label scoreLabel, warnLabel, deathTitle, deathScore;
	Label snackLabel, talkPrompt, toastLabel;
	TahoMan talkTarget;
	Tween toastTween;
	int snacks;
	Label streetEventLabel;
	ColorRect blackout;
	Control deathBox;
	Vector3 camFocus = new(0, 0, -2);
	int nextRow = -4, maxRow, riverStreak, roadRunLength;
	float idle;
	int nextLandmarkRow = 6, landmarkEnd = -1, accidentBackup;
	float streetEventTimer = 10f, dialogueTimer = 5f, streetMessageTime, blackoutTime, tanodWatchTime;
	float chaserTime, chaserSpeed, inputDistractTime;
	Node3D chaser;
	bool chaserIsDog;
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

		// snack counter, "press E" prompt and pop-up messages
		snackLabel = new Label();
		snackLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
		snackLabel.OffsetLeft = 20;
		snackLabel.OffsetTop = -56;
		snackLabel.OffsetRight = 360;
		snackLabel.OffsetBottom = -16;
		snackLabel.AddThemeFontSizeOverride("font_size", 26);
		layer.AddChild(snackLabel);

		talkPrompt = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
		talkPrompt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		talkPrompt.OffsetTop = -110;
		talkPrompt.OffsetBottom = -70;
		talkPrompt.AddThemeFontSizeOverride("font_size", 30);
		talkPrompt.AddThemeColorOverride("font_color", new Color("#fff176"));
		talkPrompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		talkPrompt.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(talkPrompt);

		toastLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0) };
		toastLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		toastLabel.OffsetTop = -190;
		toastLabel.OffsetBottom = -140;
		toastLabel.AddThemeFontSizeOverride("font_size", 34);
		toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		toastLabel.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(toastLabel);

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

		blackout = new ColorRect
		{
			Color = new Color(0, 0, 0, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		blackout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(blackout);

		streetEventLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false
		};
		streetEventLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		streetEventLabel.OffsetTop = 160;
		streetEventLabel.OffsetBottom = 220;
		streetEventLabel.AddThemeFontSizeOverride("font_size", 32);
		streetEventLabel.AddThemeColorOverride("font_color", new Color("#ffe082"));
		streetEventLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		streetEventLabel.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(streetEventLabel);

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

		var hint = new Label { Text = "or press R / Space   -   C to change character", HorizontalAlignment = HorizontalAlignment.Center };
		hint.AddThemeFontSizeOverride("font_size", 18);
		hint.Modulate = new Color(1, 1, 1, 0.6f);
		box.AddChild(hint);
	}

	void UpdateHud()
	{
		scoreLabel.Text = $"Score: {maxRow}    Best: {best}";
		snackLabel.Text = $"{City.Current.ItemName}: {snacks}";
	}

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
			bool clear = type == LaneType.Grass && row >= 4 && rng.NextDouble() < 0.10;               // some sidewalks are the taho man's street

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
            if (type == LaneType.Grass && prev?.Type == LaneType.River)
            	lane.FaceWavingPeopleAwayFromWater(waterAhead: false);
            if (type == LaneType.River && prev?.Type == LaneType.Grass)
            	prev.FaceWavingPeopleAwayFromWater(waterAhead: true);
            lanes[row] = lane;
            nextRow++;

            if (type == LaneType.Road)
            {
            	roadRunLength++;
            }
            else
            {
            	if (roadRunLength == 4)
            		for (int crossingRow = row - roadRunLength; crossingRow < row; crossingRow++)
            			if (lanes.TryGetValue(crossingRow, out var crossingLane))
            				crossingLane.MarkPedestrianCrossing();
            	roadRunLength = 0;
            }

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
				ped.GetPlayer = () => player;
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
		if (k.Keycode == Key.C) { GetTree().ChangeSceneToFile("res://Select.tscn"); return; } // back to character select

		if (!player.Alive)
		{
			if (k.Keycode is Key.Space or Key.Enter) Restart();
			return;
		}
		if (inputDistractTime > 0f) return;

		switch (k.Keycode)
		{
			case Key.W: case Key.Up:    TryMove(0, 1); break;
			case Key.S: case Key.Down:  TryMove(0, -1); break;
			case Key.A: case Key.Left:  TryMove(-1, 0); break;
			case Key.D: case Key.Right: TryMove(1, 0); break;
			case Key.E: TryTalk(); break;
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
        foreach (var walker in pedestrians)                    // pedestrians are solid too
            if (walker.BlocksCell(newRow, newX)) return;

		if (lane.Type == LaneType.Road)
		{
			if (tanodWatchTime > 0f && lane.HasPedestrianCrossing &&
				Math.Abs(newX - lane.CrosswalkX) > 1.5f)
			{
				tanodWatchTime = 0f;
				SpawnChaser(dog: false);
				ShowStreetMessage("Barangay Tanod! Hoy, bawal tumawid diyan!", 2.5f);
			}
			else if (!lane.IsClear(newX, 0f, 0.35f))
			{
				ShowStreetMessage("Walang preno!", 1.8f);
			}
		}

        float yaw = dr > 0 ? 0 : dr < 0 ? Mathf.Pi : dx < 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2;
        player.StartHop(new Vector3(newX, 0, -newRow), yaw, newRow);

		if (lane.Type == LaneType.Grass && lane.HasPersonNear(newX, 1.6f))
			ShowStreetMessage("Tabi-tabi po!", 1.6f);

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
            if (player.Alive) UpdateStreetEvents(dt);
            if (player.Alive) UpdateChaser(dt);

            if (player.Alive) idle += dt;
            if (player.Alive && idle > EagleWarnTime && !eagle.Active)
                eagle.Swoop(player.Position + new Vector3(8, 10, -12), () => player.Position + new Vector3(0, 0.5f, 0));
        }

        if (eagle.Carrying) player.Position = eagle.Position + new Vector3(0, -0.7f, 0);
        warnLabel.Visible = player.Alive && eagle.Swooping;
        UpdateTalkTarget();
        UpdateStreetVisuals(dt);
        UpdateEventNpcs(dt);

        // smooth camera follow
        var focus = new Vector3(player.Position.X, 0, player.Position.Z - 2f);
        camFocus = camFocus.Lerp(focus, 1f - Mathf.Exp(-3.5f * dt));
        PlaceCamera();
    }

    void UpdateStreetEvents(float dt)
    {
        streetEventTimer -= dt;
        if (streetEventTimer <= 0f)
        {
        	TriggerStreetEvent(rng.Next(7));
        	streetEventTimer = 18f + (float)rng.NextDouble() * 14f;
        }

        if (tanodWatchTime > 0f) tanodWatchTime -= dt;
        if (inputDistractTime > 0f) inputDistractTime -= dt;

        dialogueTimer -= dt;
        if (dialogueTimer <= 0f)
        {
        	dialogueTimer = 6f + (float)rng.NextDouble() * 5f;
        	foreach (int row in new[] { player.Row - 1, player.Row + 1 })
        	{
        		if (lanes.TryGetValue(row, out var lane) && lane.Type == LaneType.Road &&
        			lane.NearbyVehicleKind(player.Position.X, 3f) == "jeepney")
        		{
        			ShowStreetMessage("Libre sakay!", 1.8f);
        			break;
        		}
        	}
        }
    }

    void TriggerStreetEvent(int eventId)
    {
        switch (eventId)
        {
        	case 0:
        		ShowStreetMessage("HOY! TABI!", 2.5f);
        		SpawnEventNpcs(1, new[] { "HOY! TABI!" }, 4f);
        		break;
        	case 1:
        		ShowStreetMessage("BROWNOUT!", 2.2f);
        		blackoutTime = 2.2f;
        		break;
        	case 2:
        		if (StartChismis()) ShowStreetMessage("CHISMIS ALERT!", 2.5f);
        		break;
        	case 3:
        		ShowStreetMessage("VIDEOKE NIGHT!", 2.8f);
        		SpawnEventNpcs(1, new[] { "La-la-la!" }, 5f);
        		inputDistractTime = 0.35f;
        		break;
        	case 4:
        		ShowStreetMessage("BARANGAY TANOD: Use the marked crossing!", 3f);
        		tanodWatchTime = 12f;
        		break;
        	case 5:
        		ShowStreetMessage("ASONG KALYE!", 2.5f);
        		SpawnChaser(dog: true);
        		break;
        	default:
        		bool reversed = false;
        		foreach (var lane in lanes.Values
        			.Where(l => l.Type == LaneType.Road)
        			.OrderBy(l => Math.Abs(l.Row - player.Row)))
        		{
        			if (lane.ReverseTricycle()) { reversed = true; break; }
        		}
        		ShowStreetMessage(reversed ? "TRICYCLE SPECIAL! Walang preno!" : "TRICYCLE SPECIAL!", 2.5f);
        		break;
        }
    }

    void ShowStreetMessage(string text, float duration)
    {
        streetEventLabel.Text = text;
        streetEventLabel.Visible = true;
        streetMessageTime = duration;
    }

    void UpdateStreetVisuals(float dt)
    {
        if (streetMessageTime > 0f)
        {
        	streetMessageTime -= dt;
        	streetEventLabel.Visible = streetMessageTime > 0f;
        }

        if (blackoutTime > 0f)
        {
        	blackoutTime -= dt;
        	float alpha = Mathf.Clamp(blackoutTime / 1.4f, 0f, 0.88f);
        	blackout.Color = new Color(0, 0, 0, alpha);
        }
        else
        {
        	blackout.Color = new Color(0, 0, 0, 0);
        }
    }

    bool SpawnEventNpcs(int count, string[] dialogue, float lifetime)
    {
        foreach (var lane in lanes.Values
        	.Where(l => l.Type == LaneType.Grass && l.Row >= maxRow && l.Row <= maxRow + 10)
        	.OrderBy(l => Math.Abs(l.Row - (maxRow + 3))))
        {
        	for (int x = -7; x <= 7 - count + 1; x++)
        	{
        		bool available = true;
        		for (int i = 0; i < count; i++)
        		{
        			if (lane.Blocked.Contains(x + i) ||
        				tahoMen.Any(t => t.Row == lane.Row && Math.Abs(t.Position.X - (x + i)) < 1.5f))
        			{
        				available = false;
        				break;
        			}
        		}
        		if (!available) continue;

        		var cells = new int[count];
        		for (int i = 0; i < count; i++)
        		{
        			cells[i] = x + i;
        			lane.Blocked.Add(cells[i]);
        			var npc = BuildEventNpc(dialogue[i % dialogue.Length], i == 0
        				? new Color("#e67e22")
        				: new Color("#2196f3"));
        			npc.Position = new Vector3(x + i, 0, -lane.Row);
        			npc.RotationDegrees = new Vector3(0, i == 0 ? 35 : -35, 0);
        			AddChild(npc);
        			eventNpcs.Add(new EventNpc { Node = npc, Lane = lane, Cells = new[] { x + i }, TimeLeft = lifetime });
        		}
        		return true;
        	}
        }
        return false;
    }

	bool StartChismis()
	{
		var candidates = pedestrians.Where(p => p.CanGossip && Math.Abs(p.Row - player.Row) <= 8).ToList();
		Pedestrian first = null, second = null;
		float closest = 5.5f;
		foreach (var a in candidates)
		{
			foreach (var b in candidates)
			{
				if (a == b || a.Row != b.Row) continue;
				float distance = Math.Abs(a.WorldX - b.WorldX);
				if (distance < 1.5f || distance >= closest) continue;
				first = a;
				second = b;
				closest = distance;
			}
		}
		if (first == null || second == null) return false;

		float middle = (first.WorldX + second.WorldX) * 0.5f;
		first.StartGossip(middle - 0.3f, "Saan ka pupunta?", 5.5f);
		second.StartGossip(middle + 0.3f, "Pre, ingat!", 5.5f);
		return true;
	}

    static Node3D BuildEventNpc(string dialogue, Color shirt)
    {
        var npc = new Node3D();
        npc.AddChild(Util.Box(new Vector3(0.42f, 0.48f, 0.28f), shirt, new Vector3(0, 0.62f, 0)));
        npc.AddChild(Util.Box(new Vector3(0.28f, 0.28f, 0.28f), new Color("#c68642"), new Vector3(0, 1.02f, 0)));
        npc.AddChild(Util.Box(new Vector3(0.34f, 0.09f, 0.34f), new Color("#2b1d14"), new Vector3(0, 1.2f, 0)));
        npc.AddChild(Util.Box(new Vector3(0.14f, 0.34f, 0.16f), new Color("#34495e"), new Vector3(-0.11f, 0.19f, 0)));
        npc.AddChild(Util.Box(new Vector3(0.14f, 0.34f, 0.16f), new Color("#34495e"), new Vector3(0.11f, 0.19f, 0)));
        npc.AddChild(new Label3D
        {
        	Text = dialogue,
        	FontSize = 24,
        	PixelSize = 0.008f,
        	OutlineSize = 5,
        	OutlineModulate = Colors.Black,
        	Modulate = Colors.White,
        	Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        	Position = new Vector3(0, 1.65f, 0)
        });
        return npc;
    }

    void UpdateEventNpcs(float dt)
    {
        for (int i = eventNpcs.Count - 1; i >= 0; i--)
        {
        	var npc = eventNpcs[i];
        	npc.TimeLeft -= dt;
        	if (npc.TimeLeft > 0f) continue;
        	if (GodotObject.IsInstanceValid(npc.Node)) npc.Node.QueueFree();
        	if (GodotObject.IsInstanceValid(npc.Lane))
        		foreach (int cell in npc.Cells) npc.Lane.Blocked.Remove(cell);
        	eventNpcs.RemoveAt(i);
        }
    }

    void SpawnChaser(bool dog)
    {
        if (chaser != null) return;
        chaserIsDog = dog;
        chaserSpeed = dog ? 2.7f : 3.4f;
        chaserTime = dog ? 9f : 8f;
        chaser = BuildChaser(dog);
        chaser.Position = player.Position + (dog ? new Vector3(-5f, 0, 1f) : new Vector3(5f, 0, 3f));
        AddChild(chaser);
    }

    static Node3D BuildChaser(bool dog)
    {
        var actor = new Node3D();
        if (dog)
        {
        	Color fur = new("#9b5b2b");
        	actor.AddChild(Util.Box(new Vector3(0.65f, 0.38f, 0.34f), fur, new Vector3(0, 0.48f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.28f, 0.3f, 0.3f), fur, new Vector3(0.38f, 0.58f, -0.02f)));
        	actor.AddChild(Util.Box(new Vector3(0.2f, 0.12f, 0.2f), new Color("#d7a26e"), new Vector3(0.57f, 0.48f, -0.08f)));
        	foreach (float x in new[] { -0.22f, 0.22f })
        		foreach (float z in new[] { -0.11f, 0.11f })
        			actor.AddChild(Util.Box(new Vector3(0.12f, 0.28f, 0.12f), fur, new Vector3(x, 0.18f, z)));
        	actor.AddChild(Util.Box(new Vector3(0.1f, 0.35f, 0.1f), fur, new Vector3(-0.36f, 0.65f, 0.02f)));
        }
        else
        {
        	actor.AddChild(Util.Box(new Vector3(0.46f, 0.55f, 0.3f), new Color("#315b36"), new Vector3(0, 0.66f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.29f, 0.3f, 0.29f), new Color("#c68642"), new Vector3(0, 1.1f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.38f, 0.08f, 0.38f), new Color("#1c2b1f"), new Vector3(0, 1.29f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.12f, 0.38f, 0.12f), new Color("#315b36"), new Vector3(0.29f, 0.68f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.08f, 0.24f, 0.08f), new Color("#8d6e63"), new Vector3(0.36f, 0.46f, -0.12f)));
        	actor.AddChild(Util.Box(new Vector3(0.12f, 0.38f, 0.14f), new Color("#263238"), new Vector3(-0.12f, 0.2f, 0)));
        	actor.AddChild(Util.Box(new Vector3(0.12f, 0.38f, 0.14f), new Color("#263238"), new Vector3(0.12f, 0.2f, 0)));
        }
        return actor;
    }

    void UpdateChaser(float dt)
    {
        if (chaser == null) return;
        chaserTime -= dt;
        var direction = player.Position - chaser.Position;
        direction.Y = 0;
        float distance = direction.Length();
        if (distance < 0.8f)
        {
        	Die(chaserIsDog ? "The stray dog caught you!" : "The Barangay Tanod caught you!", squash: true);
        	return;
        }
        if (chaserTime <= 0f)
        {
        	chaser.QueueFree();
        	chaser = null;
        	return;
        }
        chaser.Position += direction / distance * chaserSpeed * dt;
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

        deathTitle.Text = $"{title}\nNaku po!";
        deathScore.Text = $"{City.Current.Name}\nScore: {maxRow}    Best: {best}";
        deathBox.Visible = true;
    }

    // ---------- talking to the vendor ----------

    void UpdateTalkTarget()
    {
        talkTarget = null;
        if (player.Alive)
        {
            float closest = TahoMan.TalkRange;
            foreach (var vendor in tahoMen)
            {
                float d = vendor.DistanceTo(player);
                if (d < closest) { closest = d; talkTarget = vendor; }
            }
        }
        talkPrompt.Visible = talkTarget != null;
        if (talkTarget != null) talkPrompt.Text = $"Press E to talk to the {City.Current.VendorTitle}";
    }

    void TryTalk()
    {
        if (talkTarget == null) return;

        if (talkTarget.Talk())
        {
            snacks++;
            idle = 0;                                   // a snack break resets the eagle timer
            player.HoldItem(City.Current.Vendor);
            UpdateHud();
            ShowToast(City.Current.GiftText);
        }
        else
        {
            ShowToast("He's sold out!");
        }
    }

    void ShowToast(string text)
    {
        toastLabel.Text = text;
        toastLabel.Modulate = Colors.White;
        toastTween?.Kill();
        toastTween = CreateTween();
        toastTween.TweenInterval(1.6);
        toastTween.TweenProperty(toastLabel, "modulate:a", 0f, 0.6f);
    }

    void PlaceCamera()
    {
        camera.Position = camFocus + new Vector3(-7, 11, 7);
        camera.LookAt(camFocus, Vector3.Up);
    }
}
