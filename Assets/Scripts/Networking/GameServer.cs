using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

public class GameServer : MonoBehaviour {
    private byte channelReliable;
    private int maxConnections = 16;

    private int port = 8888;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private int serverSocket = -1;

    // list of connected clients
    private List<int> clients = new List<int>();

    // list of connected players sorted by rooms (players[0] is lobby room
    //private List<List<PlayerState>> players = new List<List<PlayerState>>();
    private List<Room> rooms = new List<Room>();

    // maps playerID to their index in player list
    private Dictionary<int, PlayerIndex> playerIndices = new Dictionary<int, PlayerIndex>();

    private DatabaseUtil dbUtil;

    class Room {
        public string name;
        public float countdownTimer = 5.0f;
        public List<PlayerState> players = new List<PlayerState>();
        public LevelData level = new LevelData();
        public Room(string name) {
            this.name = name;
        }
    }

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
        public bool ready = false;
        public PlayerState(int id, string name, Color32 color, Vector3 pos = new Vector3()) {
            this.id = id;
            this.name = name;
            this.color = color;
            this.pos = pos;
        }
        public void reset() {
            ready = false;
            alive = true;
            pos = Vector3.zero;
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
            ResetToMenu.Reset();
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        // create lobby room at index 0
        rooms.Add(new Room("Lobby"));

    }

    // Update is called once per frame
    void Update() {
        updateRooms();

        checkMessages();
    }

