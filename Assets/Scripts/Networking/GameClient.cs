using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Text;

// internal class different from servers PlayerState
public class PlayerInfo {
    public int id;
    public string name;
    public Color32 color;
    public bool ready;
    public PlayerInfo(int id, string name, Color32 color, bool ready) {
        this.id = id;
        this.name = name;
        this.color = color;
        this.ready = ready;
    }
}

public class GameClient : MonoBehaviour {
    public GameObject playerPrefab;

    private byte channelReliable;
    private HostTopology topology;
    private int maxConnections = 4;

    private int port = 8887;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int clientSocket = -1;  // this clients socket ID
    private int serverSocket = -1;  // ID of server this client is connected to    

    private bool waitingForLoginResponse = false;
    private bool waitingForRoomChangeResponse = false;

    // this client is always at the first entry
    private List<PlayerInfo> playersInRoom = new List<PlayerInfo>();
    private int indexInRoom = -1;
    private List<PlayerSync> playersInGame = new List<PlayerSync>();
    public string roomName { get; private set; }
    private string myName;

    private int[] levelLoad;
    private Vector3 spawn;
    private Level level;
    private bool enabledServer = false;
    private float timeUntilStartServer = 2.0f;

    private MenuUIController menuUI;
    private LobbyUIController lobbyUI;
    // chat scroll view seems to take a frame or two to get settled (also other reasons)
    // message processing is paused once logged in and for a couple frames
    // after because the scene transition was causing problems
    private int loadFrames = 0;
    private bool waitToCheckMessages = false;
    private bool justConnected = true;

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        DontDestroyOnLoad(gameObject);

        menuUI = FindObjectOfType<MenuUIController>();
        menuUI.setupStartingUI(this);

