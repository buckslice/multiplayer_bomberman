using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

public class LobbyUIController : MonoBehaviour {

    // lobby screen stuff
    public Text playerNamesText;
    public GameObject connectedPlayerNamesPanel;
    public Text chatLogText;
    public InputField chatInputBar;
    public InputField createRoomInputField;
    public Button createRoomButton;
    public Text roomTitleText;
    public GameObject roomListPanel;
    public GameObject roomPanel;
    public GameObject leaveRoomButton;
    public RectTransform roomContent;
    public RectTransform roomScrollView;

    public GameClient client { private get; set; }

    private bool firstChat = true;
    private float updateNamesTimer = 0.0f;

    private List<RoomPanelEditor> roomPanels;

    // Use this for initialization
    void Start() {
        chatInputBar.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update() {
        if (!client) {
            return;
        }

        // check if chatbar is being typed on
        if (Input.GetKeyDown(KeyCode.Return)) {
            chatInputBar.gameObject.SetActive(!chatInputBar.IsActive());
            if (chatInputBar.IsActive()) {
                chatInputBar.ActivateInputField();
            } else if (chatInputBar.text.Length != 0) {   // send message if just hit enter
                Packet p = new Packet(PacketType.CHAT_MESSAGE);
                PlayerInfo me = client.getMyPlayer();
                p.Write(me.name);
                p.Write(me.color);
                p.Write(chatInputBar.text);
                client.sendPacket(p);
                logChatMessage(me.name, me.color, chatInputBar.text);
                chatInputBar.text = "";
            }
        }

        // check if names should be updated
        updateNamesTimer -= Time.deltaTime;
        if (updateNamesTimer < 0.0f) {
            updateNamesTimer = 0.5f;
            StringBuilder sb = new StringBuilder();

            IList<PlayerInfo> playerInfos = client.getPlayerInfoList();
            for (int i = 0; i < playerInfos.Count; ++i) {
                PlayerInfo ps = playerInfos[i];
                sb.Append(getNameWithColor(ps.name, ps.color));
                sb.Append('\n');
            }
            playerNamesText.text = sb.ToString();
        }
    }

    // called when menu button is pressed
    public void tryCreateRoom() {
        string roomName = createRoomInputField.text;
        if (roomName != "") {
            client.tryChangeRoom(true, roomName);
            createRoomButton.interactable = false;
        } else {
            logError("Enter a room name!");
        }
    }

    public void tryChangeRoom(string roomName) {
        client.tryChangeRoom(false, roomName);
    }

    public void onChangeRoomFailure(bool createFail) {
        if (createFail) {
            createRoomButton.interactable = true;
            logError("Room name taken!");
        } else {
            logError("Unabled to join room");
        }
    }

    public void updateRoomUI(string roomName) {
        bool isLobby = roomName == "Lobby";
        if (isLobby) {
            roomTitleText.text = "Room List";
            logMessage("Joined <color=#7b00ff>Lobby</color>");
            createRoomButton.interactable = true;
        } else {
            string redRoomName = "<color=#ff0000>Room</color> " + roomName;
            logMessage("Joined " + redRoomName);
            roomTitleText.text = redRoomName;
        }
        roomScrollView.offsetMin = new Vector2(isLobby ? 250 : 0, 0);
        roomListPanel.SetActive(isLobby);
        roomPanel.SetActive(!isLobby);
        leaveRoomButton.SetActive(!isLobby);
    }

    public void roomContentChanged(int rows) {
        // to animate a new element popping in
        //Vector3 lpos = roomContent.localPosition;
        //lpos.y = 75;
        //roomContent.localPosition = lpos;

        // set height of element
        roomContent.sizeDelta = new Vector2(0, rows * 75);
    }

    public void updateRoomList(List<string> rooms) {
        roomContentChanged(rooms.Count + 1);

        // add or destroy RoomPanelPrefabs until same as number of rooms
        // go through and set stuff through scripts
    }

    public void logChatMessage(string name, Color32 color, string message) {
        StringBuilder sb = getChatLog();
        sb.Append("[");
        sb.Append(getNameWithColor(name, color));
        sb.Append("] ");
        sb.Append(message);
        updateChat(sb);
    }

    public void logConnectionMessage(string name, Color32 color, bool joined, bool server) {
        StringBuilder sb = getChatLog();
        if (server) {
            sb.Append("[<color=#ff0000>SERVER</color>] <");
        } else {
            sb.Append("<");
        }
        sb.Append(getNameWithColor(name, color));
        sb.Append("> ");
        if (server) {
            sb.Append(joined ? "connected" : "disconnected");
        } else {
            sb.Append(joined ? "joined the room" : "left the room");
        }
        updateChat(sb);
    }

    public void logMessage(string message) {
        StringBuilder sb = getChatLog();
        sb.Append(message);
        updateChat(sb);
    }

    public void logError(string message) {
        StringBuilder sb = getChatLog();
        sb.Append("<color=#ff00ff>[ERROR] ");
        sb.Append(message);
        sb.Append("</color>");
        updateChat(sb);
    }

    private StringBuilder getChatLog() {
        StringBuilder sb = new StringBuilder();
        if (firstChat) {
            chatLogText.text = "";
            chatLogText.rectTransform.sizeDelta = new Vector2(0, 0);
            sb.Append("<< Hit Enter to Chat! >>\n");
            firstChat = false;
        } else {
            sb.Append(chatLogText.text);
        }
        sb.Append("\n");
        return sb;
    }

    private void updateChat(StringBuilder sb) {
        chatLogText.text = sb.ToString();
        float newHeight = LayoutUtility.GetPreferredHeight(chatLogText.rectTransform);
        chatLogText.rectTransform.sizeDelta = new Vector2(0, newHeight);
    }

    private string getNameWithColor(string name, Color32 color) {
        StringBuilder sb = new StringBuilder();
        sb.Append("<color=#");
        sb.Append(color.r.ToString("X2"));
        sb.Append(color.g.ToString("X2"));
        sb.Append(color.b.ToString("X2"));
        sb.Append(">");
        sb.Append(name);
        sb.Append("</color>");
        return sb.ToString();
    }
}
