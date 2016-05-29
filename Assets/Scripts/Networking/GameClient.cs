using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class GameClient : MonoBehaviour {
    public GameObject playerPrefab;

    // menu screen stuff
    public GameObject background;
    public GameObject startClientButton;
    public GameObject joinButton;
    public InputField nameInputField;
    public InputField passwordInputField;
    public Text statusText;

    // lobby screen stuff
    private Text playerNamesText;
    private Text chatLogText;
    private GameObject chatInputBar;
    private Text chatInputText;


    private IEnumerator statusTextAnim;

    private byte channelReliable;
    private HostTopology topology;
    private int maxConnections = 4;

    private string roomName;
    private int port = 8887;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int clientSocket = -1;  // this clients socket ID
    private int serverSocket = -1;  // ID of server this client is connected to    

    private bool waitingForLoginResponse = false;

    // this client is always at the first entry
    private List<PlayerState> playersOnServer = new List<PlayerState>();
    private List<PlayerSync> otherPlayers = new List<PlayerSync>();

    private int[] levelLoad;
    private Vector3 spawn;
    private Level level;
    private bool enabledServer = false;
    private float timeUntilStartServer = 2.0f;

    private float updateNamesTimer = 0.0f;
    private bool typingInChat = false;
    private bool firstChat = true;
    private string chatString;
    private bool inLobby = true;

    // internal class different from servers PlayerState
    class PlayerState {
        public int id;
        public string name;
        public Color32 color;
        public PlayerState(int id, string name, Color32 color) {
            this.id = id;
            this.name = name;
            this.color = color;
        }
    }

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        //Destroy(gameObject.GetComponent<GameServer>());
        DontDestroyOnLoad(gameObject);

        // UI stuff
        startClientButton.SetActive(false);
        statusText.gameObject.SetActive(true);

        statusTextAnim = statusTextAnimRoutine();
        StartCoroutine(statusTextAnim);

        // network init
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        topology = new HostTopology(config, maxConnections);
        StartCoroutine(tryConnectRoutine());

    }

    void OnLevelWasLoaded(int levelNum) {
        if (levelNum == 1) {
            playerNamesText = GameObject.Find("ConnectedPlayerNames").GetComponent<Text>();
            chatLogText = GameObject.Find("ChatContent").GetComponent<Text>();
            chatInputBar = GameObject.Find("ChatInputBar");
            chatInputText = chatInputBar.transform.Find("ChatInputText").GetComponent<Text>();
            chatInputBar.SetActive(false);
        }

        //GameObject levelGO = GameObject.Find("Level");
        //if (levelGO && levelNum == 1) {
        //    level = levelGO.GetComponent<Level>();

        //    for (int i = 0; i < levelLoad.Length; ++i) {
        //        level.setTile(i, levelLoad[i]);
        //    }
        //    level.BuildMesh();

        //    // spawn player for this client
        //    GameObject pgo = (GameObject)Instantiate(playerPrefab, spawn, Quaternion.identity);
        //    pgo.GetComponent<PlayerSync>().init(playerID, this);

        //    // delay starting the game a little so the client can get rid of old state packets from server
        //    StartCoroutine(setFullyLoaded(0.3f));
        //}
    }

    // Update is called once per frame
    void Update() {
        // if in menu scene then make pressing tab switch
        // between name and password input fields and make
        // hitting enter try to join with current credentials
        if (SceneManager.GetActiveScene().buildIndex == 0) {
            if (Input.GetKeyDown(KeyCode.Tab)) {
                if (nameInputField.isFocused) {
                    passwordInputField.ActivateInputField();
                } else {
                    nameInputField.ActivateInputField();
                }
            }
            if (Input.GetKeyDown(KeyCode.Return)) {
                tryJoiningGame();
            }

            // if havnt connected to a server and waited then start one
            timeUntilStartServer -= Time.deltaTime;
            if (!enabledServer && serverSocket < 0 && timeUntilStartServer < 0.0f) {
                Debug.Log("Enabling Server");
                gameObject.GetComponent<GameServer>().enabled = true;
                enabledServer = true;
            }
        } else {
            updateNamesTimer -= Time.deltaTime;
            if (updateNamesTimer < 0.0f) {
                updateNamesTimer = 0.5f;
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < playersOnServer.Count; ++i) {
                    PlayerState ps = playersOnServer[i];
                    sb.Append(getNameWithColor(ps.name, ps.color));
                    sb.Append('\n');
                }

                playerNamesText.text = sb.ToString();
            }

            // do chat update
            if (Input.GetKeyDown(KeyCode.Return)) {
                typingInChat = !typingInChat;
                chatInputBar.SetActive(typingInChat);
                if (!typingInChat && chatString.Length != 0) {    // just hit enter so send message
                    Packet p = new Packet(PacketType.CHAT_MESSAGE);
                    PlayerState me = playersOnServer[0];
                    p.Write(me.name);
                    p.Write(me.color);
                    p.Write(chatString);
                    sendPacket(p);
                    processChatString(me.name, me.color, chatString);
                    chatString = "";
                }
            }
            // gather keypresses for message
            if (typingInChat) {
                string inputs = Input.inputString;
                for (int i = 0; i < inputs.Length; ++i) {
                    char c = inputs[i];
                    if (c == '\b') {
                        if (chatString.Length != 0) {
                            chatString = chatString.Substring(0, chatString.Length - 1);
                        }
                    } else if (c == '\n') {
                        // can just ignore i think?
                    } else {
                        chatString += c;
                    }
                }
                chatInputText.text = chatString;
            }
        }

        checkMessages();
    }

    public void checkMessages() {
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
                    StopCoroutine(statusTextAnim);
                    Debug.Log("CLIENT: found server broadcast!");
                    statusText.text = !enabledServer ? "Found Server!" : "Created Server!";
                    flashStatusText(Color.yellow);

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

    public void receivePacket(Packet packet) {
        PacketType pt = (PacketType)packet.ReadByte();

        int id;
        switch (pt) {
            case PacketType.LOGIN:
                waitingForLoginResponse = false;
                id = packet.ReadInt();
                if (id >= 0) {
                    string name = packet.ReadString();
                    Color32 color = packet.ReadColor();
                    playersOnServer.Clear();
                    playersOnServer.Add(new PlayerState(id, name, color));

                    int numPlayers = packet.ReadInt();
                    for (int i = 0; i < numPlayers; ++i) {
                        int pid = packet.ReadInt();
                        string pname = packet.ReadString();
                        Color32 pcolor = packet.ReadColor();
                        playersOnServer.Add(new PlayerState(pid, pname, pcolor));
                    }

                    Debug.Log("CLIENT: authenticated by server, joining game");
                    statusText.text = "Login successful!";
                    statusText.color = Color.yellow;

                    // load into next scene
                    SceneManager.LoadScene(1);
                } else if (id == -1) {
                    Debug.Log("CLIENT: invalid login");
                    statusText.text = "Invalid login info!";
                    flashStatusText(Color.red);
                } else if (id == -2) {
                    Debug.Log("CLIENT: already loggged in");
                    statusText.text = "Already logged in!";
                    flashStatusText(Color.red);
                }
                break;

            case PacketType.STATE_UPDATE:
                int numAlivePlayers = packet.ReadInt();
                bool myPlayerAlive = false;
                for (int i = 0, index = 0; index < numAlivePlayers; ++index) {
                    id = packet.ReadInt();
                    Vector3 pos = packet.ReadVector3();
                    if (playersOnServer[0].id == id) {
                        myPlayerAlive = true;
                        continue; // ignore own position given from server for now
                    }

                    // if player id mismatch then delete because he got disconnected
                    while (i < otherPlayers.Count && otherPlayers[i].playerID != id) {
                        Destroy(otherPlayers[i].gameObject);
                        otherPlayers.RemoveAt(i);
                    }
                    // if new index is at end of list then add new player to end
                    if (i == otherPlayers.Count) {
                        GameObject pgo = (GameObject)Instantiate(playerPrefab, pos, Quaternion.identity);
                        PlayerSync newPlayer = pgo.GetComponent<PlayerSync>();
                        newPlayer.init(id);
                        otherPlayers.Add(newPlayer);
                    } else {  // otherwise sync positions of other players
                        otherPlayers[i].updatePosition(pos);
                    }

                    i++;    // increment index into otherPlayers list
                }
                if (myPlayerAlive) {    // if my player is alive then take one off from numPlayers
                    numAlivePlayers -= 1;
                }
                // make sure to remove old players off end of list
                while (otherPlayers.Count > 0 && otherPlayers.Count > numAlivePlayers) {
                    Destroy(otherPlayers[otherPlayers.Count - 1].gameObject);
                    otherPlayers.RemoveAt(otherPlayers.Count - 1);
                }

                break;

            case PacketType.SPAWN_BOMB:
                level.placeBomb(packet.ReadVector3(), false);
                break;

            case PacketType.RESTART_GAME:
                int winner = packet.ReadInt();

                // clear otherplayers list
                for (int i = 0; i < otherPlayers.Count; ++i) {
                    if (otherPlayers[i].playerID != winner) {
                        Destroy(otherPlayers[i].gameObject);
                    }
                }
                otherPlayers.Clear();

                // save level data
                levelLoad = new int[packet.ReadInt()];
                for (int i = 0; i < levelLoad.Length; ++i) {
                    levelLoad[i] = packet.ReadByte();
                }
                // save player spawn
                spawn = packet.ReadVector3();
                string message = packet.ReadString();
                FindObjectOfType<SceneLoader>().fadeOutWithText(message);

                break;
            case PacketType.PLAYER_JOIN:
                int pjid = packet.ReadInt();
                string pjname = packet.ReadString();
                Color32 pjcolor = packet.ReadColor();
                playersOnServer.Add(new PlayerState(pjid, pjname, pjcolor));
                break;
            case PacketType.PLAYER_LEFT:
                int plid = packet.ReadInt();
                for (int i = 0; i < playersOnServer.Count; ++i) {
                    if (playersOnServer[i].id == plid) {
                        playersOnServer.RemoveAt(i);
                        break;
                    }
                }
                break;
            case PacketType.CHAT_MESSAGE:
                processChatString(packet.ReadString(), packet.ReadColor(), packet.ReadString());
                break;
            default:
                break;
        }
    }

    private void processChatString(string name, Color32 c, string content) {
        if (firstChat) {
            chatLogText.text = "";
            chatLogText.rectTransform.sizeDelta = new Vector2(0, 0);
            firstChat = false;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append(getNameWithColor(name, c));
        sb.Append(": ");
        sb.Append(content);
        sb.Append("\n");
        sb.Append(chatLogText.text);

        chatLogText.text = sb.ToString();
        float newHeight = LayoutUtility.GetPreferredHeight(chatLogText.rectTransform);
        chatLogText.rectTransform.sizeDelta = new Vector2(0, newHeight);
    }

    IEnumerator tryConnectRoutine() {
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

    IEnumerator waitThenReconnect(float waitTime, string remoteAddress, int remotePort) {
        timeUntilStartServer = 100.0f;
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
        statusText.text = "Enter Login Info:";
        flashStatusText(Color.green);
        nameInputField.gameObject.SetActive(true);
        passwordInputField.gameObject.SetActive(true);
        joinButton.SetActive(true);

        // can delete server script now if not used
        if (!enabledServer) {
            Destroy(gameObject.GetComponent<GameServer>());
        }
    }

    // tries to join game with current input field credentials
    public void tryJoiningGame() {
        if (waitingForLoginResponse) {
            return;
        }
        if (nameInputField.text == "" || passwordInputField.text == "") {
            statusText.text = "Enter name and password!";
            flashStatusText(Color.red);
            Debug.Log("CLIENT: no name/password entered");
            return;
        }

        // send packet with username and password
        Packet p = new Packet(PacketType.LOGIN);
        p.Write(nameInputField.text);
        p.Write(passwordInputField.text);
        sendPacket(p);

        waitingForLoginResponse = true;
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

    // UI ENUMERATORS
    IEnumerator flashStatusTextHandle;
    public void flashStatusText(Color color) {
        if (flashStatusTextHandle != null) {
            StopCoroutine(flashStatusTextHandle);
        }
        flashStatusTextHandle = flashStatusTextRoutine(color);
        StartCoroutine(flashStatusTextHandle);
    }

    IEnumerator flashStatusTextRoutine(Color c) {
        float t = 0.0f;
        while (t < 1.0f) {
            t += Time.deltaTime;
            statusText.color = Color.Lerp(Color.white, c, t);
            yield return null;
        }
        statusText.color = c;
        flashStatusTextHandle = null;
    }

    IEnumerator statusTextAnimRoutine() {
        int dots = 3;
        float timestep = 1.0f;
        float t = 0.0f;

        while (true) {
            t += Time.deltaTime;

            if (t > timestep) {
                dots = (dots + 1) % 4;
                string ds = "";
                for (int i = 0; i < dots; i++) {
                    ds += ".";
                }
                statusText.text = "Looking for Server" + ds;

                t -= timestep;
            }
            statusText.color = Color.Lerp(Color.yellow, Color.green, t);
            yield return null;
        }
    }
}
