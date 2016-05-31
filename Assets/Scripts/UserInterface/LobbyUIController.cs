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
    public RectTransform createRoomPanel;
    public InputField createRoomInputField;
    public Button createRoomButton;
    public Text roomTitleText;
    public GameObject roomListPanel;    // room list panel when you are in lobby
    public GameObject roomPanel;    // panel when you are inside a room
    public GameObject leaveRoomButton;
    public RectTransform roomContent;
    public RectTransform roomScrollView;
    public Object joinRoomPanelPrefab;
    public Object otherPlayerPanelPrefab;
    public PlayerPanelScript myPlayerPanel;

    public GameClient client { private get; set; }

    private bool firstChat = true;

    // list of room names and join buttons
    private List<RoomPanelEditor> roomPanels = new List<RoomPanelEditor>();
    private List<PlayerPanelScript> otherPlayerPanels = new List<PlayerPanelScript>();

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

    public void sendReadyPacket(bool ready) {
        client.setReady(ready);
        updateRoomNames();
    }

    public void onChangeRoomFailure(bool createFail) {
        if (createFail) {
            createRoomButton.interactable = true;
            logError("Room name taken!");
        } else {
            logError("Unabled to join room");
        }
    }

    // called when someone joins the room
    public void updateRoomNames() {
        if (client.roomName == "Lobby") {
            StringBuilder sb = new StringBuilder();

            IList<PlayerInfo> playerInfos = client.getPlayerInfoList();
            for (int i = 0; i < playerInfos.Count; ++i) {
                PlayerInfo ps = playerInfos[i];
                sb.Append(getTextWithColor(ps.name, ps.color));
                sb.Append('\n');
            }
            playerNamesText.text = sb.ToString();
        } else {    // in a room so update room UIs
            IList<PlayerInfo> playerInfos = client.getPlayerInfoList();
            int numPlayers = playerInfos.Count;
            roomContent.sizeDelta = new Vector2(0, numPlayers * 75);

            // if more have more panels then delete some off end
            while (otherPlayerPanels.Count > numPlayers - 1) {
                int endIndex = otherPlayerPanels.Count - 1;
                Destroy(otherPlayerPanels[endIndex].gameObject);
                otherPlayerPanels.RemoveAt(endIndex);
            }

            // if not enough panels add some to end
            PlayerPanelScript pps;
            while (numPlayers - 1 > otherPlayerPanels.Count) {
                GameObject go = (GameObject)Instantiate(otherPlayerPanelPrefab, Vector3.zero, Quaternion.identity);
                pps = go.GetComponent<PlayerPanelScript>();
                pps.init(roomPanel.transform);
                otherPlayerPanels.Add(pps);
            }
            // set name and ready text of each panel
            int myIndex = client.getMyPlayerIndex();
            int offset = 0;
            for (int i = 0; i < numPlayers; ++i) {
                if (i == myIndex) {
                    pps = myPlayerPanel;
                    offset = 1;
                    pps.setToggle(playerInfos[i].ready);
                } else {
                    pps = otherPlayerPanels[i - offset];
                }
                string pname = getTextWithColor(playerInfos[i].name, playerInfos[i].color);
                string ttext = playerInfos[i].ready ? getTextWithColor("Ready", Color.green) : getTextWithColor("Not Ready", Color.red);
                pps.setText(pname, ttext);
                pps.setYPos(i * -75);
            }
        }
    }

    // called on room change
    public void changedRoom(string roomName) {
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
        createRoomInputField.text = "";
        roomScrollView.offsetMin = new Vector2(isLobby ? 250 : 0, 0);
        roomListPanel.SetActive(isLobby);
        roomPanel.SetActive(!isLobby);
        leaveRoomButton.SetActive(!isLobby);
    }

    // called whenever the room list changes
    public void updateRoomList(List<string> rooms) {
        int numRooms = rooms.Count / 2;
        roomContent.sizeDelta = new Vector2(0, (numRooms + 1) * 75);

        // if more have more panels then delete some off end
        while (roomPanels.Count > numRooms) {
            int endIndex = roomPanels.Count - 1;
            Destroy(roomPanels[endIndex].gameObject);
            roomPanels.RemoveAt(endIndex);
        }

        // if not enough panels add some to end
        while (numRooms > roomPanels.Count) {
            GameObject go = (GameObject)Instantiate(joinRoomPanelPrefab, Vector3.zero, Quaternion.identity);
            RoomPanelEditor rpe = go.GetComponent<RoomPanelEditor>();
            rpe.init(this, roomListPanel.transform);
            roomPanels.Add(rpe);
        }

        // set up the panel anchors and text
        for (int i = 0; i < roomPanels.Count; ++i) {
            roomPanels[i].setYPos(i * -75);
            roomPanels[i].setText(rooms[i * 2], rooms[i * 2 + 1]);
        }
        createRoomPanel.anchoredPosition = new Vector2(0, roomPanels.Count * -75);
    }

    public void logChatMessage(string name, Color32 color, string message) {
        StringBuilder sb = getChatLog();
        sb.Append("[");
        sb.Append(getTextWithColor(name, color));
        sb.Append("] ");
        sb.Append(message);
        updateChat(sb);
    }

    public void logConnectionMessage(string name, Color32 color, bool joined, bool server) {
        StringBuilder sb = getChatLog();
        sb.Append("<");
        sb.Append(getTextWithColor(name, color));
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

    public void logMessage(string message, Color color) {
        StringBuilder sb = getChatLog();
        sb.Append(getTextWithColor(message, color));
        updateChat(sb);
    }

    public void logError(string message) {
        StringBuilder sb = getChatLog();
        sb.Append("[ERROR] ");
        sb.Append(getTextWithColor(message, Color.magenta));
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

    private string getTextWithColor(string text, Color32 color) {
        StringBuilder sb = new StringBuilder();
        sb.Append("<color=#");
        sb.Append(color.r.ToString("X2"));
        sb.Append(color.g.ToString("X2"));
        sb.Append(color.b.ToString("X2"));
        sb.Append(">");
        sb.Append(text);
        sb.Append("</color>");
        return sb.ToString();
    }
}
