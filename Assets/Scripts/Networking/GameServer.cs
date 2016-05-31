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
    private List<string> roomNames = new List<string>();
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
        channelReliable = config.AddChannel(QosType.ReliableSequenced);
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
        roomNames.Add("Lobby");
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
        for (int r = 0; r < players.Count; ++r) {
            for (int i = 0; i < players[r].Count; ++i) {
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
                {
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
                        sendPacket(p, clientID);

                        // tell everyone that a new player joined the server
                        Packet npPacket = new Packet(PacketType.PLAYER_JOINED_SERVER);
                        npPacket.Write(name);
                        npPacket.Write(color);
                        broadcastPacket(npPacket);

                        movePlayerToRoom(new PlayerState(clientID, name, color), 0);

                    } else {
                        if (loginSuccessful) {
                            p.Write(-2);    // if someone is already logged in with these credentials
                        } else {
                            p.Write(-1);    // invalid password
                        }
                        sendPacket(p, clientID);
                    }
                }
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
            case PacketType.CHAT_MESSAGE:
                p = new Packet(PacketType.CHAT_MESSAGE);
                p.Write(packet.ReadString());
                p.Write(packet.ReadColor());
                p.Write(packet.ReadString());
                broadcastToAllButOne(p, clientID, getPlayerRoom(clientID));   // only room they are in
                break;
            case PacketType.CHANGE_ROOM:
                {
                    bool creating = packet.ReadBool();  // is client trying to create a room
                    string roomName = packet.ReadString();
                    if (creating) {
                        if (roomNames.Contains(roomName) || roomName == "") { // fail
                            Debug.Log("SERVER: player failed to create room: " + roomName);
                            p = new Packet(PacketType.CHANGE_ROOM);
                            p.Write(true);
                            sendPacket(p, clientID);
                        } else {
                            Debug.Log("SERVER: player created room: " + roomName);
                            // create room
                            roomNames.Add(roomName);
                            players.Add(new List<PlayerState>());

                            movePlayerToRoom(getPlayerByID(clientID), players.Count - 1);

                            sendRoomListUpdate();
                        }
                    } else {
                        if (roomNames.Contains(roomName)) {
                            int roomIndex = roomNames.FindIndex(x => x == roomName);
                            movePlayerToRoom(getPlayerByID(clientID), roomIndex);
                        } else {
                            p = new Packet(PacketType.CHANGE_ROOM);
                            p.Write(false);
                            sendPacket(p, clientID);
                        }
                    }
                }
                break;

            default:
                break;
        }

    }

    // player will be moved into this room and sent a list of all other players in the room
    // all players in new room will be notified that this player joined
    private void movePlayerToRoom(PlayerState player, int roomIndex) {
        Debug.Assert(roomIndex >= 0 && roomIndex < players.Count);

        // remove player from list and recalculate indices
        if (playerIndices.ContainsKey(player.id)) { // checks incase the player is new to the server
            PlayerIndex pi = playerIndices[player.id];

            // tell players in current room that a player is leaving
            Packet bpPacket = new Packet(PacketType.PLAYER_LEFT_ROOM);
            bpPacket.Write(player.id);
            broadcastToAllButOne(bpPacket, player.id, pi.room);

            players[pi.room].RemoveAt(pi.index);
            recalculateIndices();
        }

        // give new player a list of other players in room
        Packet npPacket = new Packet(PacketType.JOINED_ROOM);
        npPacket.Write(roomNames[roomIndex]);    // send them the room name
        npPacket.Write(players[roomIndex].Count);  // number of players in room
        for (int i = 0; i < players[roomIndex].Count; ++i) {
            PlayerState ps = players[roomIndex][i];
            npPacket.Write(ps.id);
            npPacket.Write(ps.name);
            npPacket.Write(ps.color);
        }
        sendPacket(npPacket, player.id);

        players[roomIndex].Add(player);
        playerIndices[player.id] = new PlayerIndex(roomIndex, players[roomIndex].Count - 1);

        // tell other players in room that new player joined
        Packet opPacket = new Packet(PacketType.PLAYER_JOINED_ROOM);
        opPacket.Write(player.id);
        opPacket.Write(player.name);
        opPacket.Write(player.color);

        broadcastToAllButOne(opPacket, player.id, roomIndex);

        // check to see if there are any empty rooms
        bool shouldSendRoomUpdate = false;
        for (int i = 1; i < players.Count; ++i) {
            if (players[i].Count == 0) {
                players.RemoveAt(i);
                roomNames.RemoveAt(i);
                shouldSendRoomUpdate = true;
                i--;
            }
        }
        // send updated room list to all players in lobby
        if (shouldSendRoomUpdate) {
            sendRoomListUpdate();
        }

    }

    private void sendRoomListUpdate() {
        Packet ruPacket = new Packet(PacketType.ROOM_LIST_UPDATE);
        ruPacket.Write(roomNames.Count-1);
        for (int i = 1; i < roomNames.Count; ++i) {
            ruPacket.Write(roomNames[i]);       // send name of room
            ruPacket.Write(players[i].Count);   // send number of players in room
        }
        broadcastPacket(ruPacket, 0);   // broadcast packet to lobby
    }

    // remove client from player list if he is on it
    private void removeFromPlayers(int clientID) {
        if (playerIndices.ContainsKey(clientID)) {
            --numPlayers;

            // tell everyone that a player left the server
            PlayerState ps = getPlayerByID(clientID);
            Packet npPacket = new Packet(PacketType.PLAYER_LEFT_SERVER);
            npPacket.Write(ps.name);
            npPacket.Write(ps.color);
            broadcastToAllButOne(npPacket, clientID);

            PlayerIndex pi = playerIndices[clientID];
            players[pi.room].RemoveAt(pi.index);

            recalculateIndices();
        }
    }

    // recalculates mapping of ids to indices
    // should be done whenever a player moves between rooms or disconnects
    private void recalculateIndices() {
        playerIndices.Clear();
        for (int r = 0; r < players.Count; r++) {
            for (int i = 0; i < players[r].Count; i++) {
                playerIndices[players[r][i].id] = new PlayerIndex(r, i);
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
