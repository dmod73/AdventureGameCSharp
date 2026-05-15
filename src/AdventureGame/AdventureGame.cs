using System.Text;

namespace AdventureGame;

public class AdventureGame
{
	public readonly string GO_NORTH = "W";
	public readonly string GO_SOUTH = "S";
	public readonly string GO_EAST = "D";
	public readonly string GO_WEST = "A";
	public readonly string GET_LAMP = "L";
	public readonly string GET_KEY = "K";
	public readonly string OPEN_CHEST = "O";
	public readonly string QUIT = "Q";

	private Adventurer adventurer;
	private Room[,] dungeon;
	private int aRow;
	private int aCol;
	private int gRow;
	private int gCol;
	private int exitRow;
	private int exitCol;
	private bool isChestOpen;
	private bool hasPlayerQuit;
	private bool isAdventureAlive;
	private bool hasWon;
	private string lastDirection;
	private readonly List<string> messages;

	public AdventureGame()
	{
		adventurer = new Adventurer();
		dungeon = new Room[0, 0];
		lastDirection = string.Empty;
		messages = new List<string>();
	}

	public void Start()
	{
		Init();

		ShowGameStartScreen();

		string input;

		do
		{
			ShowScene();

			do
			{
				ShowInputOptions();
				input = GetInput();
			}
			while(!IsValidInput(input));

			ProcessInput(input);
			UpdateGameState();
		}
		while(!IsGameOver());

		ShowGameOverScreen();
	}

	private void Init()
	{
		adventurer = new Adventurer();
		LoadDungeonFromFile(Path.Combine(AppContext.BaseDirectory, "dungeons", "dungeon1.txt"));
		isChestOpen = false;
		hasPlayerQuit = false;
		isAdventureAlive = true;
		hasWon = false;
		lastDirection = string.Empty;
		messages.Clear();
	}

	private void LoadDungeonFromFile(string path)
	{
		if(!File.Exists(path))
		{
			throw new FileNotFoundException($"Dungeon file not found: {path}");
		}

		var lines = File.ReadAllLines(path)
			.Select(l => l.Trim())
			.Where(l => !string.IsNullOrWhiteSpace(l))
			.ToList();

		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var mapLines = new List<string>();
		bool readingMap = false;

		foreach(var line in lines)
		{
			if(line.Equals("MAP:", StringComparison.OrdinalIgnoreCase))
			{
				readingMap = true;
				continue;
			}

			if(readingMap)
			{
				mapLines.Add(line);
				continue;
			}

			int idx = line.IndexOf('=');
			if(idx <= 0)
			{
				throw new InvalidDataException($"Invalid dungeon line: {line}");
			}

			string key = line[..idx].Trim();
			string val = line[(idx + 1)..].Trim();
			values[key] = val;
		}

		int rows = ParseInt(values, "ROWS");
		int cols = ParseInt(values, "COLS");

		if(mapLines.Count != rows || mapLines.Any(m => m.Length != cols))
		{
			throw new InvalidDataException("MAP dimensions do not match ROWS/COLS.");
		}

		dungeon = new Room[rows, cols];

		for(int r = 0; r < rows; r++)
		{
			for(int c = 0; c < cols; c++)
			{
				if(mapLines[r][c] == '#')
				{
					continue;
				}

				var room = new Room();
				room.SetDescription($"Room ({r},{c})");
				dungeon[r, c] = room;
			}
		}

		for(int r = 0; r < rows; r++)
		{
			for(int c = 0; c < cols; c++)
			{
				if(dungeon[r, c] == null)
				{
					continue;
				}

				dungeon[r, c].SetNorth(IsWalkable(r - 1, c));
				dungeon[r, c].SetSouth(IsWalkable(r + 1, c));
				dungeon[r, c].SetEast(IsWalkable(r, c + 1));
				dungeon[r, c].SetWest(IsWalkable(r, c - 1));
			}
		}

		(exitRow, exitCol) = ParseCoord(values, "EXIT");
		(aRow, aCol) = ParseCoord(values, "ADVENTURER");
		(gRow, gCol) = ParseCoord(values, "GRUE");
		var (lampRow, lampCol) = ParseCoord(values, "LAMP");
		var (keyRow, keyCol) = ParseCoord(values, "KEY");
		var (chestRow, chestCol) = ParseCoord(values, "CHEST");

		EnsureWalkable(exitRow, exitCol, "EXIT");
		EnsureWalkable(aRow, aCol, "ADVENTURER");
		EnsureWalkable(gRow, gCol, "GRUE");
		EnsureWalkable(lampRow, lampCol, "LAMP");
		EnsureWalkable(keyRow, keyCol, "KEY");
		EnsureWalkable(chestRow, chestCol, "CHEST");

		dungeon[lampRow, lampCol].SetLamp(true);
		dungeon[keyRow, keyCol].SetKey(true);
		dungeon[chestRow, chestCol].SetChest(true);

		if(values.TryGetValue("LIT", out string? litList))
		{
			foreach(var token in litList.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var (lr, lc) = ParseCoordToken(token);
				EnsureWalkable(lr, lc, "LIT");
				dungeon[lr, lc].SetLit(true);
			}
		}
	}

