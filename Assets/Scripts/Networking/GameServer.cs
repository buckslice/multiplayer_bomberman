using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameServer : MonoBehaviour {

    byte channelReliable;
    int maxConnections = 4;

    int port = 8888;
    int key = 420;
    int version = 1;
    int subversion = 0;

    int serverSocket = -1;
    List<int> clientConnections = new List<int>();

    void OnEnable() {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        HostTopology topology = new HostTopology(config, maxConnections);

        serverSocket = NetworkTransport.AddHost(topology, port);
        Debug.Log("SERVER: socket opened: " + serverSocket);

        Packet p = MakeTestPacket();

        byte error;
        bool b = NetworkTransport.StartBroadcastDiscovery(
                     serverSocket, port - 1, key, version, subversion, p.getData(), p.getSize(), 100, out error);

        if (!b) {
            Debug.Log("SERVER: start broadcast discovery failed!");
            Application.Quit();
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        // remove client script and travel to game scene
        Application.runInBackground = true; // for debugging purposes
        Destroy(gameObject.GetComponent<GameClient>());
        DontDestroyOnLoad(gameObject);
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
        checkMessages();

    }

    public void sendPacket(Packet p, int clientID) {
        byte error;
        NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
    }

    public void broadcastPacket(Packet packet) {
        for (int i = 0; i < clientConnections.Count; i++) {
            sendPacket(packet, clientConnections[i]);
        }
    }


    void checkMessages() {
        int recConnectionId;    // rec stands for received
        int recChannelId;
        int bsize = 1024;
        byte[] buffer = new byte[bsize];
        int dataSize;
        byte error;

        while (true) {
            NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
                serverSocket, out recConnectionId, out recChannelId, buffer, bsize, out dataSize, out error);
            switch (recEvent) {
                case NetworkEventType.Nothing:
                    return;
                case NetworkEventType.DataEvent:
                    receivePacket(new Packet(buffer), recConnectionId);
                    break;
                case NetworkEventType.ConnectEvent:
                    clientConnections.Add(recConnectionId);
                    Debug.Log("SERVER: client connected: " + recConnectionId);
                    break;
                case NetworkEventType.DisconnectEvent:
                    Debug.Log("SERVER: client disconnected: " + recConnectionId);
                    break;
                default:
                    break;

            }
        }

    }

    void receivePacket(Packet packet, int clientSocket) {
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
                p.Write(success);
                sendPacket(p, clientSocket);

                break;
            default:
                break;
        }

    }


}
