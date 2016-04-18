using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour {
	public AudioClip sfx;

    float fuseTimer = 3.0f;

    // where bomb is in tile array
    int x;
    int y;
    Level level;

    // Use this for initialization
    void Start() {
    }

    public void init(int x, int y, Level level) {
        this.x = x;
        this.y = y;
        this.level = level;
    }

    // Update is called once per frame
    void Update() {
        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0.0f) {
			AudioSource.PlayClipAtPoint(sfx, GameObject.FindGameObjectWithTag("MainCamera").transform.position);
            explode();
        }
    }

    public void explode() {
        level.spawnExplosion(x, y, 0, 0, 3);
		Destroy(gameObject);
    }

    void OnTriggerExit(Collider c) {
        if(c.tag == "Player") {
            GetComponent<Collider>().isTrigger = false;
        }
    }

}
