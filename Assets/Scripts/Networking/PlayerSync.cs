using UnityEngine;
using System.Collections;

public class PlayerSync : MonoBehaviour {

    public int playerID = -1;

    private bool initialized = false;
    public void initAsRemotePlayer(int playerID) {
        if (initialized) {
            return;
        }
        initialized = true;
        this.playerID = playerID;
        Destroy(GetComponent<PlayerController>());
        GetComponent<Rigidbody>().isKinematic = true;
    }

    public void syncPosition(Vector3 pos) {
        // TODO add in interpolation
        transform.position = pos;
    }
}
