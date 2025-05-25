using QuikGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// --- ステップ3: DetermineConnectivity とその補助関数 ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    private List<Door> DetermineConnectivity(List<RoomDefinition> roomsToPlace, AdjacencyGraph<int, Edge<int>> connectivityGraph)
    {
        Debug.Log("Determining connectivity...");
        List<Door> doors = new List<Door>();
        HashSet<Tuple<int, int>> connectionsMade = new HashSet<Tuple<int, int>>(); // (id1, id2) where id1 < id2

        // --- 1. 入力グラフに基づく必須接続 ---
        if (connectivityGraph != null && connectivityGraph.Edges != null)
        {
            foreach (var edge in connectivityGraph.Edges)
            {
                int roomID1 = edge.Source;
                int roomID2 = edge.Target;

                if (roomID1 == roomID2 || !_roomDefinitions.ContainsKey(roomID1) || !_roomDefinitions.ContainsKey(roomID2))
                {
                    // Debug.LogWarning($"Skipping invalid or self-connecting edge from graph: {roomID1} -> {roomID2}");
                    continue;
                }

                var pair = roomID1 < roomID2 ? Tuple.Create(roomID1, roomID2) : Tuple.Create(roomID2, roomID1);
                if (connectionsMade.Contains(pair)) continue;

                if (PlaceDoorBetweenRooms(roomID1, roomID2, doors))
                {
                    connectionsMade.Add(pair);
                    Debug.Log($"Placed required door between {roomID1} and {roomID2}");
                }
                else
                {
                    // 必須接続が隣接していないため配置できなかった
                    Debug.LogError($"REQUIRED connection between {roomID1} and {roomID2} FAILED. Rooms are not adjacent.");
                    // ここで生成失敗とするべきか？ 要件による
                    return null; // 必須接続が満たせないなら失敗
                }
            }
        }

        // --- 2. 論文アルゴリズムに基づく接続ルール ---

        // 2a. 廊下(Hallway)と隣接する全てのパブリック(Public)部屋を接続 [cite: 163]
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
        {
            int hallwayId = kvp1.Key;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
            {
                int publicId = kvp2.Key;
                var pair = hallwayId < publicId ? Tuple.Create(hallwayId, publicId) : Tuple.Create(publicId, hallwayId);
                if (connectionsMade.Contains(pair)) continue;

                if (AreRoomsAdjacent(hallwayId, publicId))
                {
                    if (PlaceDoorBetweenRooms(hallwayId, publicId, doors))
                    {
                        connectionsMade.Add(pair);
                        Debug.Log($"Placed Hallway-Public door between {hallwayId} and {publicId}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to place optional door between Hallway {hallwayId} and Public {publicId} despite adjacency.");
                    }
                }
            }
        }

        // 2b. 未接続のプライベート(Private)部屋を、隣接するパブリック部屋に接続 (可能なら) [cite: 164]
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Private))
        {
            int privateId = kvp1.Key;
            if (IsRoomConnected(privateId, connectionsMade)) continue; // 既にどこかに繋がっている

            // 隣接するパブリック部屋を探す
            int connectedTo = -1;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
            {
                int publicId = kvp2.Key;
                if (AreRoomsAdjacent(privateId, publicId))
                {
                    var pair = privateId < publicId ? Tuple.Create(privateId, publicId) : Tuple.Create(publicId, privateId);
                    if (PlaceDoorBetweenRooms(privateId, publicId, doors))
                    {
                        connectionsMade.Add(pair);
                        connectedTo = publicId;
                        Debug.Log($"Connected Private {privateId} to Public {publicId}");
                        break; // 1つ接続できればOK
                    }
                }
            }
            // もしパブリックに接続できなかったら、廊下に接続できないか試す (論文にはないが、実用的かも)
            if (connectedTo == -1)
            {
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
                {
                    int hallwayId = kvp2.Key;
                    if (AreRoomsAdjacent(privateId, hallwayId))
                    {
                        var pair = privateId < hallwayId ? Tuple.Create(privateId, hallwayId) : Tuple.Create(hallwayId, privateId);
                        if (PlaceDoorBetweenRooms(privateId, hallwayId, doors))
                        {
                            connectionsMade.Add(pair);
                            connectedTo = hallwayId;
                            Debug.Log($"Connected Private {privateId} to Hallway {hallwayId} (fallback)");
                            break;
                        }
                    }
                }
            }

            if (connectedTo == -1)
            {
                Debug.LogWarning($"Private room {privateId} remains unconnected after trying Public/Hallway.");
            }
        }

        // 2c. 未接続のパブリック部屋を、隣接する他のパブリック部屋に接続 (可能なら) [cite: 165]
        // (ただし、通常は廊下との接続でカバーされることが多い)
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
        {
            int publicId1 = kvp1.Key;
            if (IsRoomConnected(publicId1, connectionsMade)) continue;

            int connectedTo = -1;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public && r.Key != publicId1))
            {
                int publicId2 = kvp2.Key;
                if (AreRoomsAdjacent(publicId1, publicId2))
                {
                    var pair = publicId1 < publicId2 ? Tuple.Create(publicId1, publicId2) : Tuple.Create(publicId2, publicId1);
                    if (!connectionsMade.Contains(pair)) // まだ接続されていなければ
                    {
                        if (PlaceDoorBetweenRooms(publicId1, publicId2, doors))
                        {
                            connectionsMade.Add(pair);
                            connectedTo = publicId2;
                            Debug.Log($"Connected Public {publicId1} to Public {publicId2}");
                            break;
                        }
                    }
                    else
                    {
                        // 既に接続があるなら、この部屋も接続済みとみなせる
                        connectedTo = publicId2;
                        break;
                    }
                }
            }
            if (connectedTo == -1)
            {
                // 他のPublicにも繋がらない場合、廊下に接続を試みる（通常はこちらが優先されるはず）
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
                {
                    int hallwayId = kvp2.Key;
                    if (AreRoomsAdjacent(publicId1, hallwayId))
                    {
                        var pair = publicId1 < hallwayId ? Tuple.Create(publicId1, hallwayId) : Tuple.Create(hallwayId, publicId1);
                        if (!connectionsMade.Contains(pair))
                        {
                            if (PlaceDoorBetweenRooms(publicId1, hallwayId, doors))
                            {
                                connectionsMade.Add(pair);
                                connectedTo = hallwayId;
                                Debug.Log($"Connected Public {publicId1} to Hallway {hallwayId} (fallback)");
                                break;
                            }
                        }
                        else
                        {
                            connectedTo = hallwayId;
                            break;
                        }
                    }
                }
            }


            if (connectedTo == -1)
            {
                Debug.LogWarning($"Public room {publicId1} remains unconnected after trying Public/Hallway.");
            }
        }


        // --- 3. 到達可能性の最終チェックと修正 [cite: 166] ---
        // (VerifyReachability内で行い、必要ならここで修正ドアを追加するロジックを呼ぶ)
        if (!VerifyReachability(roomsToPlace, doors))
        {
            Debug.LogWarning("Reachability check failed AFTER initial door placement. Attempting to force connections...");
            // TODO: 強制的に接続を追加するロジック
            // 例: 到達不能な部屋からBFSで到達可能な部屋を探し、その間の最短経路上の隣接ペアにドアを設置
            // この修正ロジックは複雑になる可能性がある
            // ForceReachability(roomsToPlace, doors, connectionsMade);
        }


        Debug.Log($"Connectivity determined. Placed {doors.Count} doors.");
        return doors;
    }

    /// <summary>
    /// 指定された部屋が既に何らかの接続を持っているか確認
    /// </summary>
    private bool IsRoomConnected(int roomId, HashSet<Tuple<int, int>> connectionsMade)
    {
        foreach (var pair in connectionsMade)
        {
            if (pair.Item1 == roomId || pair.Item2 == roomId)
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// 指定された2つの部屋の間にドアを配置しようと試みる。
    /// 成功したらdoorsリストに追加し、trueを返す。
    /// </summary>
    private bool PlaceDoorBetweenRooms(int roomID1, int roomID2, List<Door> doors)
    {
        Vector2Int? cell1, cell2;
        if (FindAdjacentWallSegment(roomID1, roomID2, out cell1, out cell2))
        {
            if (cell1.HasValue && cell2.HasValue)
            {
                Door newDoor = new Door
                {
                    Cell1 = cell1.Value,
                    Cell2 = cell2.Value,
                };
                doors.Add(newDoor);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 指定された2つの部屋IDがグリッド上で隣接しているかどうかを判定する
    /// </summary>
    private bool AreRoomsAdjacent(int roomID1, int roomID2)
    {
        Vector2Int? c1, c2;
        // FindAdjacentWallSegmentは隣接箇所を見つける関数なので、
        // これがtrueを返せば隣接していると判断できる。
        return FindAdjacentWallSegment(roomID1, roomID2, out c1, out c2);
    }


    // --- 補助関数 ---

    /// <summary>
    /// 指定された2つの部屋IDが隣接する壁セグメントを探し、
    /// ドアを設置するのに適した隣接セルペア(cell1, cell2)を返す。
    /// 複数の候補がある場合はランダムに選択する。
    /// </summary>
    private bool FindAdjacentWallSegment(int roomID1, int roomID2, out Vector2Int? cell1, out Vector2Int? cell2)
    {
        cell1 = null;
        cell2 = null;
        List<Tuple<Vector2Int, Vector2Int>> potentialDoorLocations = new List<Tuple<Vector2Int, Vector2Int>>();

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1)
                {
                    Vector2Int currentCell = new Vector2Int(x, y);
                    Vector2Int[] neighbors = {
                        new Vector2Int(x + 1, y), new Vector2Int(x - 1, y),
                        new Vector2Int(x, y + 1), new Vector2Int(x, y - 1)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                        {
                            if (_grid[neighbor.x, neighbor.y] == roomID2)
                            {
                                potentialDoorLocations.Add(Tuple.Create(currentCell, neighbor));
                            }
                        }
                    }
                }
            }
        }

        if (potentialDoorLocations.Count > 0)
        {
            // 複数の候補からランダムに選択 [cite: 172]
            var chosenLocation = potentialDoorLocations[_random.Next(potentialDoorLocations.Count)];
            cell1 = chosenLocation.Item1;
            cell2 = chosenLocation.Item2;
            return true;
        }

        return false; // 隣接箇所が見つからなかった
    }


    private bool VerifyAdjacencyConstraints(List<RoomDefinition> rooms)
    {
        // TODO: 実装 (PlaceInitialSeeds と DetermineConnectivity で隣接は考慮済みだが、最終確認として)
        // 各部屋について、ConnectivityConstraints に含まれる部屋が実際に隣接しているかチェック
        bool allConstraintsMet = true;
        foreach (var kvp in _roomDefinitions)
        {
            int roomID1 = kvp.Key;
            RoomDefinition room1 = kvp.Value;
            if (room1.ConnectivityConstraints == null) continue;

            foreach (int requiredNeighborId in room1.ConnectivityConstraints)
            {
                if (!_roomDefinitions.ContainsKey(requiredNeighborId)) continue; // 相手が存在しない

                if (!AreRoomsAdjacent(roomID1, requiredNeighborId))
                {
                    Debug.LogWarning($"Adjacency constraint NOT MET: Room {roomID1} ({room1.Type}) and Room {requiredNeighborId} ({_roomDefinitions[requiredNeighborId].Type}) are not adjacent.");
                    allConstraintsMet = false;
                    // ここで false を返して試行失敗にするか、警告に留めるかは選択
                }
            }
        }
        if (allConstraintsMet)
        {
            // Debug.Log("All adjacency constraints are met.");
        }
        return allConstraintsMet;
    }

    private bool VerifyReachability(List<RoomDefinition> rooms, List<Door> doors)
    {
        if (_roomDefinitions == null || _roomDefinitions.Count <= 1 || doors == null)
        {
            // 部屋が1つ以下、またはドアがない場合は到達可能 (またはチェック不要)
            return true;
        }

        // ドア情報から隣接グラフを構築
        var reachabilityGraph = new AdjacencyGraph<int, Edge<int>>();
        foreach (int roomId in _roomDefinitions.Keys)
        {
            reachabilityGraph.AddVertex(roomId); // 全ての部屋を頂点として追加
        }

        HashSet<Tuple<int, int>> addedEdges = new HashSet<Tuple<int, int>>();
        foreach (var door in doors)
        {
            int id1 = _grid[door.Cell1.x, door.Cell1.y];
            int id2 = _grid[door.Cell2.x, door.Cell2.y];

            if (id1 > 0 && id2 > 0 && id1 != id2)
            {
                var pair = id1 < id2 ? Tuple.Create(id1, id2) : Tuple.Create(id2, id1);
                if (!addedEdges.Contains(pair)) // 同じドアによる重複エッジを防ぐ
                {
                    reachabilityGraph.AddEdge(new Edge<int>(id1, id2));
                    reachabilityGraph.AddEdge(new Edge<int>(id2, id1)); // 無向グラフとして扱うため逆方向も追加
                    addedEdges.Add(pair);
                }
            }
        }

        // 連結成分をチェック (BFS/DFS)
        if (reachabilityGraph.VertexCount == 0) return true; // 頂点がないならOK

        HashSet<int> visited = new HashSet<int>();
        Queue<int> queue = new Queue<int>();

        // 開始点を選択 (例: 最初の部屋ID、または特定の部屋タイプがあればそれ)
        int startNode = reachabilityGraph.Vertices.First();
        // または、Entrance や Hallway があればそこから開始
        var startCandidates = _roomDefinitions.Where(kvp => kvp.Value.Type == RoomType.Hallway || kvp.Value.Type == RoomType.Public).Select(kvp => kvp.Key);
        if (startCandidates.Any())
        {
            startNode = startCandidates.First();
        }


        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (reachabilityGraph.TryGetOutEdges(current, out var outEdges))
            {
                foreach (var edge in outEdges)
                {
                    int neighbor = edge.Target;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // 全ての部屋IDが訪問済みかチェック
        bool allReachable = visited.Count == reachabilityGraph.VertexCount;

        if (!allReachable)
        {
            var unreachable = reachabilityGraph.Vertices.Except(visited).ToList();
            Debug.LogWarning($"Reachability check failed. Unreachable rooms: {string.Join(", ", unreachable)} (started from {startNode})");
        }
        else
        {
            // Debug.Log("Reachability check passed.");
        }

        return allReachable;
    }

}
