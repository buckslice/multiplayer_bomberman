using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {

    float speed = 5.0f;
    Rigidbody rb;
    Level level;

    // Use this for initialization
    void Start() {
        rb = GetComponent<Rigidbody>();
        level = GameObject.Find("Level").GetComponent<Level>();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {  // lay bomb
            level.placeBomb(transform.position);
        }
        if (Input.GetKeyDown(KeyCode.Backspace)) {  // reset
            transform.position = new Vector3(11, 11, 11);
            rb.velocity = Vector3.zero;
            level.GenerateLevel();
        }
    }

    // Update is called once per frame
    void FixedUpdate() {
        Vector2 delta = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if(delta.sqrMagnitude > 1.0f) {
            delta.Normalize();
        }
        delta *= speed;
        Vector3 vel = rb.velocity;
        rb.velocity = new Vector3(delta.x, vel.y, delta.y);
    }
}
