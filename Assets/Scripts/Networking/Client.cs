using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;

public class Client : MonoBehaviour {
	public InputField name_input;
	public InputField password_input;

	byte channelReliable;
	int maxConnections = 4;

	int serverSocket;
	int port = 8887;
	int key = 420;
	int version = 1;
	int subversion = 0;

	int clientSocket = -1;
	int serverConnection = -1;

	void Awake () {
		NetworkTransport.Init ();
		ConnectionConfig config = new ConnectionConfig ();
		channelReliable = config.AddChannel (QosType.Reliable);
		HostTopology topology = new HostTopology (config, maxConnections);

		clientSocket = NetworkTransport.AddHost(topology, port);
		Debug.Log("Client socket opened: " + clientSocket);

		byte error;
		NetworkTransport.SetBroadcastCredentials(clientSocket, key, version, subversion, out error);
		//NUtils.LogNetworkError(error);
		Debug.Log("Client started");
	}

	
	// Update is called once per frame
	void Update () {
		checkMessages ();
	}

	public void checkMessages() {
		//int recHostId;  // usually will be clientSocket
		int recConnectionId;
		int recChannelId;
		int bsize = 1024;
		byte[] buffer = new byte[bsize];
		int dataSize;
		byte error;

		while (true) {
			// when this is used network gets all received data
			//NetworkEventType recEvent = NetworkTransport.Receive(
			//    out recHostId, out recConnectionId, out recChannelId,
			//    buffer, bsize, out dataSize, out error);

			NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
				clientSocket, out recConnectionId, out recChannelId, buffer, bsize, out dataSize, out error);

			switch (recEvent) {
			case NetworkEventType.Nothing:
				return;
			case NetworkEventType.DataEvent:
				ReceivePacket(new Packet(buffer));
				break;

			case NetworkEventType.BroadcastEvent:
				if (serverConnection >= 0) { // already connected to a server
					break;
				}
				Debug.Log("CLIENT: found broadcaster!!!");

				NetworkTransport.GetBroadcastConnectionMessage(
					clientSocket, buffer, bsize, out dataSize, out error);

				Packet p = new Packet(buffer);
				p.ReadInt(); //network ID. Unused in this case.
				string s = p.ReadString();
				float f = p.ReadFloat();
				Vector3 v = p.ReadVector3();

				Debug.Log(s);
				Debug.Log(f);
				Debug.Log(v.ToString());

				string address;
				int port;
				NetworkTransport.GetBroadcastConnectionInfo(
					clientSocket, out address, out port, out error);

				serverConnection = NetworkTransport.Connect(
					clientSocket, address, port, 0, out error);
				Debug.Log("CLIENT: connected to server: " + serverConnection);

				break;
			case NetworkEventType.ConnectEvent:
				Debug.Log("CLIENT: connection received?");
				break;
			case NetworkEventType.DisconnectEvent:
				Debug.Log("CLIENT: someone disconnected?");
				break;
			default:
				break;
			}
		}
	}

	public void ReceivePacket(Packet p)
	{
		float f = p.ReadFloat ();
		Debug.Log ("CLIENT: " + f);
	}

	public void OnStartClick() {
		if (name_input.text == "" || password_input.text == "") {
			Debug.Log ("no name/password entered");
			return;
		}

		if (PlayerPrefs.HasKey (name_input.text)) {
			string pass = PlayerPrefs.GetString (name_input.text);
			if (pass == password_input.text) {
				UnityEngine.SceneManagement.SceneManager.LoadScene (1);
			} else {
				Debug.Log ("Wrong password");
			}
		} else {
			Debug.Log ("new player");
			PlayerPrefs.SetString (name_input.text, password_input.text);
			UnityEngine.SceneManagement.SceneManager.LoadScene (1);

		}

	}
}
