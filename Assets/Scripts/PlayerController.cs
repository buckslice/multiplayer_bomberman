using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {

    private float speed = 5.0f;
    private Rigidbody rb;
    private Level level;
    private int bombLimit = 3;

    private SceneLoader loader;

    // Use this for initialization
    void Start() {
        rb = GetComponent<Rigidbody>();
        level = GameObject.Find("Level").GetComponent<Level>();
        transform.position = level.getRandomGroundPosition();

        loader = GameObject.Find("Canvas").GetComponent<SceneLoader>();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {  // lay bomb
            if (GameObject.FindGameObjectsWithTag("Bomb").Length < bombLimit) {
                level.placeBomb(transform.position);
            }

        }
        if (Input.GetKeyDown(KeyCode.Backspace)) {  // reset
            level.GenerateLevel();
            transform.position = level.getRandomGroundPosition();
            rb.velocity = Vector3.zero;
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
            loader.playDeathSequence();
        }
    }

    void OnCollisionEnter(Collision c) {
        if(c.collider.tag == "Enemy") {
            loader.playDeathSequence();
        }
    }

}
