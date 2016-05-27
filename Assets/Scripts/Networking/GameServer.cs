using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Net;
using System.Net.Sockets;

public class GameServer : MonoBehaviour {
    public GameObject startGameButton;

    private byte channelReliable;
    private int maxConnections = 4;
    private List<string> logins = new List<string>();

    private int port = 8888;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int serverSocket = -1;

    // list of connected clients
    private List<int> clients = new List<int>();
    // list of players in game
    private List<PlayerState> players = new List<PlayerState>();
    // maps playerID to their index in player list
    private Dictionary<int, int> playerIndices = new Dictionary<int, int>();

    private DatabaseUtil dbUtil;

    private Level level;
    private int winner = -1;

    class PlayerState {
        public int id;
        public string name;
        public Vector3 pos;
        public bool alive = true;
        public PlayerState(int id, string name, Vector3 pos = new Vector3()) {
            this.id = id;
            this.name = name;
            this.pos = pos;
        }
    }

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        //Destroy(gameObject.GetComponent<GameClient>());
        DontDestroyOnLoad(gameObject);
        key = gameObject.GetComponent<GameClient>().getKey();
        // start up database
        dbUtil = gameObject.AddComponent<DatabaseUtil>();

        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        HostTopology topology = new HostTopology(config, maxConnections);

        serverSocket = NetworkTransport.AddHost(topology, port);
        Debug.Log("SERVER: socket opened: " + serverSocket);

        Packet p = MakeTestPacket();

        byte error;
        bool success = NetworkTransport.StartBroadcastDiscovery(
                     serverSocket, port - 1, key, version, subversion, p.getData(), p.getSize(), 500, out error);

        if (!success) {
            Debug.Log("SERVER: start broadcast discovery failed!");
            Application.Quit();
            Destroy(gameObject);
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        //SceneManager.LoadScene(1);
    }

