using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;

public class PlayerController : MonoBehaviour
{
    // Define several levels as arrays of strings.
    // Rows are read top-to-bottom. We flip the y coordinate so the top row appears at the top.
    // '.' = not part of level; '0' = valid cell; '1' = left player's start; '2' = right player's start.
    private List<string[]> levels = new List<string[]>()
    {
        new string[]
        {
            "......",
            "......",
            "100002",
            "......",
            "......"
        },
        new string[]
        {
            "..1...",
            "..0...",
            "..0...",
            "..0...",
            "..0...",
            "..2..."
        },
        new string[]
        {
            "1.0000",
            "000000",
            ".000.0",
            "000000",
            "000020"
        }
    };

    private int currentLevelIndex = 0;
    private int gridWidth;
    private int gridHeight;
    private int gridHalfWidth;

    // Spacing between cells (e.g., in pixels)
    public float cellSpacing = 150f;

    // Grid positions for the two players
    private Vector2Int leftPlayerPos;
    private Vector2Int rightPlayerPos;

    // References to the players' RectTransforms.
    // Assume the left player's RectTransform is on the same GameObject as this script.
    private RectTransform leftPlayerRect;
    public GameObject rightPlayer;
    private RectTransform rightPlayerRect;

    // Prefab for a visual indicator of a valid grid cell.
    public GameObject positionPrefab;
    private List<GameObject> positionIndicators = new List<GameObject>();

    // Parent for the grid (assumed to be the same parent as the players)
    private RectTransform gridParent;

    // Occupancy grid: true indicates a cell is part of the level.
    private bool[,] occupancyGrid;

    // List of level backgrounds
    public GameObject[] levelBackgrounds;
    public GameObject verticalBackground;
    public GameObject horizontalBackground;

    void Awake()
    {
        leftPlayerRect = GetComponent<RectTransform>();
        if (rightPlayer != null)
        {
            rightPlayerRect = rightPlayer.GetComponent<RectTransform>();
        }
        
        gridParent = leftPlayerRect.parent.GetComponent<RectTransform>();
        LoadLevel(currentLevelIndex);
    }

