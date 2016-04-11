using UnityEngine;
using System.Collections.Generic;

public class Pathfinder : MonoBehaviour {
    private struct Node {
        public int x;
        public int y;

        public Node(int x, int y) {
            this.x = x;
            this.y = y;
        }
    }

    public static Pathfinder instance { get; private set; }

    public Transform player;    // or whatever you want things to path to
    public bool drawPathData = false;

    private Level level;
    private int[,] paths;
    private Queue<Node> frontier;
    private int greatestCost = 0;
    int px, py; // tile player is in
    private float timeSincePathUpdate;

    void Awake() {
        instance = this;
        level = GetComponent<Level>();
        frontier = new Queue<Node>();
    }

    public void init(int width, int height) {
        paths = new int[width, height];
    }

    // Update is called once per frame
    void Update() {
        if (!player) {
            return;
        }

        // calculate tile player is in
        px = (int)(player.position.x / Level.SIZE);
        py = (int)(player.position.z / Level.SIZE);

        // calculate path every once and a while
        timeSincePathUpdate += Time.deltaTime;
        if (timeSincePathUpdate > 0.5f) {
            generatePaths(px, py);
            timeSincePathUpdate = 0.0f;
        }
    }

    int[] arrx = { 1, -1, 0, 0 };
    int[] arry = { 0, 0, 1, -1 };
    public Vector3 getPath(float xPos, float yPos, float radius = 0.5f) {
        int x = (int)(xPos / Level.SIZE);
        int y = (int)(yPos / Level.SIZE);

        Vector3 dir = Vector3.zero;
        if (!level.isWalkable(x, y) || paths[x, y] < 0 || !player) {
            return dir;     // if cant find a path to player
        }
        Vector3 pos = new Vector3(xPos, 0, yPos);
        if (x == px && y == py) {   // if in same tile as player then move towards them
            return (player.position - pos).normalized;
        }

        int shortest = paths[x, y];
        bool favorX = Random.value > 0.5f; // random chance to prefer x over y axis and vice versa
        for (int i = 0; i < 4; ++i) {
            int tx = x + (favorX ? arrx[i] : arry[i]);
            int ty = y + (favorX ? arry[i] : arrx[i]);
            if (level.isWalkable(tx, ty) && paths[tx, ty] < shortest) {
                shortest = paths[tx, ty];
                dir = level.getRandomPointInTile(tx, ty, radius);
            }
        }

        dir -= pos;
        return dir.normalized;
    }

    public Vector3 randomWalk(float xPos, float yPos, int lastX, int lastY ) {
        Vector3 dir = Vector3.zero;

        int x = (int)(xPos / Level.SIZE);
        int y = (int)(yPos / Level.SIZE);

        if(x == lastX && y == lastY) {
            Debug.Log("wat");
            return dir;
        }
        Vector3 pos = new Vector3(xPos, 0, yPos);
        Vector3 lastRes = Vector3.zero;
        bool favorX = Random.value > 0.5f; // random chance to prefer x over y axis and vice versa
        for (int i = 0; i < 4; ++i) {
            int tx = x + (favorX ? arrx[i] : arry[i]);
            int ty = y + (favorX ? arry[i] : arrx[i]);
            if (level.isWalkable(tx, ty)) {
                if (tx == lastX && ty == lastY) {
                    lastRes = level.getRandomPointInTile(tx, ty, 0.5f);
                } else {
                    dir = level.getRandomPointInTile(tx, ty, 0.5f);
                }
            }
        }

        if(dir == Vector3.zero && lastRes != Vector3.zero) {
            dir = lastRes;
        }
        dir -= pos;
        return dir.normalized;
    }

    // checks whether the object is fully inside one tile
    public bool fullyInTile(float xPos, float yPos, float radius) {
        int x = (int)(xPos / Level.SIZE);
        int y = (int)(yPos / Level.SIZE);
        float minx = x * Level.SIZE + radius;
        float miny = y * Level.SIZE + radius;
        float maxx = (x + 1) * Level.SIZE - radius;
        float maxy = (y + 1) * Level.SIZE - radius;

        return xPos >= minx && xPos <= maxx && yPos >= miny && yPos <= maxy;
    }

    private void generatePaths(int x, int y) {
        // clear path
        for (int i = 0; i < paths.GetLength(0); i++) {
            for (int j = 0; j < paths.GetLength(1); j++) {
                paths[i, j] = -1;
            }
        }

        frontier.Clear();
        frontier.Enqueue(new Node(x, y));
        paths[x, y] = 0;
        while (frontier.Count > 0) {
            Node n = frontier.Dequeue();
            greatestCost = Mathf.Max(greatestCost, paths[n.x, n.y]);
            // right neigbor
            if (level.isWalkable(n.x + 1, n.y) && paths[n.x + 1, n.y] < 0) {
                frontier.Enqueue(new Node(n.x + 1, n.y));
                paths[n.x +1, n.y] = paths[n.x, n.y] + 1;
            }
            // left neighbor
            if (level.isWalkable(n.x - 1, n.y) && paths[n.x - 1, n.y] < 0) {
                frontier.Enqueue(new Node(n.x - 1, n.y));
                paths[n.x - 1, n.y] = paths[n.x, n.y] + 1;
            }
            // front neighbor
            if (level.isWalkable(n.x, n.y + 1) && paths[n.x, n.y + 1] < 0) {
                frontier.Enqueue(new Node(n.x, n.y + 1));
                paths[n.x, n.y + 1] = paths[n.x, n.y] + 1;
            }
            // back neighbor
            if (level.isWalkable(n.x, n.y - 1) && paths[n.x, n.y - 1] < 0) {
                frontier.Enqueue(new Node(n.x, n.y - 1));
                paths[n.x, n.y - 1] = paths[n.x, n.y] + 1;
            }
        }
    }

    public Vector3 getRandomGroundPosition() {
        return level.getRandomGroundPosition();
    }


    // to visualize path distance
    void OnDrawGizmos() {
        if (paths == null || !drawPathData) {
            return;
        }
        for (int x = 0; x < paths.GetLength(0); x++) {
            for (int y = 0; y < paths.GetLength(1); y++) {
                float c = paths[x, y];
                if (c >= 0) {
                    Gizmos.color = new Color(1f - c / greatestCost, 0f, c / greatestCost);
                    if (c == 0) {
                        Gizmos.color = Color.yellow;
                    }
                    float maxH = 5f;
                    float height = maxH - c / greatestCost * maxH;
                    Gizmos.DrawCube(new Vector3((x + .5f) * Level.SIZE, height / 2f, (y + .5f) * Level.SIZE), new Vector3(.5f, height, .5f));
                }
            }
        }
    }
}
