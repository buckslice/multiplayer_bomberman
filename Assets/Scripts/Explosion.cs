using UnityEngine;
using System.Collections;

public class Explosion : MonoBehaviour {
    int x;
    int y;
    int dx = 0;
    int dy = 0;
    int life;
    Level level;

    // Use this for initialization
    void Start() {
    }

    public void start(int x, int y, int dx, int dy, int life, Level level) {
        this.x = x;
        this.y = y;
        this.dx = dx;
        this.dy = dy;
        this.life = life;
        this.level = level;

        StartCoroutine(explosionSequence());
    }

    IEnumerator explosionSequence() {
        yield return new WaitForSeconds(0.10f);
        if (life > 0) {
            --life;
            if (dx == 0 && dy == 0) {   // center bomb
                level.spawnExplosion(x - 1, y, -1, 0, life);
                level.spawnExplosion(x + 1, y, +1, 0, life);
                level.spawnExplosion(x, y - 1, 0, -1, life);
                level.spawnExplosion(x, y + 1, 0, +1, life);
            } else {
                level.spawnExplosion(x + dx, y + dy, dx, dy, life);
            }
        }
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

}
