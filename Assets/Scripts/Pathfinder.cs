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

    private Level level;
    private int[,] paths;
    private Queue<Node> frontier;
    private int pX, pY; // which tile the player is in
    private int pathsGenerated = 0;
    private int greatestCost = 0;
    private int lastX;
    private int lastY;
    private bool drawPathData = true;

    void Awake() {
        instance = this;
        level = GetComponent<Level>();
        frontier = new Queue<Node>();
    }

    public void init(int width, int height) {
        paths = new int[width, height];
    }
	
	// Update is called once per frame
	void Update () {
        if (!player) {
            return;
        }

        pX = (int)(player.position.x / Level.SIZE);
        pY = (int)(player.position.z / Level.SIZE);
        // only generate path if player has changed tile position
        if (pX != lastX || pY != lastY) {
            generatePath(pX, pY);
            pathsGenerated++;
            //Debug.Log(tiles[x][y] + " " + x + " " + y + " " + pathsGenerated);
        }
        lastX = pX;
        lastY = pY;
    }

    int[] arrx = { 1, -1, 0, 0 };
    int[] arry = { 0, 0, 1, -1 };

    public Vector3 getPath(float xPos, float yPos) {
        int x = (int)(xPos / Level.SIZE);
        int y = (int)(yPos / Level.SIZE);

        Vector3 dir = Vector3.zero;
        if (!level.isWalkable(x, y) || paths[x, y] < 0 || !player) {
            return dir;
        }
        if (x == pX && y == pY) {
            return Vector3.down;
        }

        int shortest = paths[x, y];
        bool favorX = Random.value > 0.5f; // random chance to prefer x over y axis and vice versa
        for (int i = 0; i < 4; ++i) {
            int tx = x + (favorX ? arrx[i] : arry[i]);
            int ty = y + (favorX ? arry[i] : arrx[i]);
            if (level.isWalkable(tx, ty) && paths[tx, ty] < shortest) {
                shortest = paths[tx, ty];
                dir = level.getRandomPointInTile(tx, ty, 0.5f);
            }
        }

        dir -= new Vector3(xPos, 0f, yPos);
        return dir.normalized;
    }

    private void generatePath(int x, int y) {
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
                paths[n.x + 1, n.y] = paths[n.x, n.y] + 1;
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
