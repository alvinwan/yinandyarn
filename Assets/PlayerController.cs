using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Collections;
using TMPro;

public enum Direction
{
    Left,
    Right,
    Up,
    Down
}

public class PlayerController : MonoBehaviour
{
    // Define several levels as arrays of strings.
    // Rows are read top-to-bottom. We flip the y coordinate so the top row appears at the top.
    // '.' = not part of level; '0' = valid cell; '1' = left player's start; '2' = right player's start.
    private List<string[]> levels = new List<string[]>()
    {
        new string[]
        {
            "100002", // 3
        },
        new string[]
        {
            "1",
            "0",
            "0",
            "2", // 2
        },
        new string[]
        {
            "1000002.", // 2
        },
        new string[]
        {
            "..0.",
            "0020",
            "1000", // 4
        },
        new string[]
        {
            "...002",
            ".00000",
            ".10000", // 3
        },
        new string[]
        {
            "..00..",
            "001020", // 4
            "..00..",
        },
    };

    private int currentLevelIndex = 0;
    private int gridWidth;
    private int gridHeight;

    // Spacing between cells (e.g., in pixels)
    public float cellSpacing = 150f;

    // Grid positions for the two players
    private Vector2Int leftPlayerPos;
    private Vector2Int rightPlayerPos;

    // References to the players' RectTransforms.
    // Assume the left player's RectTransform is on the same GameObject as this script.
    private RectTransform leftPlayerRect;
    private RectTransform leftMaskRect;
    public GameObject leftPlayer;
    public GameObject rightPlayer;
    private RectTransform rightPlayerRect;
    private RectTransform rightMaskRect;
    private List<List<int>> leftPlayerBounds;
    private List<List<int>> rightPlayerBounds;

    // Duplicate left player
    public RectTransform duplicateLeftPlayerRect;
    public RectTransform duplicateRightPlayerRect;

    // Prefab for a visual indicator of a valid grid cell.
    public GameObject positionPrefab;
    public GameObject deathPrefab;
    private List<GameObject> positionIndicators = new List<GameObject>();

    // Parent for the grid (assumed to be the same parent as the players)
    public RectTransform gridParent;

    // Occupancy grid: true indicates a cell is part of the level.
    private bool[,] occupancyGrid;

    // List of level backgrounds
    public GameObject[] levelBackgrounds;
    public GameObject verticalBackground;
    public GameObject horizontalBackground;

    private Animator animatorLeft;
    private Animator animatorRight;
    private bool isMoving = false;

    public AudioSource bounceSound;
    public AudioSource victorySound;

    public Animator animatorHeartDark;
    public Animator animatorHeartLight;

    // Win screen
    public GameObject winScreen;
    private int moveCounter = 0;
    public TextMeshProUGUI moveCounterText;

    void Awake()
    {
        Debug.Assert(rightPlayer != null, "Right player is not set.");
        leftPlayerRect = leftPlayer.GetComponent<RectTransform>();
        rightPlayerRect = rightPlayer.GetComponent<RectTransform>();
        leftMaskRect = leftPlayer.transform.parent.gameObject.GetComponent<RectTransform>();
        rightMaskRect = rightPlayer.transform.parent.gameObject.GetComponent<RectTransform>();
        
        LoadLevel(currentLevelIndex);
    }

    void Start()
    {
        animatorLeft = leftPlayer.GetComponent<Animator>();
        animatorRight = rightPlayer.GetComponent<Animator>();
        FlipAnimation(true, rightPlayerRect);
    }

    void Update()
    {
        // Optional: press L to switch to the next level manually.
        if (Input.GetKeyDown(KeyCode.L))
        {
            AnimateAdvanceLevel();
        }

        if (isMoving)
        {
            return;
        }

        // Horizontal movement for left player: valid x range [0, gridHalfWidth-1]
        // For right player: valid x range [gridHalfWidth, gridWidth-1] (mirrored movement)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            // Left player moves left.
            leftPlayerPos = FindNextValidHorizontal(leftPlayerPos, -1, leftPlayerBounds[0][0], leftPlayerBounds[0][1]);
            // Right player moves right (mirrored).
            rightPlayerPos = FindNextValidHorizontal(rightPlayerPos, 1, rightPlayerBounds[0][0], rightPlayerBounds[0][1]);
            AnimatePlayerPositions(Direction.Left);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            // If the left player is at the leftmost cell of its half and the right player
            // is at the rightmost cell of its half, and we press right, advance to the next level.
            bool isWin = leftPlayerPos.x + 1 == rightPlayerPos.x && leftPlayerPos.y == rightPlayerPos.y && currentLevelIndex != 1;

            // Otherwise, perform the normal rightward movement.
            leftPlayerPos = FindNextValidHorizontal(leftPlayerPos, 1, leftPlayerBounds[0][0], leftPlayerBounds[0][1]);
            rightPlayerPos = FindNextValidHorizontal(rightPlayerPos, -1, rightPlayerBounds[0][0], rightPlayerBounds[0][1]);
            AnimatePlayerPositions(Direction.Right, isWin);

            if (isWin)
                StartCoroutine(AnimateAdvanceLevel());
        }
        // Vertical movement for both players: full vertical range [0, gridHeight - 1]
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            leftPlayerPos = FindNextValidVertical(leftPlayerPos, 1, leftPlayerBounds[1][0], leftPlayerBounds[1][1]);
            // Note: right player moves in the opposite vertical direction.
            rightPlayerPos = FindNextValidVertical(rightPlayerPos, -1, rightPlayerBounds[1][0], rightPlayerBounds[1][1]);
            AnimatePlayerPositions(Direction.Up);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            bool isWin = leftPlayerPos.y == rightPlayerPos.y + 1 && rightPlayerPos.x == leftPlayerPos.x && currentLevelIndex == 1;

