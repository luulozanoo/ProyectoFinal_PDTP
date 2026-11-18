using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generador 2D de mazmorras con 25 habitaciones distribuidas orgánicamente,
/// temáticas visuales, suelos con diseño procedural, objetos temáticos y spawn de enemigos.
/// </summary>
public class RoomGenerator2D : MonoBehaviour
{
    public enum RoomShape
    {
        Rectangle,
        LShape,
        TShape,
        Cross,
        Rounded
    }

    public enum RoomTheme
    {
        Dungeon,
        Library,
        Alchemy,
        Treasure,
        Garden,
        Armory
    }

    private enum TileType : byte
    {
        Empty = 0,
        Room = 1,
        Corridor = 2
    }

    private struct ExitPoint
    {
        public Vector2Int roomTile;
        public Vector2Int outsideTile;
        public Vector2Int direction;
    }

    private class RoomData
    {
        public int id;
        public Vector2Int centerTile;
        public RectInt bounds;
        public RoomShape shape;
        public RoomTheme theme;
        public bool isStartRoom;
        public bool isExitRoom;
        public List<Vector2Int> floorTiles = new List<Vector2Int>();
        public List<RectInt> visualRects = new List<RectInt>();
        public List<Vector2Int> usedExitTiles = new List<Vector2Int>();
    }

    [Header("Generación")]
    [Min(2)] public int roomCount = 25;
    [Min(40)] public int areaWidth = 120;
    [Min(30)] public int areaHeight = 70;
    [Min(3)] public int minRoomGap = 4;
    [Min(0.5f)] public float tileSize = 1f;
    [Range(0f, 1f)] public float extraCorridorChance = 0.2f;
    public bool randomSeed = true;
    public int seed = 12345;
    public bool generateOnStart = true;

    [Header("Habitaciones y Pasillos")]
    [Min(4)] public int minRoomSize = 6;
    [Min(6)] public int maxRoomSize = 11;
    [Min(0.1f)] public float wallThickness = 0.28f;

    [Header("Decoración")]
    [Min(0)] public int minObjectsPerRoom = 3;
    [Min(0)] public int maxObjectsPerRoom = 7;
    [Min(0.1f)] public float furnitureScale = 1f;
    public GameObject[] objectPrefabs; // Opcional: si se deja vacío usa los sprites temáticos procedurales

    [Header("Enemigos")]
    public GameObject enemyPrefab;
    [Min(0)] public int minEnemiesPerRoom = 1;
    [Min(0)] public int maxEnemiesPerRoom = 2;

    [Header("Cámara")]
    public bool fitMainCamera = true;
    [Min(1f)] public float cameraPadding = 1.08f;

    private const string RootName = "GeneratedRooms2D";
    private const int MapMargin = 2;

