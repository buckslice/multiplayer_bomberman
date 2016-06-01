using UnityEngine;
using System.Collections;

public class PowerUp : MonoBehaviour {
    public AudioClip pickupSound;

    // where bomb is in tile array + range
    int x;
    int y;
    Level level;
    int type;


    public void init(int x, int y, Level level, int type) {
        this.x = x;
        this.y = y;
        this.level = level;
        this.type = type;
    }

    void OnTriggerEnter(Collider col) {
        AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (col.tag == "Player") {
            if (type == 1) {
                col.GetComponent<PlayerController>().bombRange++;
            } else {
                col.GetComponent<PlayerController>().bombRange--;
            }
            Destroy(gameObject);
        }
    }
}
