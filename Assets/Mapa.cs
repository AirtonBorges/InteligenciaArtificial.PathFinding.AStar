using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mapa : MonoBehaviour
{
    public SpriteRenderer MySpriteRederer;
    public Personagem MyPersonagem;
    public bool Calculando = false;

    private Texture2D _texture;
    private Sprite _spriteCopy;
    private Color[] _originalPixels;
    private Map _map;

    private Rect _textureRect;
    private Rect _worldRect;

    void Start()
    {
        _spriteCopy = Instantiate(MySpriteRederer.sprite);
        MySpriteRederer.sprite = _spriteCopy;
        _texture = MySpriteRederer.sprite.texture;
        _originalPixels = _texture.GetPixels();

        _textureRect = MySpriteRederer.sprite.rect;
        _worldRect = ((RectTransform)transform).rect;

        _map = Map.Create(_texture);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {

            _texture.SetPixels(_originalPixels);
            _texture.Apply();

            var mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            var destination = WorldPositionToTexturePosition(mousePosition);
            var origin = WorldPositionToTexturePosition(MyPersonagem.transform.position);

            if (!Calculando)
                StartCoroutine(CalcularCaminho(origin, destination));            
        }
    }

    void OnDestroy()
    {
        if (_texture != null)
        {
            _texture.SetPixels(_originalPixels);
            _texture.Apply();
        }
    }

    private Vector3 WorldPositionToTexturePosition(Vector3 worldPosition)
    {
        var oldRangeX = _worldRect.xMax - _worldRect.xMin;
        var newRangeX = _textureRect.xMax - _textureRect.xMin;
        var newX = (int)((int)((worldPosition.x - _worldRect.xMin) * newRangeX) / oldRangeX + _textureRect.xMin);

        var oldRangeY = _worldRect.yMin - _worldRect.yMax;
        var newRangeY = _textureRect.yMin - _textureRect.yMax;
        var newY = (int)((int)((worldPosition.y - _worldRect.yMin) * newRangeY) / oldRangeY + _textureRect.yMin);

        return new Vector3(newX, newY, 0);
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
            _texture.Apply();

            var oldRangeX = _textureRect.xMax - _textureRect.xMin;
            var newRangeX = _worldRect.xMax - _worldRect.xMin;
            var newX = ((int)node.Position.x - _textureRect.xMin) * newRangeX / oldRangeX + _worldRect.xMin;

            var oldRangeY = _textureRect.yMax - _textureRect.yMin;
            var newRangeY = _worldRect.yMax - _worldRect.yMin;
            var newY = ((int)node.Position.y - _textureRect.yMin) * newRangeY / oldRangeY + _worldRect.yMin;
            
            var oldZ = MyPersonagem.transform.position.z;  
            MyPersonagem.transform.position = new Vector3(newX, newY, oldZ);
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

                    AddNeighbors(texture, x, y, node);
                    map._nodes[nodeId] = node;
                }
            }

            return map;
        }

        private static void AddNeighbors(Texture2D texture, int x, int y, Node node)
        {
            var offsets = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

            foreach (var (dx, dy) in offsets)
            {
                var nx = x + dx;
                var ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= texture.width || ny >= texture.height)
                    continue;

                if (texture.GetPixel(nx, ny).g == 0)
                    continue;

                var neighborId = nx * texture.height + ny;
                node.Neighbors[neighborId] = 1f;
            }
        }
    }
}
