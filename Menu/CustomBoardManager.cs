using GorillaNetworking;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace iiMenu.Managers
{
    public class CustomBoardManager : MonoBehaviour
    {
        public static CustomBoardManager instance;

        private static Material _boardMaterial;

        public static bool CustomBoardsEnabled = true;
        public static bool CustomBoardTextEnabled = true;

        public Material forestMaterial;
        public Material stumpMaterial;

        public GameObject motdTitle;
        public GameObject motdText;
        public GameObject computerMonitor;

        public readonly Dictionary<string, GameObject> objectBoards =
            new Dictionary<string, GameObject>();

        public readonly List<TextMeshPro> textMeshPro =
            new List<TextMeshPro>();

        private bool hasFoundAllBoards;

        public const int StumpLeaderboardIndex = 3;

        
        public static Material BoardMaterial
        {
            get
            {
                if (_boardMaterial == null)
                {
                    _boardMaterial =
                        new Material(Shader.Find("GorillaTag/UberShader"));

                    _boardMaterial.color = Color.black;
                }

                return _boardMaterial;
            }

            set
            {
                _boardMaterial = value;

                if (_boardMaterial == null)
                {
                    _boardMaterial =
                        new Material(Shader.Find("GorillaTag/UberShader"));

                    _boardMaterial.color = Color.black;
                }

                if (instance != null)
                    instance.ReloadBoards();
            }
        }

        private static GameObject GetObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('/');

            if (parts.Length == 0)
                return null;

            GameObject current = null;

            foreach (GameObject root in
                     SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null)
                return null;

            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);

                if (child == null)
                    return null;

                current = child.gameObject;
            }

            return current;
        }

        private void Awake()
        {
            instance = this;

            if (_boardMaterial == null)
            {
                _boardMaterial =
                    new Material(Shader.Find("GorillaTag/UberShader"));

                _boardMaterial.color = Color.black;
            }

            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
        }

        public void ReloadBoards()
        {
            hasFoundAllBoards = false;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            hasFoundAllBoards = false;

            if (!CustomBoardsEnabled)
                return;

            BoardInformation info;

            if (!BoardInformations.TryGetValue(scene.name, out info))
                return;

            CreateObjectBoard(
                scene.name,
                info.GameObjectPath,
                info.Position,
                info.Rotation,
                info.Scale
            );
        }

        private void Update()
        {
            if (!hasFoundAllBoards)
                FindBoards();

            UpdateBoardColor();
            UpdateMOTD();
            UpdateCOCText();
        }

        private void FindBoards()
        {
            try
            {
                // Remove our previously-created map boards.
                foreach (GameObject board in objectBoards.Values)
                {
                    if (board != null)
                        Destroy(board);
                }

                objectBoards.Clear();

                /*
                 * ONLY change the actual stump leaderboard.
                 *
                 * We DO NOT touch the Forest UnityTempFile objects.
                 * This is what prevents the leaves/environment
                 * from changing color.
                 */

                GameObject treeRoom = GetObject(
                    "Environment Objects/LocalObjects_Prefab/TreeRoom"
                );

                if (treeRoom != null)
                {
                    Transform treeTransform = treeRoom.transform;

                    List<Transform> stumpChildren =
                        new List<Transform>();

                    for (int i = 0;
                         i < treeTransform.childCount;
                         i++)
                    {
                        Transform child =
                            treeTransform.GetChild(i);

                        if (child.name.Contains("UnityTempFile"))
                            stumpChildren.Add(child);
                    }

                    if (StumpLeaderboardIndex >= 0 &&
                        StumpLeaderboardIndex < stumpChildren.Count)
                    {
                        GameObject stump =
                            stumpChildren[StumpLeaderboardIndex].gameObject;

                        Renderer renderer =
                            stump.GetComponent<Renderer>();

                        if (renderer != null)
                        {
                            if (stumpMaterial == null)
                                stumpMaterial = renderer.material;

                            renderer.material = BoardMaterial;
                        }
                    }
                }

                /*
                 * DO NOT SEARCH FOR THE FOREST UnityTempFile OBJECTS.
                 *
                 * The old code did this and could recolor the leaves.
                 */

                computerMonitor = GetObject(
                    "Environment Objects/LocalObjects_Prefab/TreeRoom/TreeRoomInteractables/GorillaComputerObject/ComputerUI/monitor/monitorScreen"
                );

                if (computerMonitor != null)
                {
                    Renderer renderer =
                        computerMonitor.GetComponent<Renderer>();

                    if (renderer != null)
                        renderer.material = BoardMaterial;
                }

                FindTextObjects();

                hasFoundAllBoards = true;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[CustomBoardManager] " + e
                );

                hasFoundAllBoards = false;
            }
        }

        private void FindTextObjects()
        {
            string[] paths =
            {
                "Environment Objects/LocalObjects_Prefab/TreeRoom/CodeOfConductHeadingText",
                "Environment Objects/LocalObjects_Prefab/TreeRoom/COCBodyText_TitleData",
                "Environment Objects/LocalObjects_Prefab/TreeRoom/Data",
                "Environment Objects/LocalObjects_Prefab/TreeRoom/FunctionSelect"
            };

            foreach (string path in paths)
            {
                GameObject obj = GetObject(path);

                if (obj == null)
                    continue;

                TextMeshPro text =
                    obj.GetComponentInChildren<TextMeshPro>(true);

                if (text != null &&
                    !textMeshPro.Contains(text))
                {
                    textMeshPro.Add(text);
                }
            }
        }

        private void UpdateBoardColor()
        {
            if (BoardMaterial == null)
                return;

            if (CustomBoardsEnabled)
            {
                /*
                 * This uses your StupidTemplate backgroundColor.
                 *
                 * Set that gradient to black/red and the boards
                 * will animate between them.
                 */
                BoardMaterial.color =
                    StupidTemplate.Settings.backgroundColor
                        .GetCurrentColor();
            }
            else
            {
                BoardMaterial.color = Color.black;
            }

            if (computerMonitor != null)
            {
                Renderer renderer =
                    computerMonitor.GetComponent<Renderer>();

                if (renderer != null)
                    renderer.material = BoardMaterial;
            }
        }

        private void UpdateMOTD()
        {
            try
            {
                // MOTD HEADING
                if (motdTitle == null)
                {
                    GameObject original = GetObject(
                        "Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText"
                    );

                    if (original != null)
                    {
                        motdTitle =
                            Instantiate(
                                original,
                                original.transform.parent
                            );

                        original.SetActive(false);
                    }
                }

                if (motdTitle != null)
                {
                    TextMeshPro heading =
                        motdTitle.GetComponent<TextMeshPro>();

                    if (heading != null)
                    {
                        heading.richText = true;

                        // Smaller than before
                        heading.fontSize = 40;

                        heading.text =
                            "Unnamed Paid 1.0!";

                        heading.color = Color.white;
                    }
                }

                // MOTD BODY
                if (motdText == null)
                {
                    GameObject original = GetObject(
                        "Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText"
                    );

                    if (original != null)
                    {
                        motdText =
                            Instantiate(
                                original,
                                original.transform.parent
                            );

                        original.SetActive(false);

                        PlayFabTitleDataTextDisplay display =
                            motdText.GetComponent<
                                PlayFabTitleDataTextDisplay>();

                        if (display != null)
                            display.enabled = false;
                    }
                }

                if (motdText != null)
                {
                    TextMeshPro body =
                        motdText.GetComponent<TextMeshPro>();

                    if (body != null)
                    {
                        body.richText = true;

                        // Smaller body text
                        body.fontSize = 55;

                        body.text =
                            "\"You are using build {0}. This menu was created by \"Unnamed\" and \"Unnamed Ontop\" on Discord. \" +\r\n\"\" +\r\n\"<color=red>I, lucz1, am not responsible for any bans using this menu.</color> \" +\r\n\"If you get banned while using this, it's your responsibility.\\n\\n\\nMade with <3 by Unnamed,\\n\\n<alpha=128>{2} {0} {3}<alpha=255>\";.";

                        body.color = Color.white;
                    }
                }
            }
            catch
            {
            }
        }

        private void UpdateCOCText()
        {
            try
            {
                GameObject cocBody = GetObject(
                    "Environment Objects/LocalObjects_Prefab/TreeRoom/COCBodyText_TitleData"
                );

                GameObject cocHead = GetObject(
                    "Environment Objects/LocalObjects_Prefab/TreeRoom/CodeOfConductHeadingText"
                );

                if (cocBody != null)
                {
                    TMP_Text body =
                        cocBody.GetComponentInChildren<TMP_Text>(true);

                    if (body != null)
                    {
                        body.text =
                            "Made by Unnamed Projects, \n\nThanks to all of you for supporting Unnamed Projects\n\n=========================================\n\n100+ mods\n\nCreated BY:Unnamed on discord\n\nTested BY:Unnamed ontop on discord";

                        body.color = Color.white;
                    }
                }

                if (cocHead != null)
                {
                    TMP_Text head =
                        cocHead.GetComponentInChildren<TMP_Text>(true);

                    if (head != null)
                    {
                        head.text =
                            "Unnamed Paid 1.0!";

                        head.color = Color.white;
                    }
                }
            }
            catch
            {
            }
        }

        public static bool SetBoardsEnabled(bool enabled)
        {
            CustomBoardsEnabled = enabled;

            if (instance == null)
                return false;

            instance.ReloadBoards();

            if (instance.motdTitle != null)
                instance.motdTitle.SetActive(enabled);

            if (instance.motdText != null)
                instance.motdText.SetActive(enabled);

            GameObject originalHeading = GetObject(
                "Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText"
            );

            GameObject originalBody = GetObject(
                "Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText"
            );

            if (originalHeading != null)
                originalHeading.SetActive(!enabled);

            if (originalBody != null)
                originalBody.SetActive(!enabled);

            return true;
        }

        public void CreateObjectBoard(
            string scene,
            string gameObject,
            Vector3? position = null,
            Vector3? rotation = null,
            Vector3? scale = null)
        {
            try
            {
                GameObject existing;

                if (objectBoards.TryGetValue(
                    scene,
                    out existing))
                {
                    if (existing != null)
                        Destroy(existing);

                    objectBoards.Remove(scene);
                }

                GameObject parent =
                    GetObject(gameObject);

                if (parent == null)
                {
                    Debug.LogError(
                        "[CustomBoardManager] Could not find: " +
                        gameObject
                    );

                    return;
                }

                GameObject board =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Plane
                    );

                board.transform.SetParent(
                    parent.transform,
                    false
                );

                board.transform.localPosition =
                    position ??
                    new Vector3(
                        -22.1964f,
                        -34.9f,
                        0.57f
                    );

                board.transform.localRotation =
                    Quaternion.Euler(
                        rotation ??
                        new Vector3(
                            270f,
                            0f,
                            0f
                        )
                    );

                board.transform.localScale =
                    scale ??
                    new Vector3(
                        21.6f,
                        2.4f,
                        22f
                    );

                Collider collider =
                    board.GetComponent<Collider>();

                if (collider != null)
                    Destroy(collider);

                Renderer renderer =
                    board.GetComponent<Renderer>();

                if (renderer != null)
                    renderer.material =
                        BoardMaterial;

                objectBoards[scene] = board;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[CustomBoardManager] Failed creating board: " +
                    e
                );
            }
        }

        private readonly struct BoardInformation
        {
            public readonly string GameObjectPath;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;

            public BoardInformation(
                string path,
                Vector3 position,
                Vector3 rotation,
                Vector3 scale)
            {
                GameObjectPath = path;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        private static readonly Dictionary<string, BoardInformation>
            BoardInformations =
                new Dictionary<string, BoardInformation>
                {
                    ["Canyon2"] =
                        new BoardInformation(
                            "Canyon/CanyonScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-24.5019f, -28.7746f, 0.1f),
                            new Vector3(270f, 0f, 0f),
                            new Vector3(21.5946f, 1f, 22.1782f)
                        ),

                    ["Skyjungle"] =
                        new BoardInformation(
                            "skyjungle/UI/Scoreboard/GorillaScoreBoard",
                            new Vector3(-21.2764f, -32.1928f, 0f),
                            new Vector3(270.2987f, 0.2f, 359.9f),
                            new Vector3(21.6f, 0.1f, 20.4909f)
                        ),

                    ["Mountain"] =
                        new BoardInformation(
                            "Mountain/MountainScoreboardAnchor/GorillaScoreBoard",
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one
                        ),

                    ["Metropolis"] =
                        new BoardInformation(
                            "MetroMain/ComputerArea/Scoreboard/GorillaScoreBoard",
                            new Vector3(-25.1f, -31f, 0.1502f),
                            new Vector3(270.1958f, 0.2086f, 0f),
                            new Vector3(21f, 102.9727f, 21.4f)
                        ),

                    ["Bayou"] =
                        new BoardInformation(
                            "BayouMain/ComputerArea/GorillaScoreBoardPhysical",
                            new Vector3(-28.3419f, -26.851f, 0.3f),
                            new Vector3(270f, 0f, 0f),
                            new Vector3(21.3636f, 38f, 21f)
                        ),

                    ["Beach"] =
                        new BoardInformation(
                            "BeachScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-22.1964f, -33.7126f, 0.1f),
                            new Vector3(270.056f, 0f, 0f),
                            new Vector3(21.2f, 2f, 21.6f)
                        ),

                    ["Cave"] =
                        new BoardInformation(
                            "Cave_Main_Prefab/CrystalCaveScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-22.1964f, -33.7126f, 0.1f),
                            new Vector3(270.056f, 0f, 0f),
                            new Vector3(21.2f, 2f, 21.6f)
                        ),

                    ["Rotating"] =
                        new BoardInformation(
                            "RotatingPermanentEntrance/UI (1)/RotatingScoreboard/RotatingScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-22.1964f, -33.7126f, 0.1f),
                            new Vector3(270.056f, 0f, 0f),
                            new Vector3(21.2f, 2f, 21.6f)
                        ),

                    ["MonkeBlocks"] =
                        new BoardInformation(
                            "Environment Objects/MonkeBlocksRoomPersistent/AtticScoreBoard/AtticScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-22.1964f, -24.5091f, 0.57f),
                            new Vector3(270.1856f, 0.1f, 0f),
                            new Vector3(21.6f, 1.2f, 20.8f)
                        ),

                    ["Basement"] =
                        new BoardInformation(
                            "Basement/BasementScoreboardAnchor/GorillaScoreBoard/",
                            new Vector3(-22.1964f, -24.5091f, 0.57f),
                            new Vector3(270.1856f, 0.1f, 0f),
                            new Vector3(21.6f, 1.2f, 20.8f)
                        ),

                    ["City"] =
                        new BoardInformation(
                            "City_Pretty/CosmeticsScoreboardAnchor/GorillaScoreBoard",
                            new Vector3(-22.1964f, -34.9f, 0.57f),
                            new Vector3(270f, 0f, 0f),
                            new Vector3(21.6f, 2.4f, 22f)
                        )
                };
    }
}