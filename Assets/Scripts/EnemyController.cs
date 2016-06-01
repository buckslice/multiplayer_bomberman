//using UnityEngine;
//using System.Collections;

//public class EnemyController : MonoBehaviour {

//    public enum PathType {
//        RANDOM, // randomly walks around
//        SEEK    // finds and follows player, randoms if no path

//        // not implemented yet
//        //FOLLOW // follows player if seen recently, otherwise random
//    };

//    public PathType pathType;
//    public float speed = 2.0f;

//    private Rigidbody rb;
//    private Vector3 path = Vector3.zero;
//    private int ex, ey;   // current tile enemy is in
//    private int lastX, lastY;   // last tile enemy was in
//    private float timeSincePathCheck = 0.0f;
//    private float radius;

//    private bool randomPathing = false;
//    private Transform model;

//    // Use this for initialization
//    void Start() {
//        rb = GetComponent<Rigidbody>();
//        Vector3 p = Pathfinder.instance.getRandomGroundPosition();
//        p.y = 0.0f;
//        transform.position = p;

//        model = transform.Find("Model");
//    }

//    // Update is called once per frame
//    void Update() {
//        if (!rb || respawning) {
//            return;
//        }
//        if (model) {
//            Vector3 lp = model.localPosition;
//            lp.y += Mathf.Sin(Time.timeSinceLevelLoad*5.0f)*2.0f * Time.deltaTime;
//            model.localPosition = lp;
//        }

//        float x = transform.position.x;
//        float y = transform.position.z;
//        if (pathType == PathType.RANDOM || randomPathing) {
//            int tx = (int)(x / LevelData.SIZE);
//            int ty = (int)(y / LevelData.SIZE);
//            // if fully inside a new tile
//            if ((ex != tx || ey != ty) && Pathfinder.instance.fullyInTile(x, y, 0.5f)) {    // TODO use actual radius of enemy collider
//                lastX = ex;
//                lastY = ey;
//                ex = tx;
//                ey = ty;

//                if (pathType == PathType.RANDOM || randomPathing) {
//                    path = Pathfinder.instance.randomWalk(x, y, lastX, lastY);
//                }
//            }
//        }
//        // dont make this an else if
//        if (pathType == PathType.SEEK) {
//            timeSincePathCheck += Time.deltaTime;
//            if (timeSincePathCheck > 0.5f) {
//                Vector3 potential = Pathfinder.instance.getPath(x, y);
//                if (potential == Vector3.zero) {
//                    randomPathing = true;
//                } else {
//                    path = potential;
//                    randomPathing = false;
//                }
//                timeSincePathCheck = 0.0f;
//            }
//        }

//        // move in direction of path
//        rb.velocity = path * speed;

//    }

//    bool respawning = false;
//    void OnTriggerEnter(Collider col) {
//        if (!respawning && col.tag == "Explosion") {
//            respawning = true;
//            StartCoroutine(waitThenReload(1.0f));
//        }
//    }

//    IEnumerator waitThenReload(float time) {
//        rb.velocity = Vector3.zero;
//        yield return new WaitForSeconds(time);
//        Vector3 p = Pathfinder.instance.getRandomGroundPosition();
//        p.y = 0.0f;
//        transform.position = p;
//        respawning = false;
//    }

//    void LateUpdate() {
//        if (Input.GetKeyDown(KeyCode.Backspace)) {  // reset
//            Vector3 p = Pathfinder.instance.getRandomGroundPosition();
//            p.y = 0.0f;
//            transform.position = p;
//            rb.velocity = Vector3.zero;
//        }
//    }

//}
