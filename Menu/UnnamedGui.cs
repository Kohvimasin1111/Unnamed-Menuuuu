using Photon.Pun;
using GorillaNetworking;
using UnityEngine;

public class UnnamedGui : MonoBehaviour
{
    // ============================================================
    // INSTANCE
    // ============================================================

    public static UnnamedGui Instance;

    private Rect windowRect =
        new Rect(
            30f,
            30f,
            320f,
            300f
        );

    private string roomCode = "";

    private GUIStyle windowStyle;
    private GUIStyle labelStyle;
    private GUIStyle textFieldStyle;
    private GUIStyle buttonStyle;

    private Texture2D windowTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;
    private Texture2D textFieldTexture;

    private bool stylesCreated;

    // ============================================================
    // INITIALIZE
    // ============================================================

    public static void Initialize()
    {
        if (Instance != null)
            return;

        GameObject obj =
            new GameObject("UnnamedGui");

        Object.DontDestroyOnLoad(obj);

        Instance =
            obj.AddComponent<UnnamedGui>();
    }

    // ============================================================
    // PC GUI
    // ============================================================

    private void OnGUI()
    {
        // Starts OFF because UnnamedGuiEnabled is false
        // in Settings.Menu.
        if (!StupidTemplate.Mods.Settings.Menu.UnnamedGuiEnabled)
            return;

        CreateStyles();

        windowRect = GUI.Window(
            69420,
            windowRect,
            DrawWindow,
            "Unnamed Gui",
            windowStyle
        );
    }

    // ============================================================
    // WINDOW
    // ============================================================

    private void DrawWindow(int windowID)
    {
        GUILayout.Space(8);

        // ========================================================
        // ROOM CODE
        // ========================================================

        GUILayout.Label(
            "Room Code",
            labelStyle
        );

        roomCode = GUILayout.TextField(
            roomCode,
            20,
            textFieldStyle,
            GUILayout.Height(30)
        );

        GUILayout.Space(5);

        if (GUILayout.Button(
            "JOIN ROOM",
            buttonStyle,
            GUILayout.Height(32)
        ))
        {
            JoinRoom();
        }

        GUILayout.Space(20);

        // ========================================================
        // ROOM INFORMATION
        // ========================================================

        string lobbyName = "Not In Room";
        string playerAmount = "0 / 0";

        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom != null)
        {
            lobbyName =
                PhotonNetwork.CurrentRoom.Name;

            playerAmount =
                PhotonNetwork.CurrentRoom.PlayerCount +
                " / " +
                PhotonNetwork.CurrentRoom.MaxPlayers;
        }

        GUILayout.Label(
            "Lobby: " + lobbyName,
            labelStyle
        );

        GUILayout.Space(4);

        GUILayout.Label(
            "Players: " + playerAmount,
            labelStyle
        );

        GUILayout.FlexibleSpace();

        // ========================================================
        // DISCONNECT
        // ========================================================

        GUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
            "DISCONNECT",
            buttonStyle,
            GUILayout.Width(150f),
            GUILayout.Height(35f)
        ))
        {
            Disconnect();
        }

        GUILayout.FlexibleSpace();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // ========================================================
        // DRAG WINDOW
        // ========================================================

        GUI.DragWindow();
    }

    // ============================================================
    // JOIN ROOM
    // ============================================================

    private void JoinRoom()
    {
        string code =
            roomCode.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
            return;

        if (PhotonNetworkController.Instance == null)
            return;

        PhotonNetworkController.Instance
            .AttemptToJoinSpecificRoom(
                code,
                GorillaNetworking.JoinType.Solo
            );
    }

    // ============================================================
    // DISCONNECT
    // ============================================================

    private void Disconnect()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }

    // ============================================================
    // GUI STYLES
    // ============================================================

    private void CreateStyles()
    {
        if (stylesCreated)
            return;

        // ========================================================
        // WINDOW BACKGROUND
        // ========================================================

        windowTexture =
            MakeTexture(
                new Color32(
                    25,
                    25,
                    25,
                    255
                )
            );

        windowStyle =
            new GUIStyle(
                GUI.skin.window
            );

        windowStyle.fontSize = 16;

        windowStyle.alignment =
            TextAnchor.UpperCenter;

        windowStyle.normal.background =
            windowTexture;

        windowStyle.hover.background =
            windowTexture;

        windowStyle.active.background =
            windowTexture;

        windowStyle.focused.background =
            windowTexture;

        windowStyle.onNormal.background =
            windowTexture;

        windowStyle.onHover.background =
            windowTexture;

        windowStyle.onActive.background =
            windowTexture;

        windowStyle.onFocused.background =
            windowTexture;

        windowStyle.normal.textColor =
            Color.white;

        windowStyle.hover.textColor =
            Color.white;

        windowStyle.active.textColor =
            Color.white;

        windowStyle.focused.textColor =
            Color.white;

        windowStyle.onNormal.textColor =
            Color.white;

        windowStyle.onHover.textColor =
            Color.white;

        windowStyle.onActive.textColor =
            Color.white;

        windowStyle.onFocused.textColor =
            Color.white;

        // ========================================================
        // LABELS
        // ========================================================

        labelStyle =
            new GUIStyle(
                GUI.skin.label
            );

        labelStyle.fontSize = 14;

        labelStyle.normal.textColor =
            Color.white;

        // ========================================================
        // TEXT FIELD
        // ========================================================

        textFieldTexture =
            MakeTexture(
                new Color32(
                    25,
                    25,
                    25,
                    255
                )
            );

        textFieldStyle =
            new GUIStyle(
                GUI.skin.textField
            );

        textFieldStyle.fontSize = 14;

        textFieldStyle.normal.background =
            textFieldTexture;

        textFieldStyle.focused.background =
            textFieldTexture;

        textFieldStyle.normal.textColor =
            Color.white;

        textFieldStyle.focused.textColor =
            Color.white;

        // ========================================================
        // BUTTONS
        // ========================================================

        buttonTexture =
            MakeTexture(
                new Color32(
                    55,
                    55,
                    55,
                    255
                )
            );

        buttonHoverTexture =
            MakeTexture(
                new Color32(
                    70,
                    70,
                    70,
                    255
                )
            );

        buttonStyle =
            new GUIStyle(
                GUI.skin.button
            );

        buttonStyle.fontSize = 13;

        buttonStyle.alignment =
            TextAnchor.MiddleCenter;

        buttonStyle.normal.background =
            buttonTexture;

        buttonStyle.hover.background =
            buttonHoverTexture;

        buttonStyle.active.background =
            buttonHoverTexture;

        buttonStyle.focused.background =
            buttonTexture;

        buttonStyle.onFocused.background =
            buttonTexture;

        buttonStyle.normal.textColor =
            Color.white;

        buttonStyle.hover.textColor =
            Color.white;

        buttonStyle.active.textColor =
            Color.white;

        stylesCreated = true;
    }

    // ============================================================
    // TEXTURE CREATOR
    // ============================================================

    private Texture2D MakeTexture(Color color)
    {
        Texture2D texture =
            new Texture2D(
                1,
                1
            );

        texture.SetPixel(
            0,
            0,
            color
        );

        texture.Apply();

        return texture;
    }

    // ============================================================
    // END
    // ============================================================
}