            leftPlayerPos = FindNextValidVertical(leftPlayerPos, -1, leftPlayerBounds[1][0], leftPlayerBounds[1][1]);
            rightPlayerPos = FindNextValidVertical(rightPlayerPos, 1, rightPlayerBounds[1][0], rightPlayerBounds[1][1]);
            AnimatePlayerPositions(Direction.Down, isWin);

            if (isWin)
                StartCoroutine(AnimateAdvanceLevel());
        }
    }

    /// <summary>
    /// Advances to the next level.
    /// </summary>
    IEnumerator AnimateAdvanceLevel()
    {
        yield return new WaitForSeconds(0.12f);
        victorySound.Play();
        animatorHeartDark.SetTrigger("play");
        animatorHeartLight.SetTrigger("play");
        rightPlayer.SetActive(false);
        leftPlayer.SetActive(false);
        yield return new WaitForSeconds(1.0f);
        AnimateVictoryScreen();
    }

    void AdvanceLevel()
    {
        moveCounter = 0;
        currentLevelIndex = (currentLevelIndex + 1) % levels.Count;
        LoadLevel(currentLevelIndex);
    }

    void AnimateVictoryScreen()
    {
        moveCounterText.text = "Moves: " + moveCounter;
        winScreen.SetActive(true);
    }

    public void OnNextClick()
    {
        winScreen.SetActive(false);
        AdvanceLevel();
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

        Debug.Assert(gridWidth > 0 && gridHeight > 0, "Level layout is empty.");
        if (currentLevelIndex == 1)
        {
            Debug.Assert(gridHeight % 2 == 0, "Grid height must be even for vertical levels.");
            int gridHalfHeight = gridHeight / 2;
            leftPlayerBounds = new List<List<int>>(){
                new List<int>(){0, gridWidth - 1},
                new List<int>(){gridHalfHeight, gridHeight - 1}
            };
            rightPlayerBounds = new List<List<int>>(){
                new List<int>(){0, gridWidth - 1},
                new List<int>(){0, gridHalfHeight - 1}
            };
        } else {
            Debug.Assert(gridWidth % 2 == 0, "Grid width must be even for horizontal levels.");
            int gridHalfWidth = gridWidth / 2;
            leftPlayerBounds = new List<List<int>>(){
                new List<int>(){0, gridHalfWidth - 1},
                new List<int>(){0, gridHeight - 1}
            };
            rightPlayerBounds = new List<List<int>>(){
                new List<int>(){gridHalfWidth, gridWidth - 1},
                new List<int>(){0, gridHeight - 1}
            };
        }

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
                else if (cell == 'X')
                {
                    if (deathPrefab != null) {
                        GameObject deathIndicator = Instantiate(deathPrefab, gridParent);
                        RectTransform deathRect = deathIndicator.GetComponent<RectTransform>();
                        deathRect.anchoredPosition = GetAnchoredPosition(gridPos);
                        positionIndicators.Add(deathIndicator);
                    }
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
        leftMaskRect.anchoredPosition = GetAnchoredPosition(leftPlayerPos);
        rightMaskRect.anchoredPosition = GetAnchoredPosition(rightPlayerPos);
        leftPlayer.SetActive(true);
        rightPlayer.SetActive(true);
    }

    void AnimatePlayerPositions(Direction direction, bool isOneSided = false)
    {
        moveCounter += 1;
        bounceSound.Play();
        
        FlipAnimation(direction == Direction.Right, leftPlayerRect);
        FlipAnimation(direction == Direction.Right, duplicateLeftPlayerRect);
        animatorLeft.SetTrigger("jump");

        FlipAnimation(direction == Direction.Left, rightPlayerRect);
        FlipAnimation(direction == Direction.Left, duplicateRightPlayerRect);
        animatorRight.SetTrigger("jump");

        Vector2 newLeftPos = GetAnchoredPosition(leftPlayerPos);
        Vector2 newRightPos = GetAnchoredPosition(rightPlayerPos);
        if (direction == Direction.Left && newLeftPos.x >= leftMaskRect.anchoredPosition.x)
        {   
            // Duplicate here refers to the duplicate of each side's cat that is used to animate the jump.
            // The duplicate is only visible during the jump animation.
            StartCoroutine(MovePlayerWithDuplicate(
                leftPlayerRect,
                isOneSided ? null : duplicateLeftPlayerRect,
                new Vector2(-cellSpacing, 0),
                new Vector2(cellSpacing, 0),
                GetAnchoredPosition(leftPlayerPos),
                0.25f,
                5
            ));
        }
        else if (direction == Direction.Right && newLeftPos.x <= leftMaskRect.anchoredPosition.x)
        {
            StartCoroutine(MovePlayerWithDuplicate(
                leftPlayerRect,
                isOneSided ? null : duplicateLeftPlayerRect,
                new Vector2(cellSpacing, 0),
                new Vector2(-cellSpacing, 0),
                GetAnchoredPosition(leftPlayerPos),
                0.25f,
                5
            ));
        }
        else
        {
            StartCoroutine(MovePlayers(newLeftPos, leftMaskRect, 0.25f, 5));
        }

        if (direction == Direction.Left && newRightPos.x <= rightMaskRect.anchoredPosition.x)
        {
            StartCoroutine(MovePlayerWithDuplicate(
                rightPlayerRect,
                isOneSided ? null : duplicateRightPlayerRect,
                new Vector2(cellSpacing, 0),
                new Vector2(-cellSpacing, 0),
                GetAnchoredPosition(rightPlayerPos),
                0.25f,
                5
            ));
        } else if (direction == Direction.Right && newRightPos.x >= rightMaskRect.anchoredPosition.x)
        {
            StartCoroutine(MovePlayerWithDuplicate(
                rightPlayerRect,
                isOneSided ? null : duplicateRightPlayerRect,
                new Vector2(-cellSpacing, 0),
                new Vector2(cellSpacing, 0),
                GetAnchoredPosition(rightPlayerPos),
                0.25f,
                5
            ));
        }
        else
        {
            StartCoroutine(MovePlayers(newRightPos, rightMaskRect, 0.25f, 5));
        }
    }

    IEnumerator MovePlayers(Vector2 target, RectTransform rect, float duration, int steps = 10)
    {
        isMoving = true;
        Vector2 targetPos = rect.anchoredPosition;
        float stepDuration = duration / steps;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps; // Discrete step progress
            rect.anchoredPosition = Vector2.Lerp(targetPos, target, t);
            yield return new WaitForSeconds(stepDuration);
        }
        // Ensure final positions are exact.
        rect.anchoredPosition = target;
        isMoving = false;
    }

    IEnumerator MovePlayerWithDuplicate(
        RectTransform mainRect,
        RectTransform duplicateRect,
        Vector2 deltaToTarget,       // Delta for both cats to move by INSIDE of their clipping masks
        Vector2 duplicateInStart,    // Position that the duplicate starts from INSIDE of the clipping mask
        Vector2 duplicateOutPos,   // The final onscreen position for the clipping mask itself
        float duration, 
        int steps = 10)
    {
        isMoving = true;

        // Ensure the duplicate is visible.
        if (duplicateRect != null)
        {
            duplicateRect.gameObject.SetActive(true);
            duplicateRect.GetComponent<Animator>().SetTrigger("jump");
            duplicateRect.GetComponent<RectTransform>().parent.gameObject.GetComponent<RectTransform>().anchoredPosition = duplicateOutPos;
        }

        Vector2 mainStart = mainRect.anchoredPosition;
        Vector2 mainTarget = mainStart + deltaToTarget;
        Vector2 duplicateStart = duplicateInStart;
        Vector2 duplicateTarget = duplicateInStart + deltaToTarget;
        float stepDuration = duration / steps;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            // Lerp the main cat to its offscreen target.
            mainRect.anchoredPosition = Vector2.Lerp(mainStart, mainTarget, t);
            // Lerp the duplicate from its offscreen start to its final onscreen position.
            if (duplicateRect != null)
                duplicateRect.anchoredPosition = Vector2.Lerp(duplicateStart, duplicateTarget, t);
            yield return new WaitForSeconds(stepDuration);
        }

        // Ensure final positions.
        mainRect.GetComponent<RectTransform>().parent.gameObject.GetComponent<RectTransform>().anchoredPosition = duplicateOutPos;
        mainRect.anchoredPosition = duplicateTarget;

        // Optionally, disable the duplicate after the transition.
        if (duplicateRect != null)
        {
            duplicateRect.gameObject.SetActive(false);
        }

        isMoving = false;
    }

    // Assuming leftPlayerRect is your player's RectTransform.
    // TODO: do this for rightPlayerRect too
    void FlipAnimation(bool flip, RectTransform playerRect)
    {
        Vector3 scale = playerRect.localScale;
        // If flip is true, set x to negative to mirror the animation.
        scale.x = Mathf.Abs(scale.x) * (flip ? -1 : 1);
        playerRect.localScale = scale;
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
    Vector2Int FindNextValidVertical(Vector2Int start, int direction, int minY, int maxY)
    {
        Vector2Int candidate = start;
        // Loop at most the gridHeight times.
        for (int i = 0; i < (maxY - minY + 1); i++)
        {
            candidate.y += direction;
            if (candidate.y < minY)
                candidate.y = maxY;
            else if (candidate.y > maxY)
                candidate.y = minY;
            if (occupancyGrid[candidate.x, candidate.y])
                return candidate;
        }
        return start;
    }
}