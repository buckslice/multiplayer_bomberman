using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;

public class Server : MonoBehaviour {

	byte channelReliable;
	int maxConnections = 4;

	int serverSocket;
	int port = 8888;
	int key = 420;
	int version = 1;
	int subversion = 0;

	List<int> clientConnection = new List<int> ();

	void Awake () {
		NetworkTransport.Init ();
		ConnectionConfig config = new ConnectionConfig ();
		channelReliable = config.AddChannel (QosType.Reliable);
		HostTopology topology = new HostTopology (config, maxConnections);

		serverSocket = NetworkTransport.AddHost (topology, port);
		Debug.Log ("server socket opened: " + serverSocket);

		Packet p = MakeTestPacket();
		StartCoroutine(StartBroadcast(p, port-1));

	}
	Packet MakeTestPacket()
	{
		Packet p = new Packet();
		p.Write(0);
		p.Write("HI ITS ME THE SERVER CONNECT UP");
		p.Write(23.11074f);
		p.Write(new Vector3(2.0f, 1.0f, 0.0f));
		return p;
	}
	
	// Update is called once per frame
	void Update () {
		checkMessages ();
		for (int i = 0; i < clientConnection.Count; i++) {
			Packet p = new Packet ();
			p.Write (Time.realtimeSinceStartup);
			SendPacket (p, clientConnection[i]);
		}
	}

	public void SendPacket(Packet p, int clientID)
	{
		byte error;
		NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
	}


	void checkMessages() {
		int recConnectionId;
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
				ReceivePacket (buffer, recConnectionId);
				break;
			case NetworkEventType.ConnectEvent:
				clientConnection.Add (recConnectionId);
				Debug.Log ("client connected");
				break;
			case NetworkEventType.DisconnectEvent:
				Debug.Log ("disconnecting");
				break;
			default:
				break;

			}
		}

	}

	void ReceivePacket(byte[] buf, int clientPortNum) {
	}

	IEnumerator StartBroadcast(Packet p, int clientPort) {
		while (NetworkTransport.IsBroadcastDiscoveryRunning ())
			yield return new WaitForEndOfFrame ();

		byte error;
		bool b = NetworkTransport.StartBroadcastDiscovery (
			         serverSocket, clientPort, key, version, subversion, p.getData (), p.getSize (), 100, out error);

		if (!b)
		{
			Debug.Log("QUIT EVENT");
			Application.Quit();
		}
		else if (NetworkTransport.IsBroadcastDiscoveryRunning())
		{
			Debug.Log("Server started and broadcasting");
		}
		else
		{
			Debug.Log("Server started but not broadcasting!!!");
		}
	}

}
