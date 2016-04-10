using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour {

    // types
    // random walk around
    // pathfind after player if see them
    // pathfind after always

    Rigidbody rb;
    private Vector3 path = Vector3.zero;
    private int x, y, lastX, lastY;
    float timeSinceUpdate = 0.0f;

    // Use this for initialization
    void Start () {
        rb = GetComponent<Rigidbody>();
        Vector3 p = Pathfinder.instance.getRandomGroundPosition();
        p.y = 0.0f;
        transform.position = p;
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    // Update is called once per frame
    void FixedUpdate() {
        if (!rb) {
            path = Vector3.zero;
            return;
        }

        x = (int)(transform.position.x / Level.SIZE);
        y = (int)(transform.position.z / Level.SIZE);

        timeSinceUpdate -= Time.deltaTime;
        if ((x != lastX || y != lastY || path == Vector3.zero) || timeSinceUpdate < 0.0f) {
            path = Pathfinder.instance.getPath(transform.position.x, transform.position.z);
            timeSinceUpdate = 0.5f;
        }

        rb.velocity = path * 2f;

        // clamp velocity to maxspeed
        float max = 5.0f;
        if (rb.velocity.sqrMagnitude > max*max) {
            rb.velocity = rb.velocity.normalized * max;
        }

        lastX = x;
        lastY = y;
    }

    bool respawning = false;
    void OnTriggerEnter(Collider col) {
        if (!respawning && col.tag == "Explosion") {
            respawning = true;
            StartCoroutine(pauseThenReload());
        }
    }

    IEnumerator pauseThenReload() {
        yield return new WaitForSeconds(1.0f);
        Vector3 p = Pathfinder.instance.getRandomGroundPosition();
        p.y = 0.0f;
        transform.position = p;
        respawning = false;
    }

}
