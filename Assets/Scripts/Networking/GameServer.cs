using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameServer : MonoBehaviour {

    private byte channelReliable;
    private int maxConnections = 4;

    private int port = 8888;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private Level level;

    private int serverSocket = -1;
    
    // list of connected clients
    private List<int> clients = new List<int>();
    // list of players in game
    private List<PlayerState> players = new List<PlayerState>();
    // maps playerID to their index in player list
    private Dictionary<int, int> playerIndices = new Dictionary<int, int>();

    class PlayerState {
        public int id;
        public Vector3 pos;
        public PlayerState(int id, Vector3 pos = new Vector3()) {
            this.id = id;
            this.pos = pos;
        }
    }

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        Destroy(gameObject.GetComponent<GameClient>());
        DontDestroyOnLoad(gameObject);

        // for testing until we get database working
        PlayerPrefs.DeleteAll();

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
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        SceneManager.LoadScene(1);
    }

    Packet MakeTestPacket() {
        Packet p = new Packet(PacketType.MESSAGE);
        p.Write("HI ITS ME THE SERVER CONNECT UP");
        p.Write(23.11074f);
        p.Write(new Vector3(2.0f, 1.0f, 0.0f));
        return p;
    }

    // Update is called once per frame
    void Update() {
        // send out packet to all connected players of positions
        Packet updatePacket = new Packet(PacketType.STATE_UPDATE);
        updatePacket.Write(players.Count); // first write number of players connected
        for (int i = 0; i < players.Count; ++i) {   // for each client send their id and position
            updatePacket.Write(players[i].id);
            updatePacket.Write(players[i].pos);
        }
        broadcastPacket(updatePacket);


        checkMessages();
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
        for (int i = 0; i < players.Count; i++) {
            sendPacket(packet, players[i].id);
        }
    }

    void checkMessages() {
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
                    players.RemoveAt(playerIndices[recConnectionID]);
                    // recalculate indices for all players
                    playerIndices.Clear();
                    for (int i = 0; i < players.Count; ++i) {
                        playerIndices[players[i].id] = i;
                    }
                    Debug.Log("SERVER: client disconnected: " + recConnectionID);
                    break;
                default:
                    break;

            }
        }

    }

    void receivePacket(Packet packet, int clientID) {
        PacketType pt = (PacketType)packet.ReadByte();
        switch (pt) {
            case PacketType.LOGIN:
                string name = packet.ReadString();
                string password = packet.ReadString();
                bool success = true;
                if (PlayerPrefs.HasKey(name)) {
                    if (password == PlayerPrefs.GetString(name)) {
                        Debug.Log("SERVER: player login accepted");
                    } else {
                        success = false;
                        Debug.Log("SERVER: player login denied, wrong password");
                    }
                } else {
                    Debug.Log("SERVER: new player \"" + name + "\" joined with password \"" + password + "\"");
                    PlayerPrefs.SetString(name, password);
                }

                // send login response back to client
                Packet p = new Packet(PacketType.LOGIN);
                if (success) {
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
                    players.Add(new PlayerState(clientID, spawn));

                } else {
                    p.Write(-1);
                }
                sendPacket(p, clientID);

                break;

            case PacketType.STATE_UPDATE:
                Debug.Assert(playerIndices.ContainsKey(clientID));
                players[playerIndices[clientID]].pos = packet.ReadVector3();
                break;
            default:
                break;
        }

    }
}
