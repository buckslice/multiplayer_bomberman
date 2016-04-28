using UnityEngine;
using System.Collections;

public class PlayerSync : MonoBehaviour {

    public int playerID;

	// Use this for initialization
	void Awake () {
        GameObject netGO = GameObject.Find("Networking");
        if (netGO) {
            GameClient gc = netGO.GetComponent<GameClient>();
            if (gc && playerID != gc.playerID) {
                Destroy(GetComponent<PlayerController>());
                GetComponent<Rigidbody>().isKinematic = true;
            }
        }
    }

}
