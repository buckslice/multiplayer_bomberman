using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameClient : MonoBehaviour {
    public GameObject startServerButton;
    public GameObject startClientButton;
    public GameObject joinButton;
    public InputField nameInputField;
    public InputField passwordInputField;
    public Text statusText;
    private IEnumerator statusTextAnim;

    Packet loadPacket;

    Level level;

    byte channelReliable;
    int maxConnections = 4;

    public int playerNum;
    int port = 8887;
    int key = 420;
    int version = 1;
    int subversion = 0;

    int clientSocket = -1;  // this clients socket ID
    int serverSocket = -1;  // ID of server this client is connected to    

    bool waitingForLoginResponse = false;

    void OnEnable() {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        HostTopology topology = new HostTopology(config, maxConnections);

        clientSocket = NetworkTransport.AddHost(topology, port);
        Debug.Log("CLIENT: socket opened: " + clientSocket);

        byte error;
        NetworkTransport.SetBroadcastCredentials(clientSocket, key, version, subversion, out error);
        Debug.Log("CLIENT: started");

        // UI stuff
        Destroy(startServerButton);
        Destroy(startClientButton);
        statusText.gameObject.SetActive(true);

        statusTextAnim = statusTextAnimRoutine();
        StartCoroutine(statusTextAnim);

        Application.runInBackground = true; // for debugging purposes
        Destroy(gameObject.GetComponent<GameServer>());
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update() {
        checkMessages();
    }

    void OnLevelWasLoaded(int levelNum)
    {
        if (levelNum == 1)
        {
            level = GameObject.Find("Level").GetComponent<Level>();
            int length = loadPacket.ReadInt();
            Debug.Log("Starting Client For Loop");
            for (int i = 0; i < length; i++)
                level.setTile(i, (int)loadPacket.ReadByte());
            Debug.Log("Passed Client For Loop");
            level.BuildMesh();
        }
    }


    // sends a packet to the server
    public void sendPacket(Packet p) {
        byte error;
        NetworkTransport.Send(clientSocket, serverSocket, channelReliable, p.getData(), p.getSize(), out error);
    }

    public void checkMessages() {
        int recConnectionId;    // rec stands for received
        int recChannelId;
        int bsize = 1024;
        byte[] buffer = new byte[bsize];
        int dataSize;
        byte error;

        // continuously loop until there are no more messages
        while (true) {
            NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
                clientSocket, out recConnectionId, out recChannelId, buffer, bsize, out dataSize, out error);

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
                    Debug.Log("CLIENT: found server broadcast!");

                    // not doing anything with message currently
                    NetworkTransport.GetBroadcastConnectionMessage(clientSocket, buffer, bsize, out dataSize, out error);

                    // connect to broadcaster
                    string address;
                    int port;
                    NetworkTransport.GetBroadcastConnectionInfo(clientSocket, out address, out port, out error);
                    serverSocket = NetworkTransport.Connect(clientSocket, address, port, 0, out error);

                    StopCoroutine(statusTextAnim);
                    statusText.text = "Found Server!\nEnter Login Info:";
                    flashStatusText(Color.green);
                    nameInputField.gameObject.SetActive(true);
                    passwordInputField.gameObject.SetActive(true);
                    joinButton.SetActive(true);

                    break;
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

    public void receivePacket(Packet packet) {
        PacketType pt = (PacketType)packet.ReadByte();
        switch (pt) {
            case PacketType.LOGIN:
                waitingForLoginResponse = false;
                int success = packet.ReadInt();
                
                if (success != -1) {
                    playerNum = success;
                    Debug.Log("CLIENT: authenticated by server, joining game");
                    statusText.text = "Login successful!";
                    statusText.color = Color.yellow;
                    SceneManager.LoadScene(1);
                    loadPacket = packet;
                } else {
                    statusText.text = "Invalid login info!";
                    flashStatusText(Color.red);
                }
                break;
            default:
                break;
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
