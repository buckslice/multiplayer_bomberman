﻿using UnityEngine;
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

    // socket id on server
    public int playerID { get; private set; }

    private IEnumerator statusTextAnim;

    private Level level;

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

    private PlayerSync myPlayer = null;
    private List<PlayerSync> otherPlayers = new List<PlayerSync>();

    private int[] levelOnLoad;
    private Vector3 spawn;
    private bool gameFullyLoaded;

    void OnEnable() {
        playerID = -1;
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
        if (myPlayer) {
            Packet p = new Packet(PacketType.STATE_UPDATE);
            p.Write(myPlayer.transform.position);
            sendPacket(p);
        }

        checkMessages();
    }

    void OnLevelWasLoaded(int levelNum) {
        GameObject levelGO = GameObject.Find("Level");
        if (levelGO && levelNum == 1) {
            level = levelGO.GetComponent<Level>();
            for (int i = 0; i < levelOnLoad.Length; ++i) {
                level.setTile(i, levelOnLoad[i]);
            }
            level.BuildMesh();

            // spawn player for this client
            GameObject pgo = (GameObject)Instantiate(playerPrefab, spawn, Quaternion.identity);
            myPlayer = pgo.GetComponent<PlayerSync>();
            myPlayer.playerID = playerID;

            gameFullyLoaded = true;
        }
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

                    // connect to broadcaster by port and address
                    int rport;
                    string raddress;
                    NetworkTransport.GetBroadcastConnectionInfo(clientSocket, out raddress, out rport, out error);

                    // close client socket on port 8887 so new clients on this comp can connect to broadcast port
                    NetworkTransport.RemoveHost(clientSocket);
                    clientSocket = -1;
                    // reconnect in one second since RemoveHost kind of times out the network momentarily
                    StartCoroutine(waitThenReconnect(1.0f, raddress, rport));

                    return;

                //break;
                case NetworkEventType.ConnectEvent:
                    Debug.Log("CLIENT: connected to server");
                    break;
                case NetworkEventType.DisconnectEvent:
                    Debug.Log("CLIENT: disconnected from server");
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
                if (id != -1) {
                    playerID = id;
                    Debug.Log("CLIENT: authenticated by server, joining game");
                    statusText.text = "Login successful!";
                    statusText.color = Color.yellow;

                    // save level and spawn point info
                    int length = packet.ReadInt();
                    levelOnLoad = new int[length];
                    for (int i = 0; i < length; i++) {
                        levelOnLoad[i] = packet.ReadByte();
                    }
                    spawn = packet.ReadVector3();

                    // load into next scene
                    // rest of initialization proceeds in OnLevelWasLoaded() above
                    SceneManager.LoadScene(1);
                } else {
                    statusText.text = "Invalid login info!";
                    flashStatusText(Color.red);
                }
                break;

            case PacketType.STATE_UPDATE:
                // make sure you dont process state updates if game is not yet fully loaded
                // once you login successfully server starts sending you game states
                if (!gameFullyLoaded) {
                    return;
                }

                int numPlayers = packet.ReadInt();
                for (int i = 0, index = 0; index < numPlayers; ++index) {
                    id = packet.ReadInt();
                    Vector3 pos = packet.ReadVector3();
                    if (playerID == id) {
                        continue; // ignore own position given from server
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
                        newPlayer.initAsRemotePlayer(id);
                        otherPlayers.Add(newPlayer);
                    } else {  // otherwise sync positions of other players
                        otherPlayers[i].syncPosition(pos);
                    }

                    i++;    // increment index into otherPlayers list
                }
                // make sure to remove old players off end of list
                while (otherPlayers.Count >= numPlayers) {
                    Destroy(otherPlayers[otherPlayers.Count - 1].gameObject);
                    otherPlayers.RemoveAt(otherPlayers.Count - 1);
                }

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
        Debug.Log("CLIENT: socket opened: " + clientSocket);

        byte error;
        NetworkTransport.SetBroadcastCredentials(clientSocket, key, version, subversion, out error);
        Debug.Log("CLIENT: started");
    }

    IEnumerator waitThenReconnect(float waitTime, string remoteAddress, int remotePort) {
        yield return new WaitForSeconds(waitTime);

        while (clientSocket < 0 && port > 8880) {
            clientSocket = NetworkTransport.AddHost(topology, --port);
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