	private int ParseInt(Dictionary<string, string> values, string key)
	{
		if(!values.TryGetValue(key, out string? val) || !int.TryParse(val, out int result))
		{
			throw new InvalidDataException($"Missing or invalid integer value for {key}.");
		}
		return result;
	}

	private (int row, int col) ParseCoord(Dictionary<string, string> values, string key)
	{
		if(!values.TryGetValue(key, out string? val))
		{
			throw new InvalidDataException($"Missing coordinate for {key}.");
		}
		return ParseCoordToken(val);
	}

	private (int row, int col) ParseCoordToken(string token)
	{
		var parts = token.Split(',', StringSplitOptions.TrimEntries);
		if(parts.Length != 2 || !int.TryParse(parts[0], out int row) || !int.TryParse(parts[1], out int col))
		{
			throw new InvalidDataException($"Invalid coordinate token: {token}");
		}
		return (row, col);
	}

	private bool IsWalkable(int row, int col)
	{
		return row >= 0
			&& col >= 0
			&& row < dungeon.GetLength(0)
			&& col < dungeon.GetLength(1)
			&& dungeon[row, col] != null;
	}

	private void EnsureWalkable(int row, int col, string label)
	{
		if(!IsWalkable(row, col))
		{
			throw new InvalidDataException($"{label} coordinate ({row},{col}) is not walkable.");
		}
	}

	private void ShowGameStartScreen()
	{
		Console.WriteLine("Welcome to Adventure Game!");
		Console.WriteLine("Open the chest, then escape through the dungeon exit before the Grue catches you.");
	}

	private void ShowScene()
	{
		Console.Clear();
		var r = dungeon[aRow, aCol];
		ShowDungeonMap();

		if(adventurer.HasLamp() || r.IsLit())
		{
			Console.WriteLine(r.GetDescription());
		}
		else
		{
			Console.WriteLine("This room is pitch black!");
		}

		if(aRow == exitRow && aCol == exitCol)
		{
			AddMessage("You see the dungeon exit here.");
		}

		foreach(var message in messages.TakeLast(6))
		{
			Console.WriteLine(message);
		}
	}

	private void ShowDungeonMap()
	{
		Console.WriteLine("Dungeon map:");
		Console.WriteLine("A=Adventurer G=Grue E=Exit C=Chest K=Key L=Lamp");

		for(int r = 0; r < dungeon.GetLength(0); r++)
		{
			var top = new StringBuilder();
			var mid = new StringBuilder();
			var bottom = new StringBuilder();

			for(int c = 0; c < dungeon.GetLength(1); c++)
			{
				var room = dungeon[r, c];
				if(room == null)
				{
					top.Append("     ");
					mid.Append("     ");
					bottom.Append("     ");
					continue;
				}

				top.Append("┌─┐");
				mid.Append($"│{GetRoomSymbol(r, c)}│");
				bottom.Append("└─┘");

				top.Append(room.HasEast() ? "──" : "  ");
				mid.Append(room.HasEast() ? "  " : "  ");
				bottom.Append(room.HasEast() ? "  " : "  ");
			}

			Console.WriteLine(top.ToString().TrimEnd());
			Console.WriteLine(mid.ToString().TrimEnd());
			Console.WriteLine(bottom.ToString().TrimEnd());

			if(r < dungeon.GetLength(0) - 1)
			{
				var connectors = new StringBuilder();
				for(int c = 0; c < dungeon.GetLength(1); c++)
				{
					var room = dungeon[r, c];
					if(room == null)
					{
						connectors.Append("     ");
						continue;
					}

					connectors.Append(room.HasSouth() ? " │   " : "     ");
				}
				Console.WriteLine(connectors.ToString().TrimEnd());
			}
		}
	}

