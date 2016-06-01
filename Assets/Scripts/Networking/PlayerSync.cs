using UnityEngine;
using System.Collections;

public class PlayerSync : MonoBehaviour {

    public int playerID { get; private set; }

    private GameClient gameClient = null;
    private bool initiliazed = false;

    private const float minDist = 0.1f;
    private Vector3 lastPos;
    private Vector3 targPos;

    public MeshRenderer mesh;

    void Awake() {
        playerID = -1;
    }

    // if a GameClient reference is not provided this will be considered
    // a remote player instance
    public void init(int playerID, Color32 color, GameClient gc = null) {
        if (initiliazed) {
            return;
        }
        initiliazed = true;
        this.playerID = playerID;
        lastPos = transform.position;
        targPos = transform.position;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", color);
        mesh.SetPropertyBlock(mpb);

        gameClient = gc;
        PlayerController pc = GetComponent<PlayerController>();
        if (gameClient) {
            pc.playerSync = this;
            return;
        }
        Destroy(pc);
        Destroy(GetComponent<Rigidbody>());
        Destroy(GetComponent<Collider>());
    }

    void Update() {
        // if gameClient is null then this means we are a remote player
        // so just interpolate to latest position received
        if (!gameClient) {
            transform.position = Vector3.Lerp(transform.position, targPos, Time.deltaTime * 20.0f);
            return;
        }

        Vector3 pos = transform.position;
        // only send new position if moved more than minDist since last time
        if (Vector3.SqrMagnitude(pos - lastPos) > minDist * minDist) {
            Packet p = new Packet(PacketType.STATE_UPDATE);
            p.Write(pos);
            gameClient.sendPacket(p);
            lastPos = pos;
            //Debug.Log("CLIENT: sending my players position");
        }

    }

    public void sendDeath() {
        Packet p = new Packet(PacketType.PLAYER_DIED);
        gameClient.sendPacket(p);
    }

    public void sendBomb(Vector3 pos, int range) {
        Packet p = new Packet(PacketType.SPAWN_BOMB);
        p.Write(pos);
        p.Write(range);
        gameClient.sendPacket(p);
    }

    public void updatePosition(Vector3 pos) {
        targPos = pos;
    }
}
