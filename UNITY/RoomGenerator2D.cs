using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generador 2D de habitaciones conectadas para una vista cenital.
/// Añade este componente a un GameObject vacío y pulsa Play.
/// </summary>
public class RoomGenerator2D : MonoBehaviour
{
    [Header("Generación")]
    [Min(1)] public int gridWidth = 5;
    [Min(1)] public int gridHeight = 5;
    [Min(4f)] public float roomSize = 12f;
    [Range(0f, 1f)] public float extraDoorChance = 0.15f;
    public bool randomSeed = true;
    public int seed = 12345;
    public bool generateOnStart = true;

    [Header("Habitaciones")]
    [Min(0.1f)] public float wallThickness = 0.35f;
    [Range(0.5f, 0.9f)] public float doorWidth = 2.2f;
    public bool randomFloorColors = true;

    [Header("Decoración")]
    [Min(0)] public int minObjectsPerRoom = 3;
    [Min(0)] public int maxObjectsPerRoom = 7;
    [Min(0.1f)] public float furnitureScale = 1f;
    public GameObject[] objectPrefabs;

    [Header("Cámara")]
    public bool fitMainCamera = true;
    [Min(1f)] public float cameraPadding = 1.15f;

    private const string RootName = "GeneratedRooms2D";
    private bool[,] doorRight;
    private bool[,] doorUp;

    private Sprite squareSprite;
    private Sprite circleSprite;

    private readonly Color wallColor = new Color(0.18f, 0.16f, 0.15f);
    private readonly Color[] floorPalette =
    {
        new Color(0.55f, 0.48f, 0.38f),
        new Color(0.48f, 0.50f, 0.43f),
        new Color(0.57f, 0.52f, 0.45f),
        new Color(0.43f, 0.48f, 0.48f)
    };