	private char GetRoomSymbol(int row, int col)
	{
		var room = dungeon[row, col];

		if(row == aRow && col == aCol) return 'A';
		if(row == gRow && col == gCol) return 'G';
		if(room.HasChest()) return 'C';
		if(room.HasKey()) return 'K';
		if(room.HasLamp()) return 'L';
		if(row == exitRow && col == exitCol) return 'E';

		return ' ';
	}

	private void ShowInputOptions()
	{
		string options = ""
		+ $"GO NORTH [{GO_NORTH}] | GO EAST [{GO_EAST}] | GET LAMP [{GET_LAMP}] | OPEN CHEST [{OPEN_CHEST}]\n"
		+ $"GO SOUTH [{GO_SOUTH}] | GO WEST [{GO_WEST}] | GET KEY  [{GET_KEY}] | QUIT       [{QUIT}]\n"
		+ $"> ";

		Console.Write(options);
	}

	private string GetInput()
	{
		return Console.ReadLine()!.ToUpper();
	}

	private bool IsValidInput(string input)
	{
		string[] validInputs = { GO_NORTH, GO_SOUTH, GO_EAST, GO_WEST, GET_LAMP, GET_KEY, OPEN_CHEST, QUIT };

		if(!validInputs.Contains(input))
		{
			AddMessage("ERROR: Invalid input. Please try again.");
			return false;
		}

		return true;
	}

	private void ProcessInput(string input)
	{
		Room r = dungeon[aRow, aCol];

		if(!adventurer.HasLamp() && !r.IsLit() && input != lastDirection)
		{
			AddMessage("You got eaten alive by the Grue in the darkness!");
			isAdventureAlive = false;
			return;
		}

		if(input == GO_NORTH)
		{
			GoNorth(r);
		}
		else if(input == GO_SOUTH)
		{
			GoSouth(r);
		}
		else if(input == GO_EAST)
		{
			GoEast(r);
		}
		else if(input == GO_WEST)
		{
			GoWest(r);
		}
		else if(input == GET_LAMP)
		{
			GetLamp(r);
		}
		else if(input == GET_KEY)
		{
			GetKey(r);
		}
		else if(input == OPEN_CHEST)
		{
			OpenChest(r);
		}
		else
		{
			Quit();
		}
	}

	private void UpdateGameState()
	{
		if(!isAdventureAlive || hasPlayerQuit)
		{
			return;
		}

		if(gRow == aRow && gCol == aCol)
		{
			AddMessage("The Grue caught you!");
			isAdventureAlive = false;
			return;
		}

		if(isChestOpen)
		{
			MoveGrueTowardsAdventurer();
			if(gRow == aRow && gCol == aCol)
			{
				AddMessage("The Grue caught you!");
				isAdventureAlive = false;
				return;
			}

			if(aRow == exitRow && aCol == exitCol)
			{
				hasWon = true;
			}
		}
	}

	private List<(int row, int col)> FindShortestPathDijkstra((int row, int col) start, (int row, int col) target)
	{
		var dist = new Dictionary<(int row, int col), int>();
		var prev = new Dictionary<(int row, int col), (int row, int col)>();
		var unvisited = new HashSet<(int row, int col)>();

		for(int r = 0; r < dungeon.GetLength(0); r++)
		{
			for(int c = 0; c < dungeon.GetLength(1); c++)
			{
				if(dungeon[r, c] != null)
				{
					var node = (r, c);
					dist[node] = int.MaxValue;
					unvisited.Add(node);
				}
			}
		}

		dist[start] = 0;

		while(unvisited.Count > 0)
		{
			var current = unvisited.OrderBy(n => dist[n]).First();
			if(dist[current] == int.MaxValue)
			{
				break;
			}
			if(current == target)
			{
				break;
			}

			unvisited.Remove(current);

			foreach(var neighbor in GetNeighbors(current.row, current.col))
			{
				if(!unvisited.Contains(neighbor))
				{
					continue;
				}

				int alt = dist[current] + 1;
				if(alt < dist[neighbor])
				{
					dist[neighbor] = alt;
					prev[neighbor] = current;
				}
			}
		}

		if(start == target)
		{
			return new List<(int row, int col)> { start };
		}

		if(!prev.ContainsKey(target))
		{
			return new List<(int row, int col)>();
		}

		var path = new List<(int row, int col)>();
		var cursor = target;
		path.Add(cursor);

		while(cursor != start)
		{
			cursor = prev[cursor];
			path.Add(cursor);
		}

		path.Reverse();
		return path;
	}

