using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mapa : MonoBehaviour
{
    public SpriteRenderer MySpriteRederer;
    public Personagem MyPersonagem;

    private Texture2D _texture;
    private Color[] _originalPixels;

    private Map _map;
    public bool Calculando = false;

    void Start()
    {
        _texture = MySpriteRederer.sprite.texture;
        _originalPixels = _texture.GetPixels();
        _map = Map.Create(_texture);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        _texture.SetPixels(_originalPixels);
        var worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var localPos = MySpriteRederer.transform.InverseTransformPoint(worldPos);

        var sprite = MySpriteRederer.sprite;
        var pixelsPerUnit = sprite.pixelsPerUnit;

        var xTextureRectangle = MySpriteRederer.sprite.rect;

        var xWorldPositionRectangle = ((RectTransform)MySpriteRederer.transform).rect;

        var oldRangeX = (xWorldPositionRectangle.xMax - xWorldPositionRectangle.xMin);
        var newRangeX = (xTextureRectangle.width);
        var newX = (int)((worldPos.x - xWorldPositionRectangle.xMin) * newRangeX) / oldRangeX + xTextureRectangle.xMin;
        
        var oldRangeY = (xWorldPositionRectangle.yMin - xWorldPositionRectangle.yMax);
        var newRangeY = (xTextureRectangle.height);
        var newY = (int)((worldPos.y - xWorldPositionRectangle.yMax) * newRangeY) / oldRangeY + xTextureRectangle.yMin;

        var xNewPosition = new Vector3(newX, newY, 0);
        var xDestination = new Vector3(999, 999, 0);
        StartCoroutine(CalcularCaminho(xNewPosition, xDestination));
    }

    IEnumerator CalcularCaminho(Vector3 origem, Vector3 destino)
    {
        Calculando = true;

        var startId = (int)origem.x * _texture.height + (int)origem.y;
        var goalId = (int)destino.x * _texture.height + (int)destino.y;

        Debug.Log($"Start: {startId} - Goal: {goalId}");
        var start = _map.GetNode(startId);
        var goal = _map.GetNode(goalId);
        

        if (start == null || goal == null)
        {
            Debug.Log("No path found");
            Calculando = false;
            yield break;
        }

        var path = AStar(start, goal, _map);
        if (path == null)
        {
            Debug.Log("No path found");
            Calculando = false;
            yield break;
        }

        foreach (var node in path)
        {
            _texture.SetPixel((int)node.Position.x, (int)node.Position.y, Color.green);
            var xWordPositionX = node.Position.x / MySpriteRederer.sprite.pixelsPerUnit;
            var xWordPositionY = node.Position.y / MySpriteRederer.sprite.pixelsPerUnit;
            var xWordPosition = new Vector3(xWordPositionX, xWordPositionY, -2);
            MyPersonagem.transform.position = xWordPosition;
            yield return null;
        }

        Calculando = false;
    }

    List<Node> AStar(Node start, Node goal, Map map)
    {
        var openSet = new SortedSet<(double fScore, int nodeId)>();
        var gScore = new Dictionary<int, double> { [start.NodeId] = 0 };
        var cameFrom = new Dictionary<int, int>();
        var fScoreDict = new Dictionary<int, double>();

        openSet.Add((Heuristic(start, goal), start.NodeId));
        fScoreDict[start.NodeId] = Heuristic(start, goal);

        var closedSet = new HashSet<int>();

        while (openSet.Count > 0)
        {
            var currentId = openSet.Min.nodeId;
            openSet.Remove(openSet.Min);

            if (currentId == goal.NodeId)
                return ReconstructPath(map, cameFrom, goal.NodeId);

            closedSet.Add(currentId);
            var current = map.GetNode(currentId);

            foreach (var (neighborId, cost) in current.Neighbors)
            {
                if (closedSet.Contains(neighborId)) continue;

                var tentativeG = gScore[currentId] + cost;

                if (!gScore.ContainsKey(neighborId) || tentativeG < gScore[neighborId])
                {
                    cameFrom[neighborId] = currentId;
                    gScore[neighborId] = tentativeG;
                    var fScore = tentativeG + Heuristic(map.GetNode(neighborId), goal);

                    fScoreDict[neighborId] = fScore;
                    openSet.Add((fScore, neighborId));
                }
            }
        }

        return null;
    }

    List<Node> ReconstructPath(Map map, Dictionary<int, int> cameFrom, int currentId)
    {
        var path = new List<Node>();
        while (cameFrom.ContainsKey(currentId))
        {
            path.Add(map.GetNode(currentId));
            currentId = cameFrom[currentId];
        }
        path.Add(map.GetNode(currentId));
        path.Reverse();
        return path;
    }

    double Heuristic(Node a, Node b)
    {
        return Math.Abs(a.Position.x - b.Position.x) + Math.Abs(a.Position.y - b.Position.y);
    }

    class Node
    {
        public int NodeId;
        public Vector3 Position;
        public Dictionary<int, float> Neighbors = new();
    }

    class Map
    {
        private Dictionary<int, Node> _nodes = new();

        public Node GetNode(int id) => _nodes.TryGetValue(id, out var node) ? node : null;

        public static Map Create(Texture2D texture)
        {
            var map = new Map();
            var width = texture.width;
            var height = texture.height;

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    if (texture.GetPixel(x, y).g == 0) continue;

                    var nodeId = x * height + y;
                    var node = new Node
                    {
                        NodeId = nodeId,
                        Position = new Vector3(x, y, 0)
                    };

                    AddNeighbors(texture, width, height, x, y, node);
                    map._nodes[nodeId] = node;
                }
            }

            return map;
        }

        private static void AddNeighbors(Texture2D texture, int width, int height, int x, int y, Node node)
        {
            var offsets = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

            foreach (var (dx, dy) in offsets)
            {
                var nx = x + dx;
                var ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                if (texture.GetPixel(nx, ny).g == 0)
                    continue;

                var neighborId = nx * height + ny;
                node.Neighbors[neighborId] = 1f;
            }
        }
    }
}
