using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour {

    private float speed = 5.0f;
    private Rigidbody rb;
    private Level level;
    private bool reloading = false;
    private int bombLimit = 3;

    // Use this for initialization
    void Start() {
        rb = GetComponent<Rigidbody>();
        level = GameObject.Find("Level").GetComponent<Level>();
        transform.position = level.getRandomGroundPosition();
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
        if (!reloading && col.tag == "Explosion") {
            reloading = true;
            //SceneManager.LoadScene (SceneManager.GetActiveScene ().buildIndex);
            StartCoroutine(pauseThenReload());
        }
    }

    IEnumerator pauseThenReload() {
        Time.timeScale = 0.0f;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup < start + 1.0f) {
            yield return 0;
        }
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        reloading = false;
    }
}