    private void Start()
    {
        CreateSprites();
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generar habitaciones 2D")]
    public void Generate()
    {
        CreateSprites();
        Clear();

        if (randomSeed) seed = System.Environment.TickCount;
        Random.InitState(seed);

        doorRight = new bool[gridWidth, gridHeight];
        doorUp = new bool[gridWidth, gridHeight];

        CarveConnectedLayout();

        Transform root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
                BuildRoom(root, x, y);
        }

        if (fitMainCamera) FitCamera();
        Debug.Log($"RoomGenerator2D: {gridWidth * gridHeight} habitaciones 2D generadas.");
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

    private void CreateSprites()
    {
        if (squareSprite == null) squareSprite = MakeSprite(false);
        if (circleSprite == null) circleSprite = MakeSprite(true);
    }

    private Sprite MakeSprite(bool circle)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = !circle ||
                    (new Vector2(x - 15.5f, y - 15.5f).sqrMagnitude <= 15.5f * 15.5f);
                pixels[y * size + x] = inside ? Color.white : new Color(1, 1, 1, 0);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void CarveConnectedLayout()
    {
        bool[,] visited = new bool[gridWidth, gridHeight];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();

        Vector2Int start = new Vector2Int(
            Random.Range(0, gridWidth), Random.Range(0, gridHeight));

        visited[start.x, start.y] = true;
        stack.Push(start);

        Vector2Int[] directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> options = new List<Vector2Int>();

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (next.x < 0 || next.y < 0 ||
                    next.x >= gridWidth || next.y >= gridHeight)
                    continue;

                if (!visited[next.x, next.y]) options.Add(next);
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int chosen = options[Random.Range(0, options.Count)];
            OpenDoor(current, chosen);
            visited[chosen.x, chosen.y] = true;
            stack.Push(chosen);
        }

        // Puertas adicionales para crear rutas alternativas.
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (x < gridWidth - 1 && Random.value < extraDoorChance)
                    doorRight[x, y] = true;

                if (y < gridHeight - 1 && Random.value < extraDoorChance)
                    doorUp[x, y] = true;
            }
        }
    }

    private void OpenDoor(Vector2Int a, Vector2Int b)
    {
        if (b.x == a.x + 1) doorRight[a.x, a.y] = true;
        else if (b.x == a.x - 1) doorRight[b.x, b.y] = true;
        else if (b.y == a.y + 1) doorUp[a.x, a.y] = true;
        else if (b.y == a.y - 1) doorUp[b.x, b.y] = true;
    }

    private void BuildRoom(Transform root, int x, int y)
    {
        GameObject room = new GameObject($"Room_{x}_{y}");
        room.transform.SetParent(root, false);

        Vector2 center = new Vector2(
            x * roomSize + roomSize * 0.5f,
            y * roomSize + roomSize * 0.5f);

        room.transform.localPosition = center;

        Color floorColor = randomFloorColors
            ? floorPalette[Random.Range(0, floorPalette.Length)]
            : floorPalette[0];

        CreatePiece(room.transform, "Floor", Vector2.zero,
            new Vector2(roomSize, roomSize), floorColor, squareSprite, 0, false);

        float half = roomSize * 0.5f;

        // Cada pared compartida se construye una sola vez.
        BuildWall(room.transform, "Wall_Top",
            new Vector2(0, half), true,
            y < gridHeight - 1 && doorUp[x, y]);

        BuildWall(room.transform, "Wall_Right",
            new Vector2(half, 0), false,
            x < gridWidth - 1 && doorRight[x, y]);

        if (y == 0)
            BuildWall(room.transform, "Wall_Bottom",
                new Vector2(0, -half), true, false);

        if (x == 0)
            BuildWall(room.transform, "Wall_Left",
                new Vector2(-half, 0), false, false);

        PlaceFurniture(room.transform, half);
    }

    private void BuildWall(Transform parent, string wallName,
        Vector2 center, bool horizontal, bool hasDoor)
    {
        float length = roomSize + wallThickness;

        if (!hasDoor)
        {
            Vector2 size = horizontal
                ? new Vector2(length, wallThickness)
                : new Vector2(wallThickness, length);

            CreatePiece(parent, wallName, center, size,
                wallColor, squareSprite, 3, true);
            return;
        }

        float segmentLength = (length - doorWidth) * 0.5f;
        float offset = (doorWidth + segmentLength) * 0.5f;

        Vector2 segmentSize = horizontal
            ? new Vector2(segmentLength, wallThickness)
            : new Vector2(wallThickness, segmentLength);

        Vector2 direction = horizontal ? Vector2.right : Vector2.up;

        CreatePiece(parent, wallName + "_A",
            center + direction * offset, segmentSize,
            wallColor, squareSprite, 3, true);

        CreatePiece(parent, wallName + "_B",
            center - direction * offset, segmentSize,
            wallColor, squareSprite, 3, true);
    }

    private void PlaceFurniture(Transform room, float half)
    {
        int min = Mathf.Min(minObjectsPerRoom, maxObjectsPerRoom);
        int max = Mathf.Max(minObjectsPerRoom, maxObjectsPerRoom);
        int count = Random.Range(min, max + 1);

        List<Vector2> positions = new List<Vector2>();
        float margin = 1.5f;
        float limit = half - margin;

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 position = new Vector2(
                    Random.Range(-limit, limit),
                    Random.Range(-limit, limit));

                // Mantener despejadas las zonas cercanas a las puertas.
                if (Mathf.Abs(position.x) < doorWidth &&
                    Mathf.Abs(position.y) > half - 2.5f)
                    continue;

                if (Mathf.Abs(position.y) < doorWidth &&
                    Mathf.Abs(position.x) > half - 2.5f)
                    continue;

                bool tooClose = false;
                foreach (Vector2 other in positions)
                {
                    if (Vector2.Distance(position, other) < 1.8f * furnitureScale)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                positions.Add(position);
                SpawnFurniture(room, position, i);
                break;
            }
        }
    }

    private void SpawnFurniture(Transform room, Vector2 position, int index)
    {
        if (objectPrefabs != null && objectPrefabs.Length > 0)
        {
            GameObject prefab = objectPrefabs[Random.Range(0, objectPrefabs.Length)];
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, room);
                instance.name = $"Prefab_{index}";
                instance.transform.localPosition = position;
                instance.transform.localRotation =
                    Quaternion.Euler(0, 0, Random.Range(0, 4) * 90f);
                instance.transform.localScale *= furnitureScale;
            }
            return;
        }

        // Objetos provisionales dibujados con sprites 2D.
        int type = Random.Range(0, 4);
        Color color;
        Vector2 size;
        Sprite sprite = squareSprite;
        string objectName;

        switch (type)
        {
            case 0: // Mesa
                objectName = "Mesa";
                color = new Color(0.48f, 0.25f, 0.12f);
                size = new Vector2(1.8f, 1.2f);
                break;
            case 1: // Barril
                objectName = "Barril";
                color = new Color(0.38f, 0.20f, 0.09f);
                size = new Vector2(1.15f, 1.15f);
                sprite = circleSprite;
                break;
            case 2: // Caja
                objectName = "Caja";
                color = new Color(0.65f, 0.43f, 0.20f);
                size = new Vector2(1.15f, 1.15f);
                break;
            default: // Estantería
                objectName = "Estanteria";
                color = new Color(0.30f, 0.17f, 0.10f);
                size = new Vector2(0.9f, 2f);
                break;
        }

        size *= furnitureScale;
        GameObject furniture = CreatePiece(room, objectName + "_" + index,
            position, size, color, sprite, 2, true);

        if (type == 0)
        {
            // Pequeña marca central para distinguir visualmente la mesa.
            CreatePiece(furniture.transform, "Superficie",
                Vector2.zero, size * 0.72f,
                new Color(0.68f, 0.43f, 0.23f), squareSprite, 1, false);
        }
    }

    private GameObject CreatePiece(Transform parent, string pieceName,
        Vector2 localPosition, Vector2 size, Color color,
        Sprite sprite, int sortingOrder, bool collider)
    {
        GameObject piece = new GameObject(pieceName);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = new Vector3(size.x, size.y, 1);

        SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (collider)
        {
            BoxCollider2D box = piece.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }

        return piece;
    }

    private void FitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.transform.position = new Vector3(
            gridWidth * roomSize * 0.5f,
            gridHeight * roomSize * 0.5f,
            -10f);
        cam.transform.rotation = Quaternion.identity;

        float verticalSize = gridHeight * roomSize * 0.5f;
        float horizontalSize = (gridWidth * roomSize * 0.5f) / cam.aspect;
        cam.orthographicSize = Mathf.Max(verticalSize, horizontalSize) * cameraPadding;
    }
}