    void Update()
    {
        // Horizontal movement for left player: valid x range [0, gridHalfWidth-1]
        // For right player: valid x range [gridHalfWidth, gridWidth-1] (mirrored movement)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            // Left player moves left.
            leftPlayerPos = FindNextValidHorizontal(leftPlayerPos, -1, 0, gridHalfWidth - 1);
            // Right player moves right (mirrored).
            rightPlayerPos = FindNextValidHorizontal(rightPlayerPos, 1, gridHalfWidth, gridWidth - 1);
            UpdatePlayerPositions();
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            // If the left player is at the rightmost cell of its half and the right player
            // is at the leftmost cell of its half, and we press right, advance to the next level.
            if (leftPlayerPos.x == gridHalfWidth - 1 && rightPlayerPos.x == gridHalfWidth && currentLevelIndex != 1)
            {
                AdvanceLevel();
            }
            else
            {
                // Otherwise, perform the normal rightward movement.
                leftPlayerPos = FindNextValidHorizontal(leftPlayerPos, 1, 0, gridHalfWidth - 1);
                rightPlayerPos = FindNextValidHorizontal(rightPlayerPos, -1, gridHalfWidth, gridWidth - 1);
                UpdatePlayerPositions();
            }
        }
        // Vertical movement for both players: full vertical range [0, gridHeight - 1]
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            leftPlayerPos = FindNextValidVertical(leftPlayerPos, 1);
            // Note: right player moves in the opposite vertical direction.
            rightPlayerPos = FindNextValidVertical(rightPlayerPos, -1);
            UpdatePlayerPositions();
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            int gridHalfHeight = gridHeight / 2;  // TODO: This calculate should be moved out somewhere
            if (leftPlayerPos.y == gridHalfHeight && rightPlayerPos.y == gridHalfHeight - 1 && currentLevelIndex == 1)
            {
                AdvanceLevel();
            }
            leftPlayerPos = FindNextValidVertical(leftPlayerPos, -1);
            rightPlayerPos = FindNextValidVertical(rightPlayerPos, 1);
            UpdatePlayerPositions();
        }

        // Optional: press L to switch to the next level manually.
        if (Input.GetKeyDown(KeyCode.L))
        {
            AdvanceLevel();
        }
    }

    /// <summary>
    /// Advances to the next level.
    /// </summary>
    void AdvanceLevel()
    {
        currentLevelIndex = (currentLevelIndex + 1) % levels.Count;
        LoadLevel(currentLevelIndex);
    }

    // Loads a level based on the current level layout.
    void LoadLevel(int index)
    {
        // Clear any existing grid cell indicators.
        foreach (var indicator in positionIndicators)
        {
            Destroy(indicator);
        }
        positionIndicators.Clear();

        // Load the level background.
        if (levelBackgrounds.Length > 0) {
            foreach (var background in levelBackgrounds)
            {
                background.SetActive(false);
            }
            if (index < levelBackgrounds.Length) {
                levelBackgrounds[index].SetActive(true);
            }
        }

        // TODO: Hard-coded for now. Only show vertical background for level 1.
        if (index == 1)
        {
            verticalBackground.SetActive(true);
            horizontalBackground.SetActive(false);
        }
        else
        {
            verticalBackground.SetActive(false);
            horizontalBackground.SetActive(true);
        }

        string[] levelLayout = levels[index];
        gridHeight = levelLayout.Length;
        gridWidth = 0;
        // Determine the grid width based on the longest row.
        foreach (string row in levelLayout)
        {
            if (row.Length > gridWidth)
                gridWidth = row.Length;
        }

        Debug.Assert(gridWidth > 0 && gridHeight > 0 && gridWidth % 2 == 0, "Invalid level layout");
        gridHalfWidth = gridWidth / 2;

        occupancyGrid = new bool[gridWidth, gridHeight];

        // Loop over each row and column.
        // We flip y so that the first string row (top of the level) becomes the highest y value.
        for (int y = 0; y < gridHeight; y++)
        {
            string row = levelLayout[y];
            for (int x = 0; x < row.Length; x++)
            {
                char cell = row[x];
                if (cell == '.')
                {
                    // Not part of the level.
                    continue;
                }

                // Calculate grid position.
                // Flip y: top row (y==0) becomes gridY = gridHeight - 1.
                Vector2Int gridPos = new Vector2Int(x, gridHeight - 1 - y);
                occupancyGrid[gridPos.x, gridPos.y] = true;

                // Instantiate the position prefab to show the valid cell.
                if (positionPrefab != null)
                {
                    GameObject indicator = Instantiate(positionPrefab, gridParent);
                    RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
                    indicatorRect.anchoredPosition = GetAnchoredPosition(gridPos);
                    positionIndicators.Add(indicator);
                }

                // Set starting positions.
                if (cell == '1')
                {
                    leftPlayerPos = gridPos;
                }
                else if (cell == '2')
                {
                    rightPlayerPos = gridPos;
                }
                // '0' means valid cell with no special start.
            }
        }
        UpdatePlayerPositions();
    }

    // Returns the anchored position for a given grid coordinate.
    Vector2 GetAnchoredPosition(Vector2Int pos)
    {
        float offsetX = (gridWidth - 1) / 2f;
        float offsetY = (gridHeight - 1) / 2f;
        float anchoredX = (pos.x - offsetX) * cellSpacing;
        float anchoredY = (pos.y - offsetY) * cellSpacing;
        return new Vector2(anchoredX, anchoredY);
    }

    // Updates the players' positions on the canvas.
    void UpdatePlayerPositions()
    {
        leftPlayerRect.anchoredPosition = GetAnchoredPosition(leftPlayerPos);
        if (rightPlayerRect != null)
        {
            rightPlayerRect.anchoredPosition = GetAnchoredPosition(rightPlayerPos);
        }
    }

    // Helper method: searches horizontally (within a given x-range) for the next valid cell.
    // 'direction' should be +1 (right) or -1 (left).
    Vector2Int FindNextValidHorizontal(Vector2Int start, int direction, int minX, int maxX)
    {
        Vector2Int candidate = start;
        // Loop at most the width of the half to avoid infinite loops.
        for (int i = 0; i < (maxX - minX + 1); i++)
        {
            candidate.x += direction;
            if (candidate.x < minX)
                candidate.x = maxX;
            else if (candidate.x > maxX)
                candidate.x = minX;
            if (occupancyGrid[candidate.x, candidate.y])
                return candidate;
        }
        // Fallback: if no valid cell is found, return the start.
        return start;
    }

    // Helper method: searches vertically for the next valid cell.
    // 'direction' should be +1 (up) or -1 (down).
    Vector2Int FindNextValidVertical(Vector2Int start, int direction)
    {
        Vector2Int candidate = start;
        // Loop at most the gridHeight times.
        for (int i = 0; i < gridHeight; i++)
        {
            candidate.y += direction;
            if (candidate.y < 0)
                candidate.y = gridHeight - 1;
            else if (candidate.y >= gridHeight)
                candidate.y = 0;
            if (occupancyGrid[candidate.x, candidate.y])
                return candidate;
        }
        return start;
    }
}