        // network init
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.ReliableSequenced);
        topology = new HostTopology(config, maxConnections);
        StartCoroutine(tryConnectRoutine());

    }

    void OnLevelWasLoaded(int levelNum) {
        if (levelNum == 1) {
            lobbyUI = FindObjectOfType<LobbyUIController>();
            lobbyUI.client = this;
        }
    }

    // Update is called once per frame
    void Update() {
        if (SceneManager.GetActiveScene().buildIndex == 0) {
            // if havnt connected to a server and waited long enough then start one
            timeUntilStartServer -= Time.deltaTime;
            if (!enabledServer && serverSocket < 0 && timeUntilStartServer < 0.0f) {
                Debug.Log("Enabling Server");
                gameObject.GetComponent<GameServer>().enabled = true;
                enabledServer = true;
            }
        } else {    // scene 1
            if (waitToCheckMessages && ++loadFrames > 2) {
                waitToCheckMessages = false;
            }
        }

        checkMessages();
    }

    private void checkMessages() {
        if (clientSocket < 0) {
            return;
        }

        int recConnectionID;    // rec stands for received
        int recChannelID;
        int bsize = 1024;
        byte[] buffer = new byte[bsize];
        int dataSize;
        byte error;

        // continuously loop until there are no more messages
        while (true) {
            if (waitToCheckMessages) {
                return;
            }
            NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
                clientSocket, out recConnectionID, out recChannelID, buffer, bsize, out dataSize, out error);
            switch (recEvent) {
                case NetworkEventType.Nothing:
                    return;
                case NetworkEventType.DataEvent:
                    receivePacket(new Packet(buffer));
                    break;

                case NetworkEventType.BroadcastEvent:
                    if (serverSocket >= 0) { // already connected to a server
                        break;
                    }
                    menuUI.stopStatusTextAnim();
                    Debug.Log("CLIENT: found server broadcast!");
                    string statusText = !enabledServer ? "Found Server!" : "Created Server!";
                    menuUI.setStatusText(statusText, Color.yellow, true);

                    // get broadcast message (not doing anything with it currently)
                    NetworkTransport.GetBroadcastConnectionMessage(clientSocket, buffer, bsize, out dataSize, out error);

                    // connect to broadcaster by port and address
                    int broadcastPort;
                    string broadcastAddress;
                    NetworkTransport.GetBroadcastConnectionInfo(clientSocket, out broadcastAddress, out broadcastPort, out error);

                    // close client socket on port 8887 so new clients on this comp can connect to broadcast port
                    NetworkTransport.RemoveHost(clientSocket);
                    clientSocket = -1;
                    // reconnect in one second since RemoveHost kind of times out the network momentarily
                    StartCoroutine(waitThenReconnect(0.5f, broadcastAddress, broadcastPort));

                    return;
                case NetworkEventType.ConnectEvent:
                    Debug.Log("CLIENT: connected to server");
                    break;
                case NetworkEventType.DisconnectEvent:
                    Debug.Log("CLIENT: disconnected from server");
                    ResetToMenu.Reset();
                    break;
                default:
                    break;
            }
        }
    }

    // sends a packet to the server
    public void sendPacket(Packet p) {
        byte error;
        NetworkTransport.Send(clientSocket, serverSocket, channelReliable, p.getData(), p.getSize(), out error);
    }

    private void receivePacket(Packet packet) {
        PacketType pt = (PacketType)packet.ReadByte();

        int id, len;
        switch (pt) {
            case PacketType.LOGIN:
                waitingForLoginResponse = false;
                id = packet.ReadInt();
                if (id >= 0) {
                    Debug.Log("CLIENT: authenticated by server, joining lobby");
                    myName = packet.ReadString();
                    menuUI.setStatusText("Login successful!", Color.yellow, false);

                    // load into next scene
                    SceneManager.LoadScene(1);
                    waitToCheckMessages = true;   // pause checking packets until in new scene
                } else if (id == -1) {
                    Debug.Log("CLIENT: invalid login");
                    menuUI.setStatusText("Invalid login info!", Color.red, true);
                } else if (id == -2) {
                    Debug.Log("CLIENT: already loggged in");
                    menuUI.setStatusText("Already logged in!", Color.red, true);
                }
                break;

            case PacketType.STATE_UPDATE:
                int numAlivePlayers = packet.ReadInt();
                for (int i = 0, ai = 0; ai < numAlivePlayers; ++ai) {
                    id = packet.ReadInt();
                    Vector3 pos = packet.ReadVector3();
                    // if player id mismatch then delete players until they match
                    while (i < playersInGame.Count && playersInGame[i].playerID != id) {
                        Destroy(playersInGame[i].gameObject);
                        playersInGame.RemoveAt(i);
                    }
                    if (i == playersInGame.Count) { // init new gameObject if new player
                        GameObject pgo = (GameObject)Instantiate(playerPrefab, pos, Quaternion.identity);
                        PlayerSync newPlayer = pgo.GetComponent<PlayerSync>();
                        newPlayer.init(id, playersInRoom[i].color, i == getMyPlayerIndex() ? this : null);
                        playersInGame.Add(newPlayer);
                    }else if(i != getMyPlayerIndex()) { // else update existing player if not you
                        playersInGame[i].updatePosition(pos);
                    }
                    ++i;
                }
                // remove players from the end of list
                while(playersInGame.Count > 0 && playersInGame.Count > numAlivePlayers) {
                    Destroy(playersInGame[playersInGame.Count - 1].gameObject);
                    playersInGame.RemoveAt(playersInGame.Count - 1);
                }
                break;

            case PacketType.SPAWN_BOMB:
                level.placeBomb(packet.ReadVector3(), false, packet.ReadInt());
                break;

            case PacketType.SPAWN_POWERUP:
                level.placePowerUp(packet.ReadVector3(), packet.ReadInt());
                break;

            case PacketType.PLAYER_JOINED_ROOM:    // a player joined your room
                int pjid = packet.ReadInt();
                string pjname = packet.ReadString();
                Color32 pjcolor = packet.ReadColor();
                playersInRoom.Add(new PlayerInfo(pjid, pjname, pjcolor, false));
                lobbyUI.updateRoomNames();
                lobbyUI.logConnectionMessage(pjname, pjcolor, true, false);
                break;
            case PacketType.PLAYER_LEFT_ROOM:    // a player left your room
                int plid = packet.ReadInt();
                for (int i = 0; i < playersInRoom.Count; ++i) {
                    PlayerInfo pi = playersInRoom[i];
                    if (pi.id == plid) {
                        playersInRoom.RemoveAt(i);
                        indexInRoom = -1;
                        lobbyUI.updateRoomNames();
                        lobbyUI.logConnectionMessage(pi.name, pi.color, false, false);
                        break;
                    }
                }
                break;
            case PacketType.CHAT_MESSAGE:
                lobbyUI.logChatMessage(packet.ReadString(), packet.ReadColor(), packet.ReadString());
                break;
            case PacketType.CHANGE_ROOM:
                // only time client will receive a packet of this type is if
                // they tried to change a room but it failed somehow
                lobbyUI.onChangeRoomFailure(packet.ReadBool());
                waitingForRoomChangeResponse = false;
                break;

            case PacketType.YOU_JOINED_ROOM:  // you joined a room
                //lobbyUI.setLobbyActive(true);
                waitingForRoomChangeResponse = false;
                roomName = packet.ReadString();
                len = packet.ReadInt();
                playersInRoom.Clear();
                for (int i = 0; i < len; ++i) {
                    playersInRoom.Add(new PlayerInfo(
                        packet.ReadInt(), packet.ReadString(), packet.ReadColor(), packet.ReadBool()));
                }
                indexInRoom = -1;
                if (justConnected) {
                    justConnected = false;
                    PlayerInfo pi = getMyPlayer();
                    lobbyUI.logConnectionMessage(pi.name, pi.color, true, true);
                }

                lobbyUI.changedRoom(roomName);
                lobbyUI.updateRoomNames();
                break;
            case PacketType.PLAYER_JOINED_SERVER:
                lobbyUI.logConnectionMessage(packet.ReadString(), packet.ReadColor(), true, true);
                break;
            case PacketType.PLAYER_LEFT_SERVER:
                lobbyUI.logConnectionMessage(packet.ReadString(), packet.ReadColor(), false, true);
                break;
            case PacketType.ROOM_LIST_UPDATE:
                List<string> roomListUpdate = new List<string>();
                len = packet.ReadInt();
                for (int i = 0; i < len; ++i) {
                    roomListUpdate.Add(packet.ReadString());
                    roomListUpdate.Add(packet.ReadInt().ToString());
                }
                lobbyUI.updateRoomList(roomListUpdate);
                break;
            case PacketType.SET_READY:
                string playerName = packet.ReadString();
                for (int i = 0; i < playersInRoom.Count; ++i) {
                    if (playersInRoom[i].name == playerName) {
                        playersInRoom[i].ready = packet.ReadBool();
                        break;
                    }
                }
                lobbyUI.updateRoomNames();
                break;
            case PacketType.GAME_COUNTDOWN:
                int t = packet.ReadInt();
                if (t == 0) {
                    lobbyUI.logMessage("Game Start!", Color.yellow);

                    GameObject levelGO = GameObject.Find("Level");
                    if (!levelGO) {
                        Debug.LogError("CLIENT: can't find level!!!");
                        return;
                    }

                    level = levelGO.GetComponent<Level>();
                    LevelData ld = new LevelData();
                    level.ld = ld;

                    // read level data
                    len = packet.ReadInt();
                    for (int i = 0; i < len; ++i) {
                        ld.setTile(i, packet.ReadByte());
                    }
                    level.buildMesh();

                    lobbyUI.setLobbyActive(false);
                    lobbyUI.fadeInFromBlack();
                } else {
                    lobbyUI.logMessage(t + "...", Color.yellow);
                }
                break;
            case PacketType.GAME_END:
                string message = packet.ReadString();
                // destroy remaining player objects
                for (int i = 0; i < playersInGame.Count; ++i) {
                    Destroy(playersInGame[i].gameObject);
                }
                // find and destroy all leftover powerups
                GameObject[] powerups = GameObject.FindGameObjectsWithTag("PowerUp");
                for(int i = 0; i < powerups.Length; ++i) {
                    Destroy(powerups[i]);
                }
                playersInGame.Clear();
                lobbyUI.fadeOutWithText(message);

                break;
            default:
                break;
        }
    }

    private IEnumerator tryConnectRoutine() {
        while (clientSocket < 0) {
            clientSocket = NetworkTransport.AddHost(topology, port);
            if (clientSocket < 0) {
                timeUntilStartServer = 2.0f;
                Debug.Log("CLIENT: port blocked: " + port);
                yield return new WaitForSeconds(1.0f);
            }
        }
        byte error;
        NetworkTransport.SetBroadcastCredentials(clientSocket, key, version, subversion, out error);
        Debug.Log("CLIENT: connected on port: " + port);
    }

    private IEnumerator waitThenReconnect(float waitTime, string remoteAddress, int remotePort) {
        timeUntilStartServer = 1000.0f;
        yield return new WaitForSeconds(waitTime);

        while (clientSocket < 0 && port > 8870) { // limit to 16 players max
            clientSocket = NetworkTransport.AddHost(topology, --port);
        }
        if (port <= 8870) { // just incase this happens
            Debug.Log("CLIENT: no open ports, quiting");
            ResetToMenu.Reset();
            yield break;
        }
        Debug.Log("CLIENT: reconnected on port: " + port);
        byte error;
        serverSocket = NetworkTransport.Connect(clientSocket, remoteAddress, remotePort, 0, out error);

        // set up UI for login
        menuUI.setupLoginUI();

        // can delete server script now if not used
        if (!enabledServer) {
            Destroy(gameObject.GetComponent<GameServer>());
        }
    }

    // tries to login with given name and password
    public void tryLogin(string name, string password) {
        if (waitingForLoginResponse) {
            return;
        }
        waitingForLoginResponse = true;

        // send packet with username and password
        Packet p = new Packet(PacketType.LOGIN);
        p.Write(name);
        p.Write(password);
        sendPacket(p);

    }

    public void tryChangeRoom(bool creating, string name) {
        if (waitingForRoomChangeResponse) {
            return;
        }
        waitingForRoomChangeResponse = true;

        Packet p = new Packet(PacketType.CHANGE_ROOM);
        p.Write(creating); // are you trying to create this room?
        p.Write(name);
        sendPacket(p);
    }

    public IList<PlayerInfo> getPlayerInfoList() {
        return playersInRoom.AsReadOnly();
    }

    public void setReady(bool ready) {
        Packet p = new Packet(PacketType.SET_READY);
        p.Write(ready);
        sendPacket(p);

        playersInRoom[getMyPlayerIndex()].ready = ready;
    }

    public int getMyPlayerIndex() {
        if (indexInRoom < 0) {   // dirty
            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].name == myName) {
                    indexInRoom = i;
                    return indexInRoom;
                }
            }
        }
        Debug.Assert(indexInRoom >= 0, "Can't find my player in my room!");
        return indexInRoom;
    }

    public PlayerInfo getMyPlayer() {
        return playersInRoom[getMyPlayerIndex()];
    }

}
