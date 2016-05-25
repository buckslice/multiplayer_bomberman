using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameClient : MonoBehaviour {
    public GameObject startServerButton;
    public GameObject startClientButton;
    public GameObject joinButton;
    public InputField nameInputField;
    public InputField passwordInputField;
    public Text statusText;
    public GameObject playerPrefab;

    private IEnumerator statusTextAnim;

    private byte channelReliable;
    private HostTopology topology;
    private int maxConnections = 4;

    private int port = 8887;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int clientSocket = -1;  // this clients socket ID
    private int serverSocket = -1;  // ID of server this client is connected to    
    private int playerID = -1;      // ID of player on server

    private bool waitingForLoginResponse = false;

    private List<PlayerSync> otherPlayers = new List<PlayerSync>();

    private int[] levelLoad;
    private Vector3 spawn;
    private Level level;

    private bool restartingGame = true;

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        Destroy(gameObject.GetComponent<GameServer>());
        DontDestroyOnLoad(gameObject);

        // UI stuff
        Destroy(startServerButton);
        Destroy(startClientButton);
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
        }

        checkMessages();
    }

    void OnLevelWasLoaded(int levelNum) {
        GameObject levelGO = GameObject.Find("Level");
        if (levelGO && levelNum == 1) {
            level = levelGO.GetComponent<Level>();

            for (int i = 0; i < levelLoad.Length; ++i) {
                level.setTile(i, levelLoad[i]);
            }
            level.BuildMesh();

            // spawn player for this client
            GameObject pgo = (GameObject)Instantiate(playerPrefab, spawn, Quaternion.identity);
            pgo.GetComponent<PlayerSync>().init(playerID, this);

            // delay starting the game a little so the client can get rid of old state packets from server
            StartCoroutine(setFullyLoaded(0.3f));
        }
    }

    IEnumerator setFullyLoaded(float t) {
        yield return new WaitForSeconds(t);
        restartingGame = false;
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
                    statusText.text = "Found Server!";
                    flashStatusText(Color.yellow);

                    // get broadcast message (not doing anything with it currently)
                    NetworkTransport.GetBroadcastConnectionMessage(clientSocket, buffer, bsize, out dataSize, out error);

                    Packet p = new Packet(buffer);
                    PacketType type = (PacketType)p.ReadByte(); // reading it just to get it out of the way
                    string localIP = p.ReadString();

                    // connect to broadcaster by port and address
                    int broadcastPort;
                    string broadcastAddress;
                    NetworkTransport.GetBroadcastConnectionInfo(clientSocket, out broadcastAddress, out broadcastPort, out error);

                    // close client socket on port 8887 so new clients on this comp can connect to broadcast port
                    NetworkTransport.RemoveHost(clientSocket);
                    clientSocket = -1;
                    // reconnect in one second since RemoveHost kind of times out the network momentarily
                    //StartCoroutine(waitThenReconnect(0.5f, broadcastAddress, broadcastPort));
                    StartCoroutine(waitThenReconnect(0.5f, localIP, broadcastPort));

                    return;

                //break;
                case NetworkEventType.ConnectEvent:
                    Debug.Log("CLIENT: connected to server");
                    break;
                case NetworkEventType.DisconnectEvent:
                    Debug.Log("CLIENT: disconnected from server");
                    // if was at login screen then reset
                    if (SceneManager.GetActiveScene().buildIndex == 0 && nameInputField.IsActive()) {
                        ResetToMenu.Reset();
                    }
                    else
                    {
                        SceneManager.LoadScene(0);
                    }

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
        // make sure you dont process state updates if game is not yet fully loaded
        // once you login successfully server starts sending you game states
        if (restartingGame && pt != PacketType.LOGIN) {
            return;
        }

        int id;
        switch (pt) {
            case PacketType.LOGIN:
                waitingForLoginResponse = false;
                id = packet.ReadInt();
                if (id >= 0) {
                    playerID = id;
                    Debug.Log("CLIENT: authenticated by server, joining game");
                    statusText.text = "Login successful!";
                    statusText.color = Color.yellow;

                    levelLoad = new int[packet.ReadInt()];
                    for (int i = 0; i < levelLoad.Length; ++i) {
                        levelLoad[i] = packet.ReadByte();
                    }
                    // spawn player for this client
                    spawn = packet.ReadVector3();

                    // load into next scene
                    // rest of initialization proceeds in OnLevelWasLoaded() above
                    SceneManager.LoadScene(1);
                } else if(id == -1){
                    statusText.text = "Invalid login info!";
                    flashStatusText(Color.red);
                } else if (id == -2)
                {
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
                    if (playerID == id) {
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
                restartingGame = true;
                int winner = packet.ReadInt();

                // clear otherplayers list
                for(int i = 0; i < otherPlayers.Count; ++i) {
                    if(otherPlayers[i].playerID != winner) {
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
            default:
                break;
        }
    }

    IEnumerator tryConnectRoutine() {
        while (clientSocket < 0) {
            clientSocket = NetworkTransport.AddHost(topology, port);
            if (clientSocket < 0) {
                Debug.Log("CLIENT: port blocked: " + port);
                yield return new WaitForSeconds(1.0f);
            }
        }
        byte error;
        NetworkTransport.SetBroadcastCredentials(clientSocket, key, version, subversion, out error);
        Debug.Log("CLIENT: connected on port: " + port);
    }

    IEnumerator waitThenReconnect(float waitTime, string remoteAddress, int remotePort) {
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