    void updateRooms() {
        // check each room thats not the lobby
        for (int roomIndex = 1; roomIndex < rooms.Count; ++roomIndex) {
            Room room = rooms[roomIndex];
            List<PlayerState> playersInRoom = rooms[roomIndex].players;

            //if game not going
            if (room.countdownTimer > 0) {
                // check if all players are ready
                bool allReady = true;
                for (int i = 0; i < playersInRoom.Count; ++i) {
                    if (!playersInRoom[i].ready) {
                        allReady = false;
                        break;
                    }
                }
                if (allReady && playersInRoom.Count >= 2) {
                    int btime = (int)(room.countdownTimer + 1);
                    room.countdownTimer -= Time.deltaTime;
                    int ptime = (int)(room.countdownTimer + 1);
                    if (ptime != btime) {
                        if (ptime == 0) {
                            generateAndSendStartPackets(roomIndex);
                        } else {
                            Packet cdPacket = new Packet(PacketType.GAME_COUNTDOWN);
                            cdPacket.Write(ptime);
                            broadcastPacket(cdPacket, roomIndex);
                        }
                    }
                } else {
                    room.countdownTimer = 5.0f;
                }
                continue;
            }

            // find number of alive players in this room (also check for winner while your at it)
            PlayerState winner = null;
            int numAlive = 0;
            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].alive) {
                    ++numAlive;
                    winner = playersInRoom[i];
                }
            }
            if (numAlive <= 1) { // if either of these then game is over
                Packet gpacket = new Packet(PacketType.GAME_END);
                string message = (numAlive == 0 ? "It's a Draw!" : winner.name + " Wins!");
                gpacket.Write(message);
                broadcastPacket(gpacket, roomIndex);

                for (int i = 0; i < playersInRoom.Count; ++i) {
                    Debug.Log("moving player to lobby " + playersInRoom[i].name);
                    movePlayerToRoom(playersInRoom[i], 0);  // move them back to lobby
                }
                --roomIndex;
                continue;
            }

            // else game still going so send state updates
            Packet updatePacket = new Packet(PacketType.STATE_UPDATE);
            updatePacket.Write(playersInRoom.Count);
            // send positions of other alive players in room
            for (int i = 0; i < playersInRoom.Count; ++i) {
                if (playersInRoom[i].alive) {
                    updatePacket.Write(playersInRoom[i].id);
                    updatePacket.Write(playersInRoom[i].pos);
                }
            }
            // send out packet to all players in room
            broadcastPacket(updatePacket, roomIndex);

        }
    }

    private void generateAndSendStartPackets(int roomIndex) {
        LevelData level = rooms[roomIndex].level;
        level.generateLevel();

        List<PlayerState> players = rooms[roomIndex].players;
        for (int i = 0; i < players.Count; ++i) {
            Packet cdPacket = new Packet(PacketType.GAME_COUNTDOWN);
            cdPacket.Write(0);
            // send level data
            int[] tiles = level.getTiles();
            cdPacket.Write(tiles.Length);
            for (int j = 0; j < tiles.Length; j++) {
                cdPacket.Write((byte)tiles[j]);
            }
            // give each player a spawn point
            for (int j = 0; j < players.Count; ++j) {
                players[j].pos = level.getRandomGroundPosition();
                cdPacket.Write(players[j].pos);
            }

            sendPacket(cdPacket, players[i].id);
        }
    }

    private void sendPacket(Packet p, int clientID) {
        byte error;
        NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
    }

    // broadcasts packet to all connected players
    private void broadcastPacket(Packet packet) {
        for (int r = 0; r < rooms.Count; ++r) {
            for (int i = 0; i < rooms[r].players.Count; ++i) {
                sendPacket(packet, rooms[r].players[i].id);
            }
        }
    }
    // same as above but to certain room only
    private void broadcastPacket(Packet packet, int room) {
        for (int i = 0; i < rooms[room].players.Count; ++i) {
            sendPacket(packet, rooms[room].players[i].id);
        }
    }
    // broadcasts packet to all connected players except one
    private void broadcastToAllButOne(Packet packet, int excludeClientID) {
        for (int r = 0; r < rooms.Count; ++r) {
            for (int i = 0; i < rooms[r].players.Count; ++i) {
                if (rooms[r].players[i].id == excludeClientID) {
                    continue;
                }
                sendPacket(packet, rooms[r].players[i].id);
            }
        }
    }
    // same as above but to certain room only
    private void broadcastToAllButOne(Packet packet, int excludeClientID, int room) {
        for (int i = 0; i < rooms[room].players.Count; ++i) {
            if (rooms[room].players[i].id == excludeClientID) {
                continue;
            }
            sendPacket(packet, rooms[room].players[i].id);
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
                        Debug.Log("SERVER: new player \"" + name + "\" joined with password \"" + password + "\"");

                        p.Write(clientID);
                        p.Write(name);
                        sendPacket(p, clientID);    // send login response for client

                        // assign player a random color (use hue shifting so always bright)
                        Color32 color = Color.HSVToRGB(Random.value, 1.0f, 1.0f);

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
                p.Write(packet.ReadInt());
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
                        if (roomExists(roomName) || roomName == "") { // fail
                            Debug.Log("SERVER: player failed to create room: " + roomName);
                            p = new Packet(PacketType.CHANGE_ROOM);
                            p.Write(true);
                            sendPacket(p, clientID);
                        } else {
                            Debug.Log("SERVER: player created room: " + roomName);
                            // create room
                            rooms.Add(new Room(roomName));
                            movePlayerToRoom(getPlayerByID(clientID), rooms.Count - 1);
                        }
                    } else {
                        if (roomExists(roomName)) {
                            int roomIndex = rooms.FindIndex(x => x.name == roomName);
                            movePlayerToRoom(getPlayerByID(clientID), roomIndex);
                        } else {
                            p = new Packet(PacketType.CHANGE_ROOM);
                            p.Write(false);
                            sendPacket(p, clientID);
                        }
                    }
                }
                break;
            case PacketType.SET_READY:
                p = new Packet(PacketType.SET_READY);
                PlayerState ps = getPlayerByID(clientID);
                ps.ready = packet.ReadBool();
                p.Write(ps.name);
                p.Write(ps.ready);
                broadcastToAllButOne(p, clientID, getPlayerRoom(clientID));
                break;
            default:
                break;
        }

    }

    // player will be moved into this room and sent a list of all other players in the room
    // all players in new room will be notified that this player joined
    private void movePlayerToRoom(PlayerState player, int roomIndex) {
        Debug.Assert(roomIndex >= 0 && roomIndex < rooms.Count);
        player.reset();
        // remove player from list and recalculate indices
        if (playerIndices.ContainsKey(player.id)) { // checks incase the player is new to the server
            PlayerIndex pi = playerIndices[player.id];

            // tell players in current room that a player is leaving
            Packet bpPacket = new Packet(PacketType.PLAYER_LEFT_ROOM);
            bpPacket.Write(player.id);
            broadcastToAllButOne(bpPacket, player.id, pi.room);

            rooms[pi.room].players.RemoveAt(pi.index);
        }
        rooms[roomIndex].players.Add(player);
        recalculateIndices();

        // give new player a list of all players in the room
        Packet npPacket = new Packet(PacketType.YOU_JOINED_ROOM);
        npPacket.Write(rooms[roomIndex].name);    // send them the room name
        int numPlayers = rooms[roomIndex].players.Count;
        npPacket.Write(numPlayers);  // number of players in room
        for (int i = 0; i < numPlayers; ++i) {
            PlayerState ps = rooms[roomIndex].players[i];
            npPacket.Write(ps.id);
            npPacket.Write(ps.name);
            npPacket.Write(ps.color);
            npPacket.Write(ps.ready);
        }
        sendPacket(npPacket, player.id);

        // tell other players in room that new player joined
        Packet opPacket = new Packet(PacketType.PLAYER_JOINED_ROOM);
        opPacket.Write(player.id);
        opPacket.Write(player.name);
        opPacket.Write(player.color);

        broadcastToAllButOne(opPacket, player.id, roomIndex);

        // checks and removes any empty rooms
        for (int i = 1; i < rooms.Count; ++i) {
            if (rooms[i].players.Count == 0) {
                Debug.Log("SERVER: closing down empty room: " + rooms[i].name);
                rooms.RemoveAt(i--);
            }
        }

        // broadcast a new roomlist packet to everyone in lobby
        Packet ruPacket = new Packet(PacketType.ROOM_LIST_UPDATE);
        ruPacket.Write(rooms.Count - 1);
        for (int i = 1; i < rooms.Count; ++i) {
            ruPacket.Write(rooms[i].name);           // send name of room
            ruPacket.Write(rooms[i].players.Count);  // send number of players in room
        }
        broadcastPacket(ruPacket, 0);
    }

    // remove client from player list if he is on it
    private void removeFromPlayers(int clientID) {
        if (playerIndices.ContainsKey(clientID)) {
            // tell everyone that a player left the server
            PlayerState ps = getPlayerByID(clientID);
            Packet npPacket = new Packet(PacketType.PLAYER_LEFT_SERVER);
            npPacket.Write(ps.name);
            npPacket.Write(ps.color);
            broadcastToAllButOne(npPacket, clientID);

            // also tell people he left the room
            Packet lrPacket = new Packet(PacketType.PLAYER_LEFT_ROOM);
            lrPacket.Write(ps.id);
            broadcastPacket(lrPacket, getPlayerRoom(ps.id));

            PlayerIndex pi = playerIndices[clientID];
            rooms[pi.room].players.RemoveAt(pi.index);

            recalculateIndices();
        }
    }

    // recalculates mapping of ids to indices
    // should be done whenever a player moves between rooms or disconnects
    private void recalculateIndices() {
        playerIndices.Clear();
        for (int r = 0; r < rooms.Count; r++) {
            for (int i = 0; i < rooms[r].players.Count; i++) {
                playerIndices[rooms[r].players[i].id] = new PlayerIndex(r, i);
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
        return rooms[pi.room].players[pi.index];
    }

    private bool isNameUnique(string name) {
        for (int r = 0; r < rooms.Count; ++r) {
            for (int i = 0; i < rooms[r].players.Count; ++i) {
                if (rooms[r].players[i].name == name) {
                    return false;
                }
            }
        }
        return true;
    }
    private bool roomExists(string name) {
        for (int i = 0; i < rooms.Count; ++i) {
            if (rooms[i].name == name) {
                return true;
            }
        }
        return false;
    }

}
