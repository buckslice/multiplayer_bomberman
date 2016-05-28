using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

public class GameServer : MonoBehaviour {
    private byte channelReliable;
    private int maxConnections = 4;

    private int port = 8888;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int serverSocket = -1;

    // list of connected clients
    private List<int> clients = new List<int>();

    // list of connected players sorted by rooms (players[0] is lobby room
    private List<List<PlayerState>> players = new List<List<PlayerState>>();
    private int numPlayers = 0;

    // maps playerID to their index in player list
    private Dictionary<int, PlayerIndex> playerIndices = new Dictionary<int, PlayerIndex>();

    private DatabaseUtil dbUtil;

    private Level level;

    class PlayerIndex {
        public int room;
        public int index;
        public PlayerIndex(int room, int index) {
            this.room = room;
            this.index = index;
        }
    }

    class PlayerState {
        public int id;
        public string name;
        public Color32 color;
        public Vector3 pos;
        public bool alive = true;
        public PlayerState(int id, string name, Color32 color, Vector3 pos = new Vector3()) {
            this.id = id;
            this.name = name;
            this.color = color;
            this.pos = pos;
        }
    }

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        //Destroy(gameObject.GetComponent<GameClient>());
        DontDestroyOnLoad(gameObject);
        // start up database
        dbUtil = gameObject.AddComponent<DatabaseUtil>();

        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        HostTopology topology = new HostTopology(config, maxConnections);

        serverSocket = NetworkTransport.AddHost(topology, port);
        Debug.Log("SERVER: socket opened: " + serverSocket);

        Packet p = new Packet(PacketType.MESSAGE);
        p.Write("Hi its the server broadcasting!");

        byte error;
        bool success = NetworkTransport.StartBroadcastDiscovery(
                     serverSocket, port - 1, key, version, subversion, p.getData(), p.getSize(), 500, out error);

        if (!success) {
            Debug.Log("SERVER: start broadcast discovery failed!");
            //Application.Quit();
            Destroy(this);
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        // create lobby room 0
        players.Add(new List<PlayerState>());
        //SceneManager.LoadScene(1);
    }

