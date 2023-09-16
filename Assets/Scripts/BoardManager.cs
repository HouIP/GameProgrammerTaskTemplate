using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Gpt4All;
using TMPro;
using UnityEditor;
using Random = System.Random;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; set; }
    private bool[,] allowedMoves { get; set; }

    private const float TILE_SIZE = 1.0f;
    private const float TILE_OFFSET = 0.5f;

    private int selectionX = -1;
    private int selectionY = -1;

    public List<GameObject> chessmanPrefabs;
    private List<GameObject> activeChessman;

    private Quaternion whiteOrientation = Quaternion.Euler(0, 270, 0);
    private Quaternion blackOrientation = Quaternion.Euler(0, 90, 0);

    public Chessman[,] Chessmans { get; set; }
    private Chessman selectedChessman;

    public bool isWhiteTurn = true;

    private Material previousMat;
    public Material selectedMat;

    public int[] EnPassantMove { set; get; }

    public LlmManager manager;

    [Header("UI")]
    public GameObject chatBubbleUI;
    public GameObject personalityUI;
    public TextMeshProUGUI personalityText;
    public TextMeshProUGUI output;
    public bool isControlLocked = false;

    // Use this for initialization
    void Start()
    {
        Instance = this;
        SpawnAllChessmans();
        EnPassantMove = new int[2] { -1, -1 };

        output.text = "As the commander of a medieval army, you must maintain your patience, for the battlefield delivers news of each encounter in due time. Remember, each chess piece possesses its own unique personality. Expected the unexpected! Now, lead with wisdom and cunning! \n\nWhite Turn\n";
    }


    // Update is called once per frame
    void Update()
    {
        if (isControlLocked) return;

        UpdateSelection();

        if (Input.GetMouseButtonDown(0))
        {
            if (selectionX >= 0 && selectionY >= 0)
            {
                if (selectedChessman == null && !isControlLocked)
                {
                    // Select the chessman
                    SelectChessman(selectionX, selectionY);
                }
                else
                {
                    // Move the chessman
                    MoveChessman(selectionX, selectionY);
                }
                updatePersonalityUI(selectionX, selectionY);
            }
        }

        if (Input.GetKey("escape"))
            Application.Quit();
    }

    private void updatePersonalityUI(int x, int y)
    {
        if (Chessmans[x, y] != null)
        {
            Transform ChessmanTransform = Chessmans[x, y].transform;

            if (!personalityUI.activeSelf) personalityUI.SetActive(true);
            personalityUI.transform.position =
                new Vector3(ChessmanTransform.position.x, 1.5f, ChessmanTransform.position.z);

            // UI height override for king, queen
            if (Chessmans[x, y].GetType() == typeof(King) || Chessmans[x, y].GetType() == typeof(Queen) ||
                Chessmans[x, y].GetType() == typeof(Bishop))
                personalityUI.transform.position =
                    new Vector3(ChessmanTransform.position.x, 2.25f, ChessmanTransform.position.z);
            personalityText.text = Chessmans[x, y].Personality;
        }
    }

    private void SelectChessman(int x, int y)
    {
        if (Chessmans[x, y] == null) return;

        if (Chessmans[x, y].isWhite != isWhiteTurn) return;

        bool hasAtLeastOneMove = false;

        allowedMoves = Chessmans[x, y].PossibleMoves();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (allowedMoves[i, j])
                {
                    hasAtLeastOneMove = true;
                    i = 8;
                    break;
                }
            }
        }

        if (!hasAtLeastOneMove)
            return;

        chatBubbleUI.SetActive(true);
        chatBubbleUI.transform.position = new Vector3(x, 0.5f, y);

        selectedChessman = Chessmans[x, y];
        previousMat = selectedChessman.GetComponent<MeshRenderer>().material;
        selectedMat.mainTexture = previousMat.mainTexture;
        selectedChessman.GetComponent<MeshRenderer>().material = selectedMat;

        BoardHighlights.Instance.HighLightAllowedMoves(allowedMoves);
    }

    // get the surrounding chessmen of a given chessman
    private List<Chessman> GetSurroundingChessmen(int x, int y)
    {
        List<Chessman> surroundingChessmen = new ();

        // Define relative coordinates for surrounding tiles
        int[] dx = { -1, 0, 1, 0 }; // Left, Up, Right, Down
        int[] dy = { 0, -1, 0, 1 };

        for (int i = 0; i < dx.Length; i++)
        {
            int newX = x + dx[i];
            int newY = y + dy[i];

            // Check if the new coordinates are within the grid
            if (newX >= 0 && newX < 8 && newY >= 0 && newY < 8)
            {
                //Debug.Log("x: " + newX + " y: " + newY);
                surroundingChessmen.Add(Chessmans[x, y]);
            }
        }

        return surroundingChessmen;
    }

    private bool calculateVictory()
    {
        var rng = new System.Random();
        return rng.Next(0, 100) < 50;
    }

    private async Task<bool> CalculateVictoryAsync(Chessman attackerChessman, Chessman defenderChessman)
    {
        string attacker = attackerChessman.Personality + " " + attackerChessman.GetType().ToString();
        string defender = defenderChessman.Personality + " " + defenderChessman.GetType().ToString();

        personalityUI.SetActive(false);
        isControlLocked = true;
        string fightDescription = "You command <color=green>" + attacker.ToLower() + "</color=green> launch a attack to the enemy <color=red>" + defender.ToLower() + "</color=red>!";

        output.text += fightDescription + "\n";
        ChatBubble.Instance.ShowDialogue("Waiting for result...", attackerChessman.transform.position);

        string fightPrompt = "Determine the outcome of the following fight from " + attacker + " to " + defender + ". State the outcome of the fight, only say win or lost. Do not response with anything else";

        string rawResponse = await manager.Prompt(fightPrompt);

        bool result;
        if (rawResponse.Contains("win"))
        {
            result = true;
            string winPrompt = "Write a short line speaking like a " + attacker + " attacking " + defender + "in a medieval war";
            rawResponse = await manager.Prompt(winPrompt);

            output.text += "<color=green>" + attacker + "</color=green> win the fight!\n\n";
            ChatBubble.Instance.ShowDialogue(rawResponse.Trim('"'), attackerChessman.transform.position);
        }
        else
        {
            result = false;
            string losePrompt = "Write a short line speaking like a " + defender + " successfully fights back " + attacker + " in a medieval war";
            rawResponse = await manager.Prompt(losePrompt);

            output.text += "<color=green>" + attacker + "</color=green> lost the fight!\n\n";
            ChatBubble.Instance.ShowDialogue(rawResponse.Trim('"'), defenderChessman.transform.position);
        }

        while (ChatBubble.Instance.isWriting)
        {
            await Task.Delay(2000);
        }
        ChatBubble.Instance.CloseDialogue();
        isControlLocked = false;
        return result;
    }

    private async void MoveChessman(int x, int y)
    {
        if (allowedMoves[x, y])
        {
            Chessman c = Chessmans[x, y];
            var victory = true;

            if (c != null && c.isWhite != isWhiteTurn)
            {
                // Capture a piece

                if (c.GetType() == typeof(King))
                {
                    // End the game
                    EndGame();
                    return;
                }

                BoardHighlights.Instance.HideHighlights();
                personalityUI.SetActive(false);
                victory = await CalculateVictoryAsync(selectedChessman, c);
                //victory = calculateVictory();
                if (victory)
                {
                    activeChessman.Remove(c.gameObject);
                    Destroy(c.gameObject);
                }
                else
                {
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                }
            }
            if (x == EnPassantMove[0] && y == EnPassantMove[1])
            {
                if (isWhiteTurn)
                    c = Chessmans[x, y - 1];
                else
                    c = Chessmans[x, y + 1];

                activeChessman.Remove(c.gameObject);
                Destroy(c.gameObject);
            }
            EnPassantMove[0] = -1;
            EnPassantMove[1] = -1;
            if (selectedChessman.GetType() == typeof(Pawn))
            {
                string personality = selectedChessman.Personality;
                if (y == 7) // White Promotion
                {
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                    SpawnChessman(1, x, y, true, personality);
                    selectedChessman = Chessmans[x, y];
                }
                else if (y == 0) // Black Promotion
                {
                    activeChessman.Remove(selectedChessman.gameObject);
                    Destroy(selectedChessman.gameObject);
                    SpawnChessman(7, x, y, false, personality);
                    selectedChessman = Chessmans[x, y];
                }
                EnPassantMove[0] = x;
                if (selectedChessman.CurrentY == 1 && y == 3)
                    EnPassantMove[1] = y - 1;
                else if (selectedChessman.CurrentY == 6 && y == 4)
                    EnPassantMove[1] = y + 1;
            }

            if (victory)
            {
                Chessmans[selectedChessman.CurrentX, selectedChessman.CurrentY] = null;
                selectedChessman.transform.position = GetTileCenter(x, y);
                selectedChessman.SetPosition(x, y);
                Chessmans[x, y] = selectedChessman;
                updatePersonalityUI(x, y);
            }

            isWhiteTurn = !isWhiteTurn;
            if (isWhiteTurn) output.text += "White Turn\n";
            else output.text += "Black Turn\n";
        }
        selectedChessman.GetComponent<MeshRenderer>().material = previousMat;

        BoardHighlights.Instance.HideHighlights();
        selectedChessman = null;
    }

    private void UpdateSelection()
    {
        if (!Camera.main) return;

        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 50.0f, LayerMask.GetMask("ChessPlane")))
        {
            selectionX = (int)hit.point.x;
            selectionY = (int)hit.point.z;
        }
        else
        {
            selectionX = -1;
            selectionY = -1;
        }
    }

    private void SpawnChessman(int index, int x, int y, bool isWhite, string personality)
    {
        Vector3 position = GetTileCenter(x, y);
        GameObject go;

        if (isWhite)
        {
            go = Instantiate(chessmanPrefabs[index], position, whiteOrientation) as GameObject;
        }
        else
        {
            go = Instantiate(chessmanPrefabs[index], position, blackOrientation) as GameObject;
        }

        go.transform.SetParent(transform);
        Chessmans[x, y] = go.GetComponent<Chessman>();
        Chessmans[x, y].SetPosition(x, y);
        Chessmans[x,y].SetPersonality(personality);
        activeChessman.Add(go);
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        Vector3 origin = Vector3.zero;
        origin.x += (TILE_SIZE * x) + TILE_OFFSET;
        origin.z += (TILE_SIZE * y) + TILE_OFFSET;

        return origin;
    }

    private void SpawnAllChessmans()
    {
        activeChessman = new List<GameObject>();
        Chessmans = new Chessman[8, 8];

        string[] personalities = { "Brave", "Coward", "Loyal", "Treacherous", "Honest", "Deceitful", "Merciful", "Cruel" };

        /////// White ///////

        // King
        SpawnChessman(0, 3, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Queen
        SpawnChessman(1, 4, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Rooks
        SpawnChessman(2, 0, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(2, 7, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Bishops
        SpawnChessman(3, 2, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(3, 5, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Knights
        SpawnChessman(4, 1, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(4, 6, 0, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Pawns
        for (int i = 0; i < 8; i++)
        {
            SpawnChessman(5, i, 1, true, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        }


        /////// Black ///////

        // King
        SpawnChessman(6, 4, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Queen
        SpawnChessman(7, 3, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Rooks
        SpawnChessman(8, 0, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(8, 7, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Bishops
        SpawnChessman(9, 2, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(9, 5, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Knights
        SpawnChessman(10, 1, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        SpawnChessman(10, 6, 7, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);

        // Pawns
        for (int i = 0; i < 8; i++)
        {
            SpawnChessman(11, i, 6, false, personalities[UnityEngine.Random.Range(0, personalities.Length)]);
        }
    }

    private void EndGame()
    {
        if (isWhiteTurn)
        {
            Debug.Log("White wins");
            output.text += "White wins! \n\n\n";
        }
        else
        {
            Debug.Log("Black wins");
            output.text += "Black wins! \n\n\n";
        }

        foreach (GameObject go in activeChessman)
        {
            Destroy(go);
        }

        isWhiteTurn = true;
        BoardHighlights.Instance.HideHighlights();
        SpawnAllChessmans();
    }
}