	private IEnumerable<(int row, int col)> GetNeighbors(int row, int col)
	{
		var r = dungeon[row, col];
		if(r.HasNorth()) yield return (row - 1, col);
		if(r.HasSouth()) yield return (row + 1, col);
		if(r.HasEast()) yield return (row, col + 1);
		if(r.HasWest()) yield return (row, col - 1);
	}

	private void MoveGrueTowardsAdventurer()
	{
		var path = FindShortestPathDijkstra((gRow, gCol), (aRow, aCol));
		if(path.Count <= 1)
		{
			AddMessage("The Grue waits, unable to find a path.");
			return;
		}

		var coordinates = path.Select(p => $"({p.row},{p.col})");
		AddMessage($"Grue path (Dijkstra): {string.Join(" -> ", coordinates)}");

		var next = path[1];
		AddMessage($"The Grue moves {DirectionBetween(path[0], next)} to ({next.row},{next.col}).");
		gRow = next.row;
		gCol = next.col;
	}

	private string DirectionBetween((int row, int col) from, (int row, int col) to)
	{
		if(to.row == from.row - 1) return "NORTH";
		if(to.row == from.row + 1) return "SOUTH";
		if(to.col == from.col + 1) return "EAST";
		if(to.col == from.col - 1) return "WEST";
		return "UNKNOWN";
	}

	private bool IsGameOver()
	{
		return hasWon || hasPlayerQuit || !isAdventureAlive;
	}

	private void ShowGameOverScreen()
	{
		if(hasWon)
		{
			Console.WriteLine("You escaped the dungeon with the treasure. You win!");
		}
		else if(!isAdventureAlive)
		{
			Console.WriteLine("Game Over! You were defeated.");
		}
		else
		{
			Console.WriteLine("Game Over! You quit the game.");
		}
	}

	private void GoNorth(Room r)
	{
		if(r.HasNorth())
		{
			aRow -= 1;
			lastDirection = GO_SOUTH;
		}
		else
		{
			AddMessage("You cannot go north!\a");
		}
	}

	private void GoSouth(Room r)
	{
		if(r.HasSouth())
		{
			aRow += 1;
			lastDirection = GO_NORTH;
		}
		else
		{
			AddMessage("You cannot go south!\a");
		}
	}

	private void GoEast(Room r)
	{
		if(r.HasEast())
		{
			aCol += 1;
			lastDirection = GO_WEST;
		}
		else
		{
			AddMessage("You cannot go east!\a");
		}
	}

	private void GoWest(Room r)
	{
		if(r.HasWest())
		{
			aCol -= 1;
			lastDirection = GO_EAST;
		}
		else
		{
			AddMessage("You cannot go west!\a");
		}
	}

	private void GetLamp(Room r)
	{
		if(r.HasLamp())
		{
			AddMessage("You got the lamp!");
			adventurer.SetLamp(true);
			r.SetLamp(false);
		}
		else
		{
			AddMessage("There is no lamp in this room.");
		}
	}

	private void GetKey(Room r)
	{
		if(r.HasKey())
		{
			AddMessage("You got the key!");
			adventurer.SetKey(true);
			r.SetKey(false);
		}
		else
		{
			AddMessage("There is no key in this room.");
		}
	}

	private void OpenChest(Room r)
	{
		if(r.HasChest())
		{
			if(adventurer.HasKey())
			{
				AddMessage("You opened the chest and took the treasure. Now find the exit!");
				isChestOpen = true;
			}
			else
			{
				AddMessage("You do not have the key!");
			}
		}
		else
		{
			AddMessage("There is no chest in this room.");
		}
	}

	private void Quit()
	{
		AddMessage("You quit the game!");
		hasPlayerQuit = true;
	}

	private void AddMessage(string message)
	{
		messages.Add(message);
	}
}
