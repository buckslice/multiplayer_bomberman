using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour {
    public float fuseTimer = 3.0f;
    public AudioClip explosionSound;

    // where bomb is in tile array + range
    int x;
    int y;
    Level level;
    int range;

    // Use this for initialization
    void Start() {
    }

    public void init(int x, int y, Level level, int range) {
        this.x = x;
        this.y = y;
        this.level = level;
        this.range = range;
    }

    // Update is called once per frame
    void Update() {
        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0.0f) {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
            explode();
        }
    }

    public void explode() {
        level.spawnExplosion(x, y, 0, 0, range);
		Destroy(gameObject);
    }

    void OnTriggerExit(Collider c) {
        if(c.tag == "Player") {
            GetComponent<Collider>().isTrigger = false;
        }
    }

    public void enableTrigger() {
        GetComponent<Collider>().isTrigger = false;
    }

}