    private Packet MakeTestPacket() {
        IPHostEntry host;
        string localIP = "";
        host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                localIP = ip.ToString();
                Debug.Log(localIP);
                break;
            }
        }

        Packet p = new Packet(PacketType.MESSAGE);
        p.Write(localIP);
        //p.Write("HI ITS ME THE SERVER CONNECT UP");
        //p.Write(23.11074f);
        //p.Write(new Vector3(2.0f, 1.0f, 0.0f));
        return p;
    }

    private int getNumAlivePlayers() {
        int numAlive = 0;
        for (int i = 0; i < players.Count; ++i) {
            if (players[i].alive) {
                numAlive++;
            }
        }
        return numAlive;
    }

    // returns -1 if no winners yet
    // returns 0 if draw
    // returns an num > 0 indicating id of player who won
    private int checkForWinner() {
        int winner = -1;
        int numAlive = 0;
        for (int i = 0; i < players.Count; ++i) {
            if (players[i].alive) {
                if (++numAlive > 1) {
                    return -1;  // game still going
                }
                winner = players[i].id;
            }
        }
        if (numAlive == 1) {
            return winner;          // this player won!
        } else {
            return 0;               // no winners! a draw
        }
    }

    // Update is called once per frame
    void Update() {
        // send out packet to all connected players of positions
        Packet updatePacket = new Packet(PacketType.STATE_UPDATE);

        updatePacket.Write(getNumAlivePlayers());
        for (int i = 0; i < players.Count; ++i) {   // for each client send their id and position
            if (players[i].alive) {
                updatePacket.Write(players[i].id);
                updatePacket.Write(players[i].pos);
            }
        }
        broadcastPacket(updatePacket);

        checkMessages();
    }

    void LateUpdate() {
        if (winner != -1) {    // if game has ended then restart
            level.GenerateLevel();
            // figure out message to say at end
            string gameOverMessage = "It's a Draw!";
            if (winner > 0) {
                string playername = players[playerIndices[winner]].name;
                gameOverMessage = playername + " wins!";
            }

            for (int i = 0; i < players.Count; ++i) {
                Packet p = new Packet(PacketType.RESTART_GAME);
                p.Write(winner);
                int[] tiles = level.getTiles();
                p.Write(tiles.Length);
                for (int j = 0; j < tiles.Length; j++) {
                    p.Write((byte)tiles[j]);
                }
                p.Write(level.getRandomGroundPosition());
                p.Write(gameOverMessage);

                sendPacket(p, players[i].id);
                players[i].alive = true;
            }
            winner = -1;
        }
    }

    void OnLevelWasLoaded(int levelNum) {
        GameObject levelGO = GameObject.Find("Level");
        if (levelGO) {
            level = levelGO.GetComponent<Level>();
            level.GenerateLevel();
        }
    }

    private void sendPacket(Packet p, int clientID) {
        byte error;
        NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
    }

    // broadcasts packet to all connected players in game
    private void broadcastPacket(Packet packet) {
        for (int i = 0; i < players.Count; ++i) {
            sendPacket(packet, players[i].id);
        }
    }

    private void broadcastToAllButOne(Packet packet, int excludeClientID) {
        for (int i = 0; i < players.Count; ++i) {
            if (playerIndices[excludeClientID] == i) {
                continue;
            }
            sendPacket(packet, players[i].id);
        }
    }

    private void checkMessages() {
        int recConnectionID;    // rec stands for received
        int recChannelID;
        int bsize = 1024;
        byte[] buffer = new byte[bsize];
        int dataSize;
        byte error;

        while (true) {
            NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
                serverSocket, out recConnectionID, out recChannelID, buffer, bsize, out dataSize, out error);
            switch (recEvent) {
                case NetworkEventType.Nothing:
                    return;
                case NetworkEventType.DataEvent:
                    receivePacket(new Packet(buffer), recConnectionID);
                    break;
                case NetworkEventType.ConnectEvent:
                    clients.Add(recConnectionID);
                    Debug.Log("SERVER: client connected: " + recConnectionID);
                    break;
                case NetworkEventType.DisconnectEvent:

                    clients.Remove(recConnectionID);
                    Debug.Log("SERVER: client disconnected: " + recConnectionID);
                    removeFromPlayers(recConnectionID);

                    break;
                default:
                    break;

            }
        }

    }

    private void receivePacket(Packet packet, int clientID) {
        PacketType pt = (PacketType)packet.ReadByte();
        Packet p;   // return packet
        switch (pt) {
            case PacketType.LOGIN:
                string name = packet.ReadString();
                string password = packet.ReadString();

                // queries database for login info and returns success
                // will add new entry if name not found
                bool loginSuccessful = dbUtil.tryLogin(name, password);

                // send login response back to client
                p = new Packet(PacketType.LOGIN);
                if (loginSuccessful && !logins.Contains(name)) {
                    logins.Add(name);
                    p.Write(clientID);
                    int[] tiles = level.getTiles();
                    p.Write(tiles.Length);
                    for (int i = 0; i < tiles.Length; i++) {
                        p.Write((byte)tiles[i]);
                    }
                    // send player their spawn point
                    Vector3 spawn = level.getRandomGroundPosition();
                    p.Write(spawn);

                    // add player to game (and start sending them state info)
                    playerIndices[clientID] = players.Count;
                    players.Add(new PlayerState(clientID, name, spawn));

                } else if (logins.Contains(name) && loginSuccessful) {
                    p.Write(-2);
                } else {
                    p.Write(-1);
                }
                sendPacket(p, clientID);

                break;

            case PacketType.STATE_UPDATE:
                Debug.Assert(playerIndices.ContainsKey(clientID));
                players[playerIndices[clientID]].pos = packet.ReadVector3();
                break;

            case PacketType.SPAWN_BOMB:
                p = new Packet(PacketType.SPAWN_BOMB);
                p.Write(packet.ReadVector3());
                broadcastToAllButOne(p, clientID);
                break;

            case PacketType.PLAYER_DIED:
                players[playerIndices[clientID]].alive = false;
                winner = checkForWinner();

                break;
            default:
                break;
        }

    }


    // remove client from player list if he is on it
    private void removeFromPlayers(int clientID) {
        if (playerIndices.ContainsKey(clientID)) {
            logins.Remove(players[playerIndices[clientID]].name);
            players.RemoveAt(playerIndices[clientID]);
            // recalculate mapping of ids to indices
            playerIndices.Clear();
            for (int i = 0; i < players.Count; ++i) {
                playerIndices[players[i].id] = i;
            }
        }
    }
}