    private TileType[,] map;
    private int mapWidth;
    private int mapHeight;
    private List<RoomData> rooms = new List<RoomData>();

    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
    };

    // Caché de sprites procedurales
    private bool spritesReady;
    private Dictionary<RoomTheme, Sprite> floorSprites;
    private Sprite corridorSprite;
    private Sprite wallSprite;
    private Sprite shadowSprite;
    private Sprite glowSprite;
    private Dictionary<string, Sprite> propSprites;

    private void Start()
    {
        EnsureSprites();
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generar habitaciones 2D")]
    public void Generate()
    {
        EnsureSprites();
        Clear();

        if (randomSeed)
            seed = System.Environment.TickCount ^ Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(seed);

        int safeMinSize = Mathf.Max(4, minRoomSize);
        int safeMaxSize = Mathf.Max(safeMinSize, maxRoomSize);

        mapWidth = areaWidth + MapMargin * 2;
        mapHeight = areaHeight + MapMargin * 2;
        map = new TileType[mapWidth, mapHeight];
        rooms.Clear();

        // 1. Distribuir las 25 habitaciones de forma orgánica
        PlaceRoomsOrganically(safeMinSize, safeMaxSize);
        AssignSpecialRooms();

        Transform root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        // 2. Conectar con pasillos estrechos y puntos de salida únicos
        Transform corridorsParent = new GameObject("Corridors").transform;
        corridorsParent.SetParent(root, false);
        ConnectAllRoomsWithCorridors(corridorsParent);

        // 3. Construir suelos con diseño, detalles, objetos temáticos y enemigos
        Transform roomsParent = new GameObject("Rooms").transform;
        roomsParent.SetParent(root, false);
        foreach (RoomData room in rooms)
            BuildRoomVisualsAndFurniture(roomsParent, room);

        // 4. Construir paredes perimetrales texturizadas
        Transform wallsParent = new GameObject("Walls").transform;
        wallsParent.SetParent(root, false);
        BuildPerimeterWalls(wallsParent);

        // 5. Ajustar la cámara al escenario generado
        if (fitMainCamera) FitCameraToContent();

        Debug.Log($"RoomGenerator2D: {rooms.Count} habitaciones temáticas generadas (Seed: {seed}).");
    }

    [ContextMenu("Limpiar habitaciones 2D")]
    public void Clear()
    {
        Transform old = transform.Find(RootName);
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
    }

    private void PlaceRoomsOrganically(int minSize, int maxSize)
    {
        int currentGap = minRoomGap;
        int attempts = 0;
        int maxTotalAttempts = roomCount * 400;

        while (rooms.Count < roomCount && attempts < maxTotalAttempts)
        {
            attempts++;

            if (attempts % (roomCount * 100) == 0 && currentGap > 3)
                currentGap--;

            int w = Random.Range(minSize, maxSize + 1);
            int h = Random.Range(minSize, maxSize + 1);

            int x = Random.Range(MapMargin, mapWidth - MapMargin - w);
            int y = Random.Range(MapMargin, mapHeight - MapMargin - h);

            RectInt candidate = new RectInt(x, y, w, h);
            if (OverlapsAnyRoom(candidate, currentGap)) continue;

            rooms.Add(CreateRoomData(rooms.Count, candidate));
        }
    }

    private void AssignSpecialRooms()
    {
        if (rooms.Count == 0) return;

        rooms[0].isStartRoom = true;
        rooms[0].theme = RoomTheme.Dungeon;

        int furthestIdx = 0;
        float maxDistSqr = -1f;
        for (int i = 1; i < rooms.Count; i++)
        {
            float d = (rooms[i].centerTile - rooms[0].centerTile).sqrMagnitude;
            if (d > maxDistSqr)
            {
                maxDistSqr = d;
                furthestIdx = i;
            }
        }

        if (furthestIdx != 0)
        {
            rooms[furthestIdx].isExitRoom = true;
            rooms[furthestIdx].theme = RoomTheme.Treasure;
        }
    }

    private bool OverlapsAnyRoom(RectInt candidate, int gap)
    {
        RectInt expanded = new RectInt(
            candidate.xMin - gap,
            candidate.yMin - gap,
            candidate.width + gap * 2,
            candidate.height + gap * 2);

        foreach (RoomData other in rooms)
        {
            if (expanded.Overlaps(other.bounds))
                return true;
        }
        return false;
    }

    private RoomData CreateRoomData(int id, RectInt bounds)
    {
        int themeCount = System.Enum.GetValues(typeof(RoomTheme)).Length;
        RoomData room = new RoomData
        {
            id = id,
            bounds = bounds,
            centerTile = new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2),
            shape = (RoomShape)Random.Range(0, System.Enum.GetValues(typeof(RoomShape)).Length),
            theme = (RoomTheme)Random.Range(0, themeCount)
        };

        int w = bounds.width;
        int h = bounds.height;
        int cx = room.centerTile.x;
        int cy = room.centerTile.y;

        switch (room.shape)
        {
            case RoomShape.Rectangle:
                AddRoomRect(room, bounds);
                break;

            case RoomShape.Cross:
                {
                    int armW = Mathf.Max(3, w / 2);
                    int armH = Mathf.Max(3, h / 2);
                    AddRoomRect(room, new RectInt(bounds.xMin, cy - armH / 2, w, armH));
                    AddRoomRect(room, new RectInt(cx - armW / 2, bounds.yMin, armW, h));
                    break;
                }

            case RoomShape.LShape:
                {
                    int armW = Mathf.Max(3, w / 2);
                    int armH = Mathf.Max(3, h / 2);
                    int orientation = Random.Range(0, 4);

                    int x1 = (orientation == 0 || orientation == 3) ? cx - armW / 2 : cx - w + armW / 2;
                    int y1 = (orientation == 0 || orientation == 1) ? cy - armH / 2 : cy - h + armH / 2;

                    AddRoomRect(room, new RectInt(x1, cy - armH / 2, w, armH));
                    AddRoomRect(room, new RectInt(cx - armW / 2, y1, armW, h));
                    break;
                }

            case RoomShape.TShape:
                {
                    int barThickness = Mathf.Max(3, Mathf.Min(w, h) / 2);
                    int orientation = Random.Range(0, 4);

                    if (orientation == 0)
                    {
                        AddRoomRect(room, new RectInt(cx - w / 2, cy - barThickness / 2, w, barThickness));
                        AddRoomRect(room, new RectInt(cx - barThickness / 2, cy - h / 2, barThickness, h / 2 + barThickness / 2));
                    }
                    else if (orientation == 1)
                    {
                        AddRoomRect(room, new RectInt(cx - w / 2, cy - barThickness / 2, w, barThickness));
                        AddRoomRect(room, new RectInt(cx - barThickness / 2, cy - barThickness / 2, barThickness, h / 2 + barThickness / 2));
                    }
                    else if (orientation == 2)
                    {
                        AddRoomRect(room, new RectInt(cx - barThickness / 2, cy - h / 2, barThickness, h));
                        AddRoomRect(room, new RectInt(cx - barThickness / 2, cy - barThickness / 2, w / 2 + barThickness / 2, barThickness));
                    }
                    else
                    {
                        AddRoomRect(room, new RectInt(cx - barThickness / 2, cy - h / 2, barThickness, h));
                        AddRoomRect(room, new RectInt(cx - w / 2, cy - barThickness / 2, w / 2 + barThickness / 2, barThickness));
                    }
                    break;
                }

            case RoomShape.Rounded:
                {
                    AddRoomRect(room, new RectInt(bounds.xMin, bounds.yMin + 1, w, Mathf.Max(3, h - 2)));
                    AddRoomRect(room, new RectInt(bounds.xMin + 1, bounds.yMin, Mathf.Max(3, w - 2), h));
                    break;
                }
        }

        return room;
    }

    private void AddRoomRect(RoomData room, RectInt rect)
    {
        int xMin = Mathf.Clamp(rect.xMin, MapMargin, mapWidth - MapMargin - 2);
        int yMin = Mathf.Clamp(rect.yMin, MapMargin, mapHeight - MapMargin - 2);
        int xMax = Mathf.Clamp(rect.xMax, xMin + 2, mapWidth - MapMargin);
        int yMax = Mathf.Clamp(rect.yMax, yMin + 2, mapHeight - MapMargin);

        RectInt clamped = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        room.visualRects.Add(clamped);

        for (int x = clamped.xMin; x < clamped.xMax; x++)
        {
            for (int y = clamped.yMin; y < clamped.yMax; y++)
            {
                if (map[x, y] != TileType.Room)
                {
                    map[x, y] = TileType.Room;
                    room.floorTiles.Add(new Vector2Int(x, y));
                }
            }
        }
    }

    private void ConnectAllRoomsWithCorridors(Transform parent)
    {
        if (rooms.Count < 2) return;

        HashSet<int> connected = new HashSet<int> { 0 };
        HashSet<int> unconnected = new HashSet<int>();
        for (int i = 1; i < rooms.Count; i++) unconnected.Add(i);

        List<(int a, int b)> edges = new List<(int, int)>();

        while (unconnected.Count > 0)
        {
            int bestFrom = -1;
            int bestTo = -1;
            float bestDistSqr = float.MaxValue;

            foreach (int from in connected)
            {
                foreach (int to in unconnected)
                {
                    float distSqr = (rooms[from].centerTile - rooms[to].centerTile).sqrMagnitude;
                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestFrom = from;
                        bestTo = to;
                    }
                }
            }

            edges.Add((bestFrom, bestTo));
            connected.Add(bestTo);
            unconnected.Remove(bestTo);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (Random.value > extraCorridorChance) continue;

            int closestOther = -1;
            float bestDistSqr = float.MaxValue;

            for (int j = 0; j < rooms.Count; j++)
            {
                if (i == j || EdgeExists(edges, i, j)) continue;

                float distSqr = (rooms[i].centerTile - rooms[j].centerTile).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    closestOther = j;
                }
            }

            if (closestOther != -1)
                edges.Add((i, closestOther));
        }

        for (int i = 0; i < edges.Count; i++)
        {
            RoomData roomA = rooms[edges[i].a];
            RoomData roomB = rooms[edges[i].b];

            if (!TryPickExitPoint(roomA, roomB.centerTile, out ExitPoint exitA) ||
                !TryPickExitPoint(roomB, roomA.centerTile, out ExitPoint exitB))
                continue;

            List<Vector2Int> path = FindCorridorPath(exitA.outsideTile, exitB.outsideTile);
            if (path == null || path.Count == 0) continue;

            roomA.usedExitTiles.Add(exitA.roomTile);
            roomB.usedExitTiles.Add(exitB.roomTile);

            foreach (Vector2Int tile in path)
            {
                if (map[tile.x, tile.y] == TileType.Empty)
                    map[tile.x, tile.y] = TileType.Corridor;
            }
        }

        BuildCorridorVisuals(parent);
    }

    private bool TryPickExitPoint(RoomData room, Vector2Int targetCenter, out ExitPoint bestExit)
    {
        bestExit = default;
        bool found = false;
        float bestScore = float.MaxValue;

        foreach (Vector2Int tile in room.floorTiles)
        {
            bool tooCloseToOtherExit = false;
            foreach (Vector2Int used in room.usedExitTiles)
            {
                if (Mathf.Abs(tile.x - used.x) + Mathf.Abs(tile.y - used.y) <= 2)
                {
                    tooCloseToOtherExit = true;
                    break;
                }
            }
            if (tooCloseToOtherExit) continue;

            foreach (Vector2Int dir in CardinalDirs)
            {
                Vector2Int outside = tile + dir;
                if (!IsInsideBounds(outside) || map[outside.x, outside.y] != TileType.Empty)
                    continue;

                if (CountAdjacentRoomTiles(outside) != 1)
                    continue;

                float dist = (outside - targetCenter).sqrMagnitude;
                if (dist < bestScore)
                {
                    bestScore = dist;
                    bestExit = new ExitPoint
                    {
                        roomTile = tile,
                        outsideTile = outside,
                        direction = dir
                    };
                    found = true;
                }
            }
        }

        return found;
    }

    private List<Vector2Int> FindCorridorPath(Vector2Int start, Vector2Int goal)
    {
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int>();
        List<(Vector2Int pos, int f)> openList = new List<(Vector2Int, int)>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        gScore[start] = 0;
        openList.Add((start, Manhattan(start, goal)));

        while (openList.Count > 0)
        {
            int bestIdx = 0;
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].f < openList[bestIdx].f)
                    bestIdx = i;
            }

            Vector2Int current = openList[bestIdx].pos;
            openList.RemoveAt(bestIdx);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            if (!closedSet.Add(current)) continue;

            foreach (Vector2Int dir in CardinalDirs)
            {
                Vector2Int next = current + dir;
                if (!IsInsideBounds(next) || closedSet.Contains(next)) continue;

                if (map[next.x, next.y] == TileType.Room) continue;
                if (next != start && next != goal && CountAdjacentRoomTiles(next) > 0) continue;

                int stepCost = map[next.x, next.y] == TileType.Corridor ? 2 : 1;
                int tentativeG = gScore[current] + stepCost;

                if (!gScore.TryGetValue(next, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[next] = current;
                    gScore[next] = tentativeG;
                    openList.Add((next, tentativeG + Manhattan(next, goal)));
                }
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out Vector2Int prev))
        {
            current = prev;
            path.Add(current);
        }
        return path;
    }

    private void BuildCorridorVisuals(Transform parent)
    {
        for (int y = 0; y < mapHeight; y++)
        {
            int runStart = -1;
            for (int x = 0; x <= mapWidth; x++)
            {
                bool isCorridor = x < mapWidth && map[x, y] == TileType.Corridor;

                if (isCorridor && runStart == -1)
                {
                    runStart = x;
                }
                else if (!isCorridor && runStart != -1)
                {
                    int width = x - runStart;
                    Vector2 pos = new Vector2((runStart + width * 0.5f) * tileSize, (y + 0.5f) * tileSize);
                    Vector2 size = new Vector2(width * tileSize, tileSize);
                    CreateTiledPiece(parent, $"Corridor_{runStart}_{y}", pos, size, corridorSprite, Color.white, 0, false);
                    runStart = -1;
                }
            }
        }
    }

    private int CountAdjacentRoomTiles(Vector2Int pos)
    {
        int count = 0;
        foreach (Vector2Int dir in CardinalDirs)
        {
            Vector2Int n = pos + dir;
            if (n.x >= 0 && n.y >= 0 && n.x < mapWidth && n.y < mapHeight && map[n.x, n.y] == TileType.Room)
                count++;
        }
        return count;
    }

    private bool IsInsideBounds(Vector2Int p)
    {
        return p.x >= MapMargin && p.y >= MapMargin &&
               p.x < mapWidth - MapMargin && p.y < mapHeight - MapMargin;
    }

    private int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool EdgeExists(List<(int a, int b)> edges, int u, int v)
    {
        foreach (var e in edges)
        {
            if ((e.a == u && e.b == v) || (e.a == v && e.b == u))
                return true;
        }
        return false;
    }

    private void BuildRoomVisualsAndFurniture(Transform parent, RoomData room)
    {
        string tag = room.isStartRoom ? "_INICIO" : (room.isExitRoom ? "_TESORO_FINAL" : "");
        GameObject roomObj = new GameObject($"Room_{room.id}_{room.theme}_{room.shape}{tag}");
        roomObj.transform.SetParent(parent, false);

        Sprite floorSpr = floorSprites[room.theme];

        for (int i = 0; i < room.visualRects.Count; i++)
        {
            RectInt r = room.visualRects[i];
            Vector2 pos = new Vector2((r.xMin + r.width * 0.5f) * tileSize, (r.yMin + r.height * 0.5f) * tileSize);
            Vector2 size = new Vector2(r.width * tileSize, r.height * tileSize);
            CreateTiledPiece(roomObj.transform, $"Floor_{i}", pos, size, floorSpr, Color.white, 1, false);
        }

        Vector2 centerWorld = new Vector2((room.centerTile.x + 0.5f) * tileSize, (room.centerTile.y + 0.5f) * tileSize);
        if (room.isStartRoom)
        {
            SpawnFloorDecal(roomObj.transform, "Portal_Inicio", centerWorld, "portal_start", 1.8f, new Color(0.25f, 0.75f, 1f, 0.35f));
        }
        else if (room.isExitRoom)
        {
            SpawnFloorDecal(roomObj.transform, "Portal_Final", centerWorld, "portal_exit", 2.0f, new Color(1f, 0.85f, 0.2f, 0.4f));
        }
        else
        {
            SpawnThemeFloorDecal(roomObj.transform, room, centerWorld);
        }

        PlaceFurniture(roomObj.transform, room, centerWorld);

        if (!room.isStartRoom)
            SpawnEnemies(roomObj.transform, room);
    }

    private void SpawnThemeFloorDecal(Transform roomTransform, RoomData room, Vector2 centerWorld)
    {
        if (Random.value > 0.75f) return;

        switch (room.theme)
        {
            case RoomTheme.Library:
                SpawnFloorDecal(roomTransform, "Alfombra", centerWorld, "rug_red", 2.4f, null);
                break;
            case RoomTheme.Treasure:
                SpawnFloorDecal(roomTransform, "AlfombraReal", centerWorld, "rug_gold", 2.4f, null);
                break;
            case RoomTheme.Alchemy:
                SpawnFloorDecal(roomTransform, "CirculoRunico", centerWorld, "rune_circle", 2.2f, new Color(0.65f, 0.25f, 1f, 0.28f));
                break;
            case RoomTheme.Garden:
                SpawnFloorDecal(roomTransform, "Estanque", centerWorld, "puddle", 2.0f, null);
                break;
        }
    }

    private void SpawnFloorDecal(Transform parent, string decalName, Vector2 pos, string spriteKey, float sizeTiles, Color? glowColor)
    {
        GameObject obj = new GameObject(decalName);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one * (sizeTiles * tileSize);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = propSprites[spriteKey];
        sr.sortingOrder = 2;

        if (glowColor.HasValue)
            AddGlow(obj.transform, glowColor.Value, 1.4f);
    }

    private void BuildPerimeterWalls(Transform parent)
    {
        for (int y = 0; y <= mapHeight; y++)
        {
            int runStart = -1;
            for (int x = 0; x <= mapWidth; x++)
            {
                bool needsWall = x < mapWidth && (IsWalkable(x, y) != IsWalkable(x, y - 1));

                if (needsWall && runStart == -1)
                {
                    runStart = x;
                }
                else if (!needsWall && runStart != -1)
                {
                    float length = (x - runStart) * tileSize + wallThickness;
                    Vector2 pos = new Vector2((runStart + x) * 0.5f * tileSize, y * tileSize);
                    CreateTiledPiece(parent, $"Wall_H_{runStart}_{y}", pos, new Vector2(length, wallThickness),
                        wallSprite, Color.white, 5, true);
                    runStart = -1;
                }
            }
        }

        for (int x = 0; x <= mapWidth; x++)
        {
            int runStart = -1;
            for (int y = 0; y <= mapHeight; y++)
            {
                bool needsWall = y < mapHeight && (IsWalkable(x, y) != IsWalkable(x - 1, y));

                if (needsWall && runStart == -1)
                {
                    runStart = y;
                }
                else if (!needsWall && runStart != -1)
                {
                    float length = (y - runStart) * tileSize + wallThickness;
                    Vector2 pos = new Vector2(x * tileSize, (runStart + y) * 0.5f * tileSize);
                    CreateTiledPiece(parent, $"Wall_V_{x}_{runStart}", pos, new Vector2(wallThickness, length),
                        wallSprite, Color.white, 5, true);
                    runStart = -1;
                }
            }
        }
    }

    private bool IsWalkable(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) return false;
        return map[x, y] != TileType.Empty;
    }

    private void PlaceFurniture(Transform roomTransform, RoomData room, Vector2 centerWorld)
    {
        List<Vector2Int> validTiles = new List<Vector2Int>();
        foreach (Vector2Int t in room.floorTiles)
        {
            if (IsRoomTile(t.x + 1, t.y) && IsRoomTile(t.x - 1, t.y) &&
                IsRoomTile(t.x, t.y + 1) && IsRoomTile(t.x, t.y - 1))
            {
                if ((room.isStartRoom || room.isExitRoom) &&
                    Mathf.Abs(t.x - room.centerTile.x) <= 1 && Mathf.Abs(t.y - room.centerTile.y) <= 1)
                    continue;

                validTiles.Add(t);
            }
        }

        if (validTiles.Count == 0) return;

        int min = Mathf.Min(minObjectsPerRoom, maxObjectsPerRoom);
        int max = Mathf.Max(minObjectsPerRoom, maxObjectsPerRoom);
        int count = Random.Range(min, max + 1);

        List<Vector2> placedPositions = new List<Vector2>();
        float minDistance = 1.45f * furnitureScale * tileSize;

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 25; attempt++)
            {
                Vector2Int tile = validTiles[Random.Range(0, validTiles.Count)];
                Vector2 worldPos = new Vector2((tile.x + 0.5f) * tileSize, (tile.y + 0.5f) * tileSize);

                if (room.isStartRoom && Vector2.Distance(worldPos, centerWorld) < 1.5f * tileSize)
                    continue;

                bool tooClose = false;
                foreach (Vector2 other in placedPositions)
                {
                    if (Vector2.Distance(worldPos, other) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                placedPositions.Add(worldPos);
                SpawnThemedProp(roomTransform, room.theme, worldPos, i);
                break;
            }
        }
    }

    private void SpawnEnemies(Transform roomTransform, RoomData room)
    {
        if (enemyPrefab == null) return;

        List<Vector2Int> validTiles = new List<Vector2Int>();
        foreach (Vector2Int t in room.floorTiles)
        {
            if (IsRoomTile(t.x + 1, t.y) && IsRoomTile(t.x - 1, t.y) &&
                IsRoomTile(t.x, t.y + 1) && IsRoomTile(t.x, t.y - 1))
            {
                validTiles.Add(t);
            }
        }

        if (validTiles.Count == 0) return;

        int min = Mathf.Min(minEnemiesPerRoom, maxEnemiesPerRoom);
        int max = Mathf.Max(minEnemiesPerRoom, maxEnemiesPerRoom);
        int count = Random.Range(min, max + 1);

        for (int i = 0; i < count; i++)
        {
            Vector2Int tile = validTiles[Random.Range(0, validTiles.Count)];
            Vector2 worldPos = new Vector2((tile.x + 0.5f) * tileSize, (tile.y + 0.5f) * tileSize);

            GameObject enemy = Instantiate(enemyPrefab, roomTransform);
            enemy.name = $"Enemy_{room.id}_{i}";
            enemy.transform.localPosition = worldPos;
        }
    }

    private bool IsRoomTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) return false;
        return map[x, y] == TileType.Room;
    }

    private void SpawnThemedProp(Transform roomTransform, RoomTheme theme, Vector2 position, int index)
    {
        if (objectPrefabs != null && objectPrefabs.Length > 0)
        {
            GameObject prefab = objectPrefabs[Random.Range(0, objectPrefabs.Length)];
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, roomTransform);
                instance.name = $"Prefab_{index}";
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 4) * 90f);
                instance.transform.localScale *= furnitureScale;
            }
            return;
        }

        string key;
        Color? glow = null;
        float scale = 0.95f;

        switch (theme)
        {
            case RoomTheme.Library:
                switch (Random.Range(0, 3))
                {
                    case 0: key = "bookshelf"; scale = 1.05f; break;
                    case 1: key = "desk"; scale = 1.05f; break;
                    default: key = "brazier"; scale = 0.85f; glow = new Color(1f, 0.65f, 0.2f, 0.32f); break;
                }
                break;

            case RoomTheme.Alchemy:
                switch (Random.Range(0, 3))
                {
                    case 0: key = "cauldron"; scale = 0.95f; glow = new Color(0.25f, 1f, 0.4f, 0.35f); break;
                    case 1: key = "potion_table"; scale = 1.05f; break;
                    default: key = "crystal"; scale = 0.9f; glow = new Color(0.6f, 0.3f, 1f, 0.35f); break;
                }
                break;

            case RoomTheme.Treasure:
                switch (Random.Range(0, 3))
                {
                    case 0: key = "chest"; scale = 0.95f; glow = new Color(1f, 0.85f, 0.25f, 0.25f); break;
                    case 1: key = "gold_pile"; scale = 0.9f; break;
                    default: key = "crystal"; scale = 0.95f; glow = new Color(0.2f, 0.85f, 1f, 0.35f); break;
                }
                break;

            case RoomTheme.Garden:
                switch (Random.Range(0, 3))
                {
                    case 0: key = "bush"; scale = 1.0f; break;
                    case 1: key = "mushrooms"; scale = 0.85f; glow = new Color(0.3f, 0.95f, 0.8f, 0.25f); break;
                    default: key = "pillar"; scale = 0.9f; break;
                }
                break;

            case RoomTheme.Armory:
                switch (Random.Range(0, 3))
                {
                    case 0: key = "anvil"; scale = 0.9f; break;
                    case 1: key = "weapon_rack"; scale = 1.0f; break;
                    default: key = "brazier"; scale = 0.9f; glow = new Color(1f, 0.45f, 0.15f, 0.38f); break;
                }
                break;

            default:
                switch (Random.Range(0, 4))
                {
                    case 0: key = "barrel"; scale = 0.88f; break;
                    case 1: key = "crate"; scale = 0.9f; break;
                    case 2: key = "pillar"; scale = 0.95f; break;
                    default: key = "brazier"; scale = 0.85f; glow = new Color(1f, 0.55f, 0.15f, 0.35f); break;
                }
                break;
        }

        GameObject prop = new GameObject($"{key}_{index}");
        prop.transform.SetParent(roomTransform, false);
        prop.transform.localPosition = position;
        prop.transform.localScale = Vector3.one * (scale * furnitureScale * tileSize);

        SpriteRenderer sr = prop.AddComponent<SpriteRenderer>();
        sr.sprite = propSprites[key];
        sr.sortingOrder = 4;

        BoxCollider2D col = prop.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.76f, 0.76f);

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer != -1) prop.layer = obstacleLayer;

        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(prop.transform, false);
        shadow.transform.localPosition = new Vector2(0f, -0.14f);
        shadow.transform.localScale = new Vector3(0.95f, 0.55f, 1f);
        SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = shadowSprite;
        shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
        shadowSr.sortingOrder = 3;

        if (glow.HasValue)
            AddGlow(prop.transform, glow.Value, 2.2f);
    }

    private void AddGlow(Transform parent, Color color, float scale)
    {
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(parent, false);
        glowObj.transform.localPosition = Vector3.zero;
        glowObj.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = glowObj.AddComponent<SpriteRenderer>();
        sr.sprite = glowSprite;
        sr.color = color;
        sr.sortingOrder = 6;
    }

    private void FitCameraToContent()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        int minX = mapWidth, maxX = 0, minY = mapHeight, maxY = 0;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (map[x, y] != TileType.Empty)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minX > maxX) return;

        float contentW = (maxX - minX + 1) * tileSize;
        float contentH = (maxY - minY + 1) * tileSize;
        float centerX = (minX + maxX + 1) * 0.5f * tileSize;
        float centerY = (minY + maxY + 1) * 0.5f * tileSize;

        cam.orthographic = true;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
        cam.transform.position = new Vector3(centerX, centerY, -10f);
        cam.transform.rotation = Quaternion.identity;

        float verticalSize = contentH * 0.5f;
        float horizontalSize = (contentW * 0.5f) / cam.aspect;
        cam.orthographicSize = Mathf.Max(verticalSize, horizontalSize) * cameraPadding;
    }

    private GameObject CreateTiledPiece(Transform parent, string pieceName, Vector2 localPosition,
        Vector2 size, Sprite sprite, Color color, int sortingOrder, bool collider)
    {
        GameObject piece = new GameObject(pieceName);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = Vector3.one;

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size;
        renderer.sortingOrder = sortingOrder;

        if (collider)
        {
            BoxCollider2D box = piece.AddComponent<BoxCollider2D>();
            box.size = size;

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer != -1) piece.layer = obstacleLayer;
        }

        return piece;
    }

    // =========================================================================
    // GENERACIÓN PROCEDURAL DE SPRITES PIXEL-ART
    // =========================================================================

    private void EnsureSprites()
    {
        if (spritesReady) return;
        spritesReady = true;

        float ppu = 16f / tileSize;

        floorSprites = new Dictionary<RoomTheme, Sprite>
        {
            { RoomTheme.Dungeon, BuildDungeonFloorSprite(ppu) },
            { RoomTheme.Library, BuildLibraryFloorSprite(ppu) },
            { RoomTheme.Alchemy, BuildAlchemyFloorSprite(ppu) },
            { RoomTheme.Treasure, BuildTreasureFloorSprite(ppu) },
            { RoomTheme.Garden, BuildGardenFloorSprite(ppu) },
            { RoomTheme.Armory, BuildArmoryFloorSprite(ppu) }
        };

        corridorSprite = BuildCorridorFloorSprite(ppu);
        wallSprite = BuildWallSprite(ppu);
        shadowSprite = BuildSoftCircleSprite(32, false);
        glowSprite = BuildSoftCircleSprite(32, true);

        propSprites = new Dictionary<string, Sprite>
        {
            { "barrel", BuildBarrelSprite() },
            { "crate", BuildCrateSprite() },
            { "pillar", BuildPillarSprite() },
            { "brazier", BuildBrazierSprite() },
            { "bookshelf", BuildBookshelfSprite() },
            { "desk", BuildDeskSprite() },
            { "cauldron", BuildCauldronSprite() },
            { "potion_table", BuildPotionTableSprite() },
            { "crystal", BuildCrystalSprite() },
            { "chest", BuildChestSprite() },
            { "gold_pile", BuildGoldPileSprite() },
            { "bush", BuildBushSprite() },
            { "mushrooms", BuildMushroomsSprite() },
            { "anvil", BuildAnvilSprite() },
            { "weapon_rack", BuildWeaponRackSprite() },
            { "rug_red", BuildRugSprite(new Color(0.62f, 0.14f, 0.16f), new Color(0.88f, 0.72f, 0.28f)) },
            { "rug_gold", BuildRugSprite(new Color(0.22f, 0.24f, 0.52f), new Color(0.95f, 0.80f, 0.30f)) },
            { "rune_circle", BuildRuneCircleSprite(new Color(0.75f, 0.40f, 1f, 0.85f)) },
            { "portal_start", BuildRuneCircleSprite(new Color(0.30f, 0.85f, 1f, 0.95f)) },
            { "portal_exit", BuildRuneCircleSprite(new Color(1f, 0.85f, 0.25f, 0.95f)) },
            { "puddle", BuildPuddleSprite() }
        };
    }

    private Texture2D NewTex(int size, bool repeat = false)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        tex.SetPixels(new Color[size * size]);
        return tex;
    }

    private Sprite ToSprite(Texture2D tex, float ppu = 16f)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }

    private void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
    {
        for (int ix = x; ix < x + w; ix++)
            for (int iy = y; iy < y + h; iy++)
                if (ix >= 0 && iy >= 0 && ix < tex.width && iy < tex.height)
                    tex.SetPixel(ix, iy, c);
    }

    private void FillCircle(Texture2D tex, float cx, float cy, float r, Color c)
    {
        float r2 = r * r;
        for (int ix = 0; ix < tex.width; ix++)
            for (int iy = 0; iy < tex.height; iy++)
                if ((ix - cx) * (ix - cx) + (iy - cy) * (iy - cy) <= r2)
                    tex.SetPixel(ix, iy, c);
    }

    private Sprite BuildDungeonFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color baseC = new Color(0.40f, 0.39f, 0.37f);
        Color lightC = new Color(0.46f, 0.45f, 0.42f);
        Color mortar = new Color(0.27f, 0.26f, 0.25f);
        FillRect(t, 0, 0, 16, 16, baseC);
        FillRect(t, 0, 0, 16, 1, mortar);
        FillRect(t, 0, 8, 16, 1, mortar);
        FillRect(t, 0, 0, 1, 8, mortar);
        FillRect(t, 8, 8, 1, 8, mortar);
        FillRect(t, 2, 2, 5, 4, lightC);
        FillRect(t, 10, 10, 4, 4, lightC);
        t.SetPixel(13, 3, mortar);
        t.SetPixel(4, 12, mortar);
        return ToSprite(t, ppu);
    }

    private Sprite BuildLibraryFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color wood1 = new Color(0.50f, 0.34f, 0.20f);
        Color wood2 = new Color(0.45f, 0.30f, 0.18f);
        Color seam = new Color(0.30f, 0.19f, 0.11f);
        for (int row = 0; row < 4; row++)
        {
            FillRect(t, 0, row * 4, 16, 4, (row % 2 == 0) ? wood1 : wood2);
            FillRect(t, 0, row * 4, 16, 1, seam);
            int joint = (row * 5 + 3) % 16;
            FillRect(t, joint, row * 4, 1, 4, seam);
        }
        return ToSprite(t, ppu);
    }

    private Sprite BuildAlchemyFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color c1 = new Color(0.28f, 0.24f, 0.35f);
        Color c2 = new Color(0.36f, 0.31f, 0.44f);
        Color line = new Color(0.20f, 0.17f, 0.26f);
        FillRect(t, 0, 0, 8, 8, c1);
        FillRect(t, 8, 8, 8, 8, c1);
        FillRect(t, 8, 0, 8, 8, c2);
        FillRect(t, 0, 8, 8, 8, c2);
        FillRect(t, 0, 0, 16, 1, line);
        FillRect(t, 0, 8, 16, 1, line);
        FillRect(t, 0, 0, 1, 16, line);
        FillRect(t, 8, 0, 1, 16, line);
        return ToSprite(t, ppu);
    }

    private Sprite BuildTreasureFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color marble = new Color(0.66f, 0.63f, 0.56f);
        Color vein = new Color(0.58f, 0.55f, 0.49f);
        Color gold = new Color(0.76f, 0.64f, 0.30f);
        FillRect(t, 0, 0, 16, 16, marble);
        FillRect(t, 0, 0, 16, 1, gold);
        FillRect(t, 0, 0, 1, 16, gold);
        t.SetPixel(4, 5, vein); t.SetPixel(5, 6, vein); t.SetPixel(6, 6, vein);
        t.SetPixel(11, 10, vein); t.SetPixel(12, 11, vein);
        return ToSprite(t, ppu);
    }

    private Sprite BuildGardenFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color grass1 = new Color(0.30f, 0.47f, 0.27f);
        Color grass2 = new Color(0.36f, 0.54f, 0.31f);
        Color stone = new Color(0.45f, 0.46f, 0.41f);
        FillRect(t, 0, 0, 16, 16, grass1);
        FillRect(t, 2, 2, 5, 5, stone);
        FillRect(t, 9, 9, 5, 5, stone);
        FillRect(t, 9, 2, 3, 2, grass2);
        FillRect(t, 3, 10, 3, 2, grass2);
        t.SetPixel(13, 4, new Color(0.95f, 0.85f, 0.3f));
        t.SetPixel(4, 13, new Color(0.92f, 0.55f, 0.72f));
        return ToSprite(t, ppu);
    }

    private Sprite BuildArmoryFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color plate = new Color(0.30f, 0.31f, 0.34f);
        Color border = new Color(0.20f, 0.21f, 0.23f);
        Color rivet = new Color(0.45f, 0.47f, 0.50f);
        FillRect(t, 0, 0, 16, 16, plate);
        FillRect(t, 0, 0, 16, 1, border);
        FillRect(t, 0, 0, 1, 16, border);
        t.SetPixel(2, 2, rivet); t.SetPixel(13, 2, rivet);
        t.SetPixel(2, 13, rivet); t.SetPixel(13, 13, rivet);
        return ToSprite(t, ppu);
    }

    private Sprite BuildCorridorFloorSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color bg = new Color(0.26f, 0.27f, 0.30f);
        Color cobble = new Color(0.33f, 0.34f, 0.38f);
        FillRect(t, 0, 0, 16, 16, bg);
        FillRect(t, 1, 1, 6, 6, cobble);
        FillRect(t, 9, 1, 6, 6, cobble);
        FillRect(t, 1, 9, 6, 6, cobble);
        FillRect(t, 9, 9, 6, 6, cobble);
        return ToSprite(t, ppu);
    }

    private Sprite BuildWallSprite(float ppu)
    {
        Texture2D t = NewTex(16, true);
        Color dark = new Color(0.13f, 0.13f, 0.16f);
        Color brick = new Color(0.22f, 0.22f, 0.27f);
        Color top = new Color(0.30f, 0.31f, 0.37f);
        FillRect(t, 0, 0, 16, 16, dark);
        FillRect(t, 1, 1, 6, 6, brick);
        FillRect(t, 9, 1, 6, 6, brick);
        FillRect(t, 1, 9, 14, 6, brick);
        FillRect(t, 1, 14, 14, 1, top);
        return ToSprite(t, ppu);
    }

    private Sprite BuildSoftCircleSprite(int size, bool radialFade)
    {
        Texture2D t = NewTex(size);
        t.filterMode = FilterMode.Bilinear;
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = radialFade ? Mathf.Pow(Mathf.Clamp01(1f - d), 2f) : Mathf.Clamp01((1f - d) * 4f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ToSprite(t, size);
    }

    private Sprite BuildBarrelSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 6.2f, new Color(0.20f, 0.12f, 0.06f));
        FillCircle(t, 7.5f, 7.5f, 5.2f, new Color(0.52f, 0.30f, 0.14f));
        FillRect(t, 3, 7, 10, 1, new Color(0.32f, 0.18f, 0.08f));
        FillRect(t, 4, 10, 8, 1, new Color(0.45f, 0.47f, 0.52f));
        FillRect(t, 4, 4, 8, 1, new Color(0.45f, 0.47f, 0.52f));
        return ToSprite(t);
    }

    private Sprite BuildCrateSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 2, 2, 12, 12, new Color(0.28f, 0.17f, 0.08f));
        FillRect(t, 3, 3, 10, 10, new Color(0.65f, 0.44f, 0.22f));
        FillRect(t, 4, 4, 8, 8, new Color(0.55f, 0.35f, 0.17f));
        for (int i = 4; i < 12; i++) t.SetPixel(i, i, new Color(0.68f, 0.48f, 0.24f));
        return ToSprite(t);
    }

    private Sprite BuildPillarSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 3, 2, 10, 12, new Color(0.22f, 0.23f, 0.26f));
        FillRect(t, 4, 3, 8, 10, new Color(0.48f, 0.50f, 0.53f));
        FillRect(t, 5, 5, 6, 7, new Color(0.64f, 0.66f, 0.69f));
        return ToSprite(t);
    }

    private Sprite BuildBrazierSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 5.5f, new Color(0.18f, 0.18f, 0.22f));
        FillCircle(t, 7.5f, 7.5f, 3.8f, new Color(0.95f, 0.40f, 0.10f));
        FillCircle(t, 7.5f, 8.0f, 2.2f, new Color(1f, 0.85f, 0.25f));
        return ToSprite(t);
    }

    private Sprite BuildBookshelfSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 1, 2, 14, 12, new Color(0.26f, 0.15f, 0.08f));
        FillRect(t, 2, 3, 12, 10, new Color(0.44f, 0.26f, 0.14f));
        Color[] books = { new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 0.8f), new Color(0.2f, 0.7f, 0.3f), new Color(0.9f, 0.75f, 0.2f) };
        for (int x = 3; x <= 11; x += 2)
        {
            FillRect(t, x, 4, 2, 3, books[x % books.Length]);
            FillRect(t, x, 9, 2, 3, books[(x + 1) % books.Length]);
        }
        return ToSprite(t);
    }

    private Sprite BuildDeskSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 1, 3, 14, 10, new Color(0.26f, 0.14f, 0.07f));
        FillRect(t, 2, 4, 12, 8, new Color(0.56f, 0.34f, 0.18f));
        FillRect(t, 4, 6, 4, 4, new Color(0.92f, 0.88f, 0.74f));
        FillRect(t, 10, 8, 2, 2, new Color(1f, 0.88f, 0.3f));
        return ToSprite(t);
    }

    private Sprite BuildCauldronSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 5.8f, new Color(0.14f, 0.15f, 0.18f));
        FillCircle(t, 7.5f, 7.5f, 4.0f, new Color(0.22f, 0.82f, 0.35f));
        FillCircle(t, 6.0f, 8.5f, 1.2f, new Color(0.65f, 1f, 0.55f));
        return ToSprite(t);
    }

    private Sprite BuildPotionTableSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 1, 3, 14, 10, new Color(0.20f, 0.15f, 0.25f));
        FillRect(t, 2, 4, 12, 8, new Color(0.38f, 0.30f, 0.45f));
        FillRect(t, 4, 6, 2, 3, new Color(0.95f, 0.25f, 0.35f));
        FillRect(t, 7, 7, 2, 3, new Color(0.25f, 0.75f, 1f));
        FillRect(t, 10, 6, 2, 3, new Color(0.35f, 0.95f, 0.45f));
        return ToSprite(t);
    }

    private Sprite BuildCrystalSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 5.5f, 4.5f, new Color(0.32f, 0.34f, 0.40f));
        FillRect(t, 6, 5, 4, 7, new Color(0.35f, 0.75f, 1f));
        FillRect(t, 7, 7, 2, 6, new Color(0.75f, 0.95f, 1f));
        return ToSprite(t);
    }

    private Sprite BuildChestSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 2, 3, 12, 10, new Color(0.24f, 0.13f, 0.05f));
        FillRect(t, 3, 4, 10, 8, new Color(0.58f, 0.32f, 0.12f));
        FillRect(t, 3, 7, 10, 2, new Color(0.92f, 0.76f, 0.22f));
        FillRect(t, 7, 6, 2, 3, new Color(1f, 0.92f, 0.45f));
        return ToSprite(t);
    }

    private Sprite BuildGoldPileSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 5.0f, new Color(0.80f, 0.60f, 0.12f));
        FillCircle(t, 6.5f, 8.5f, 3.0f, new Color(0.98f, 0.82f, 0.24f));
        FillCircle(t, 9.5f, 6.5f, 2.5f, new Color(1f, 0.90f, 0.35f));
        t.SetPixel(8, 8, new Color(0.95f, 0.20f, 0.30f));
        return ToSprite(t);
    }

    private Sprite BuildBushSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 6.0f, new Color(0.14f, 0.35f, 0.16f));
        FillCircle(t, 6.0f, 8.5f, 4.0f, new Color(0.22f, 0.55f, 0.24f));
        FillCircle(t, 9.5f, 7.0f, 3.5f, new Color(0.28f, 0.65f, 0.28f));
        t.SetPixel(6, 9, new Color(0.95f, 0.30f, 0.35f));
        t.SetPixel(10, 6, new Color(0.95f, 0.30f, 0.35f));
        return ToSprite(t);
    }

    private Sprite BuildMushroomsSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 5.5f, 8.5f, 3.2f, new Color(0.85f, 0.25f, 0.25f));
        FillCircle(t, 10.5f, 6.5f, 2.6f, new Color(0.30f, 0.85f, 0.75f));
        t.SetPixel(5, 9, Color.white);
        t.SetPixel(7, 8, Color.white);
        t.SetPixel(10, 7, Color.white);
        return ToSprite(t);
    }

    private Sprite BuildAnvilSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 2, 6, 12, 4, new Color(0.20f, 0.22f, 0.26f));
        FillRect(t, 3, 7, 10, 2, new Color(0.45f, 0.48f, 0.54f));
        FillRect(t, 5, 4, 6, 3, new Color(0.30f, 0.32f, 0.36f));
        return ToSprite(t);
    }

    private Sprite BuildWeaponRackSprite()
    {
        Texture2D t = NewTex(16);
        FillRect(t, 2, 3, 12, 10, new Color(0.36f, 0.22f, 0.12f));
        FillRect(t, 4, 4, 2, 8, new Color(0.75f, 0.80f, 0.88f));
        FillRect(t, 7, 4, 2, 8, new Color(0.75f, 0.80f, 0.88f));
        FillRect(t, 10, 4, 2, 8, new Color(0.75f, 0.80f, 0.88f));
        return ToSprite(t);
    }

    private Sprite BuildRugSprite(Color main, Color trim)
    {
        Texture2D t = NewTex(16);
        FillRect(t, 1, 2, 14, 12, trim);
        FillRect(t, 2, 3, 12, 10, main);
        FillRect(t, 4, 5, 8, 6, trim);
        FillRect(t, 5, 6, 6, 4, main);
        return ToSprite(t);
    }

    private Sprite BuildRuneCircleSprite(Color color)
    {
        Texture2D t = NewTex(16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                if ((d >= 5.5f && d <= 6.8f) || (d >= 2.2f && d <= 3.1f))
                    t.SetPixel(x, y, color);
            }
        }
        return ToSprite(t);
    }

    private Sprite BuildPuddleSprite()
    {
        Texture2D t = NewTex(16);
        FillCircle(t, 7.5f, 7.5f, 5.8f, new Color(0.20f, 0.48f, 0.72f, 0.85f));
        FillCircle(t, 6.0f, 8.5f, 3.0f, new Color(0.32f, 0.65f, 0.88f, 0.85f));
        t.SetPixel(5, 10, new Color(0.85f, 0.95f, 1f, 0.9f));
        return ToSprite(t);
    }
}