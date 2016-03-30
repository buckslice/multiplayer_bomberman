using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour {

    float fuseTimer = 3.0f;

    // where bomb is in tile array
    int tileX;
    int tileY;
    Level level;

    // Use this for initialization
    void Start() {

    }

    public void init(int tileX, int tileY, Level level) {
        this.tileX = tileX;
        this.tileY = tileY;
        this.level = level;
    }

    // Update is called once per frame
    void Update() {
        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0.0f) {
            level.explodeBomb(tileX, tileY);
            Destroy(gameObject);
        }
    }
}