    // Update is called once per frame
    void Update() {

        // for each active room send out state updates
        for (int room = 1; room < players.Count; ++room) {
            List<PlayerState> playersInRoom = players[room];
            if (playersInRoom.Count < 2) { // room still filling up
                continue;
            }
            Packet updatePacket = new Packet(PacketType.STATE_UPDATE);

            // find number of alive players in this room (also check for winner while your at it)
            int winner = -1;
            int numAlive = 0;
            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].alive) {
                    ++numAlive;
                    winner = playersInRoom[i].id;
                }
            }
            if (numAlive == 0) {
                // draw
            } else if (numAlive == 1) {
                // player 'winner' won
            }// else game still going

            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].alive) {
                    numAlive++;
                }
            }
            updatePacket.Write(numAlive);

            // send positions of ether players in room
            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].alive) {
                    updatePacket.Write(playersInRoom[i].id);
                    updatePacket.Write(playersInRoom[i].pos);
                }
            }
            // send out packet to all players in room
            broadcastPacket(updatePacket, room);

        }

        checkMessages();
    }

    void LateUpdate() {
        //if (winner != -1) {    // if game has ended then restart
        //    level.GenerateLevel();
        //    // figure out message to say at end
        //    string gameOverMessage = "It's a Draw!";
        //    if (winner > 0) {
        //        string playername = players[playerIndices[winner]].name;
        //        gameOverMessage = playername + " wins!";
        //    }

        //    for (int i = 0; i < players.Count; ++i) {
        //        Packet p = new Packet(PacketType.RESTART_GAME);
        //        p.Write(winner);
        //        int[] tiles = level.getTiles();
        //        p.Write(tiles.Length);
        //        for (int j = 0; j < tiles.Length; j++) {
        //            p.Write((byte)tiles[j]);
        //        }
        //        p.Write(level.getRandomGroundPosition());
        //        p.Write(gameOverMessage);

        //        sendPacket(p, players[i].id);
        //        players[i].alive = true;
        //    }
        //    winner = -1;
        //}
    }

    void OnLevelWasLoaded(int levelNum) {
        GameObject levelGO = GameObject.Find("Level");
        if (levelGO) {
            level = levelGO.GetComponent<Level>();
            //level.GenerateLevel();
        }
    }

    private void sendPacket(Packet p, int clientID) {
        byte error;
        NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
    }

    // broadcasts packet to all connected players
    private void broadcastPacket(Packet packet) {
        for(int r = 0; r < players.Count; ++r) {
            for(int i = 0; i < players[r].Count; ++i) {
                sendPacket(packet, players[r][i].id);
            }
        }
    }
    // same as above but to certain room only
    private void broadcastPacket(Packet packet, int room) {
        for (int i = 0; i < players[room].Count; ++i) {
            sendPacket(packet, players[room][i].id);
        }
    }
    // broadcasts packet to all connected players except one
    private void broadcastToAllButOne(Packet packet, int excludeClientID) {
        for (int r = 0; r < players.Count; ++r) {
            for (int i = 0; i < players[r].Count; ++i) {
                if (players[r][i].id == excludeClientID) {
                    continue;
                }
                sendPacket(packet, players[r][i].id);
            }
        }
    }
    // same as above but to certain room only
    private void broadcastToAllButOne(Packet packet, int excludeClientID, int room) {
        for (int i = 0; i < players[room].Count; ++i) {
            if (players[room][i].id == excludeClientID) {
                continue;
            }
            sendPacket(packet, players[room][i].id);
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
                if (loginSuccessful && isNameUnique(name)) {
                    p.Write(clientID);
                    p.Write(name);
                    // assign player a random color (use hue shifting so always bright)
                    Color32 color = Color.HSVToRGB(Random.value, 1.0f, 1.0f);
                    p.Write(color);

                    // send this new player rest of current players
                    p.Write(numPlayers);
                    for (int r = 0; r < players.Count; ++r) {
                        for (int i = 0; i < players[r].Count; ++i) {
                            PlayerState player = players[r][i];
                            p.Write(player.id);
                            p.Write(player.name);
                            p.Write(player.color);
                        }
                    }

                    // add player to game lobby
                    playerIndices[clientID] = new PlayerIndex(0, players[0].Count);
                    players[0].Add(new PlayerState(clientID, name, color));
                    ++numPlayers;

                    // tell rest of players that new player joined
                    Packet pJoinPacket = new Packet(PacketType.PLAYER_JOIN);
                    pJoinPacket.Write(clientID);
                    pJoinPacket.Write(name);
                    pJoinPacket.Write(color);

                    broadcastToAllButOne(pJoinPacket, clientID);

                } else if (loginSuccessful) {
                    p.Write(-2);    // if someone is already logged in with these credentials
                } else {
                    p.Write(-1);    // invalid password
                }
                sendPacket(p, clientID);

                break;

            case PacketType.STATE_UPDATE:
                getPlayerByID(clientID).pos = packet.ReadVector3();
                break;

            case PacketType.SPAWN_BOMB:
                p = new Packet(PacketType.SPAWN_BOMB);
                p.Write(packet.ReadVector3());
                broadcastToAllButOne(p, clientID, getPlayerRoom(clientID));
                break;

            case PacketType.PLAYER_DIED:
                getPlayerByID(clientID).alive = false;
                break;
            default:
                break;
        }

    }


    // remove client from player list if he is on it
    private void removeFromPlayers(int clientID) {
        if (playerIndices.ContainsKey(clientID)) {
            --numPlayers;
            PlayerIndex pi = playerIndices[clientID];

            // tell everyone this player left
            Packet pLeftPacket = new Packet(PacketType.PLAYER_LEFT);
            pLeftPacket.Write(clientID);
            broadcastToAllButOne(pLeftPacket, clientID);

            players[pi.room].RemoveAt(pi.index);
            // recalculate mapping of ids to indices
            playerIndices.Clear();
            for (int r = 0; r < players.Count; r++) {
                for (int i = 0; i < players[r].Count; i++) {
                    playerIndices[players[r][i].id] = new PlayerIndex(r, i);
                }
            }
        }
    }

    // return room player is in
    private int getPlayerRoom(int clientID) {
        return playerIndices[clientID].room;
    }

    private PlayerState getPlayerByID(int clientID) {
        Debug.Assert(playerIndices.ContainsKey(clientID));
        PlayerIndex pi = playerIndices[clientID];
        return players[pi.room][pi.index];
    }

    // check if any currently connected players has this name
    private bool isNameUnique(string name) {
        for (int r = 0; r < players.Count; ++r) {
            for (int i = 0; i < players[r].Count; ++i) {
                if (players[r][i].name == name) {
                    return false;
                }
            }
        }
        return true;
    }

}
