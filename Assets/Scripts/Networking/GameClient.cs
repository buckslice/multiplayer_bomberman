﻿using UnityEngine;
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
    public PlayerInfo(int id, string name, Color32 color) {
        this.id = id;
        this.name = name;
        this.color = color;
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
    private List<PlayerInfo> playersInMyRoom = new List<PlayerInfo>();
    private List<PlayerSync> playersInGame = new List<PlayerSync>();
    private string roomName;

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
    private bool dontCheckMessages = false;

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
        if (SceneManager.GetActiveScene().buildIndex == 0) {
            // if havnt connected to a server and waited long enough then start one
            timeUntilStartServer -= Time.deltaTime;
            if (!enabledServer && serverSocket < 0 && timeUntilStartServer < 0.0f) {
                Debug.Log("Enabling Server");
                gameObject.GetComponent<GameServer>().enabled = true;
                enabledServer = true;
            }
        } else {    // scene 1
            if (dontCheckMessages && ++loadFrames > 2) {
                dontCheckMessages = false;
                PlayerInfo pi = getMyPlayer();
                lobbyUI.logConnectionMessage(pi.name, pi.color, true, true);
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
            if (dontCheckMessages) {
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
                    string name = packet.ReadString();
                    Color32 color = packet.ReadColor();
                    playersInMyRoom.Clear();
                    playersInMyRoom.Add(new PlayerInfo(id, name, color));

                    Debug.Log("CLIENT: authenticated by server, joining lobby");
                    menuUI.setStatusText("Login successful!", Color.yellow, false);

                    // load into next scene
                    SceneManager.LoadScene(1);
                    dontCheckMessages = true;   // pause checking packets until in new scene
                } else if (id == -1) {
                    Debug.Log("CLIENT: invalid login");
                    menuUI.setStatusText("Invalid login info!", Color.red, true);
                } else if (id == -2) {
                    Debug.Log("CLIENT: already loggged in");
                    menuUI.setStatusText("Alread logged in!", Color.red, true);
                }
                break;

            case PacketType.STATE_UPDATE:
                int numAlivePlayers = packet.ReadInt();
                bool myPlayerAlive = false;
                for (int i = 0, index = 0; index < numAlivePlayers; ++index) {
                    id = packet.ReadInt();
                    Vector3 pos = packet.ReadVector3();
                    if (playersInMyRoom[0].id == id) {
                        myPlayerAlive = true;
                        continue; // ignore own position given from server for now
                    }

                    // if player id mismatch then delete because he got disconnected
                    while (i < playersInGame.Count && playersInGame[i].playerID != id) {
                        Destroy(playersInGame[i].gameObject);
                        playersInGame.RemoveAt(i);
                    }
                    // if new index is at end of list then add new player to end
                    if (i == playersInGame.Count) {
                        GameObject pgo = (GameObject)Instantiate(playerPrefab, pos, Quaternion.identity);
                        PlayerSync newPlayer = pgo.GetComponent<PlayerSync>();
                        newPlayer.init(id);
                        playersInGame.Add(newPlayer);
                    } else {  // otherwise sync positions of other players
                        playersInGame[i].updatePosition(pos);
                    }

                    i++;    // increment index into otherPlayers list
                }
                if (myPlayerAlive) {    // if my player is alive then take one off from numPlayers
                    numAlivePlayers -= 1;
                }
                // make sure to remove old players off end of list
                while (playersInGame.Count > 0 && playersInGame.Count > numAlivePlayers) {
                    Destroy(playersInGame[playersInGame.Count - 1].gameObject);
                    playersInGame.RemoveAt(playersInGame.Count - 1);
                }

                break;

            case PacketType.SPAWN_BOMB:
                level.placeBomb(packet.ReadVector3(), false);
                break;

            case PacketType.RESTART_GAME:
                int winner = packet.ReadInt();

                // clear otherplayers list
                for (int i = 0; i < playersInGame.Count; ++i) {
                    if (playersInGame[i].playerID != winner) {
                        Destroy(playersInGame[i].gameObject);
                    }
                }
                playersInGame.Clear();

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
            case PacketType.PLAYER_JOINED_ROOM:    // a player joined your room
                int pjid = packet.ReadInt();
                string pjname = packet.ReadString();
                Color32 pjcolor = packet.ReadColor();
                Debug.Log("CLIENT: player joined room " + pjname);
                playersInMyRoom.Add(new PlayerInfo(pjid, pjname, pjcolor));
                lobbyUI.logConnectionMessage(pjname, pjcolor, true, false);
                break;
            case PacketType.PLAYER_LEFT_ROOM:    // a player left your room
                int plid = packet.ReadInt();
                for (int i = 0; i < playersInMyRoom.Count; ++i) {
                    PlayerInfo pi = playersInMyRoom[i];
                    if (pi.id == plid) {
                        lobbyUI.logConnectionMessage(pi.name, pi.color, false, false);
                        playersInMyRoom.RemoveAt(i);
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

            case PacketType.JOINED_ROOM:  // you joined a room
                waitingForRoomChangeResponse = false;
                roomName = packet.ReadString();
                lobbyUI.updateRoomUI(roomName);
                len = packet.ReadInt();
                while (playersInMyRoom.Count > 1) {
                    playersInMyRoom.RemoveAt(playersInMyRoom.Count - 1);
                }
                for (int i = 0; i < len; ++i) {
                    playersInMyRoom.Add(new PlayerInfo(packet.ReadInt(), packet.ReadString(), packet.ReadColor()));
                }
                break;
            case PacketType.PLAYER_JOINED_SERVER:
                lobbyUI.logConnectionMessage(packet.ReadString(), packet.ReadColor(), true, true);
                break;
            case PacketType.PLAYER_LEFT_SERVER:
                lobbyUI.logConnectionMessage(packet.ReadString(), packet.ReadColor(), false, true);
                break;
            case PacketType.ROOM_LIST_UPDATE:



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
        return playersInMyRoom.AsReadOnly();
    }

    public PlayerInfo getMyPlayer() {
        return playersInMyRoom[0];
    }

}
