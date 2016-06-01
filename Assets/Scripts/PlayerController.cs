using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {
    private float speed = 5.0f;
    private Rigidbody rb;
    public int bombLimit = 1;
    public int bombRange = 2;

    public PlayerSync playerSync { private get; set; }
    private Level level;
    private AudioSource source;

    // Use this for initialization
    void Start() {
        source = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        level = GameObject.Find("Level").GetComponent<Level>();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {  // lay bomb
            if (GameObject.FindGameObjectsWithTag("PlayerBomb").Length < bombLimit) {
                level.placeBomb(transform.position, true, bombRange);
                playerSync.sendBomb(transform.position, bombRange);
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate() {
        Vector2 delta = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if (delta.sqrMagnitude > 1.0f) {
            delta.Normalize();
        }
        delta *= speed;
        Vector3 vel = rb.velocity;
        rb.velocity = new Vector3(delta.x, vel.y, delta.y);
    }

    void OnTriggerEnter(Collider col) {
        if (col.tag == "Explosion") {
            source.Play();
            playerSync.sendDeath();
        }
    }

    void OnCollisionEnter(Collision c) {
        if(c.collider.tag == "Enemy") {
            source.Play();
        }
    }

}
