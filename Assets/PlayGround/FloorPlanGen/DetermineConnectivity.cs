using QuikGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// --- �X�e�b�v3: DetermineConnectivity �Ƃ��̕⏕�֐� ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    private List<Door> DetermineConnectivity(List<RoomDefinition> roomsToPlace, AdjacencyGraph<int, Edge<int>> connectivityGraph)
    {
        Debug.Log("Determining connectivity...");
        List<Door> doors = new List<Door>();
        HashSet<Tuple<int, int>> connectionsMade = new HashSet<Tuple<int, int>>(); // (id1, id2) where id1 < id2

        // --- 1. ���̓O���t�Ɋ�Â��K�{�ڑ� ---
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
                    // �K�{�ڑ����אڂ��Ă��Ȃ����ߔz�u�ł��Ȃ�����
                    Debug.LogError($"REQUIRED connection between {roomID1} and {roomID2} FAILED. Rooms are not adjacent.");
                    // �����Ő������s�Ƃ���ׂ����H �v���ɂ��
                    return null; // �K�{�ڑ����������Ȃ��Ȃ玸�s
                }
            }
        }

        // --- 2. �_���A���S���Y���Ɋ�Â��ڑ����[�� ---

        // 2a. �L��(Hallway)�Ɨאڂ���S�Ẵp�u���b�N(Public)������ڑ� [cite: 163]
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

        // 2b. ���ڑ��̃v���C�x�[�g(Private)�������A�אڂ���p�u���b�N�����ɐڑ� (�\�Ȃ�) [cite: 164]
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Private))
        {
            int privateId = kvp1.Key;
            if (IsRoomConnected(privateId, connectionsMade)) continue; // ���ɂǂ����Ɍq�����Ă���

            // �אڂ���p�u���b�N������T��
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
                        break; // 1�ڑ��ł����OK
                    }
                }
            }
            // �����p�u���b�N�ɐڑ��ł��Ȃ�������A�L���ɐڑ��ł��Ȃ������� (�_���ɂ͂Ȃ����A���p�I����)
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

        // 2c. ���ڑ��̃p�u���b�N�������A�אڂ��鑼�̃p�u���b�N�����ɐڑ� (�\�Ȃ�) [cite: 165]
        // (�������A�ʏ�͘L���Ƃ̐ڑ��ŃJ�o�[����邱�Ƃ�����)
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
                    if (!connectionsMade.Contains(pair)) // �܂��ڑ�����Ă��Ȃ����
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
                        // ���ɐڑ�������Ȃ�A���̕������ڑ��ς݂Ƃ݂Ȃ���
                        connectedTo = publicId2;
                        break;
                    }
                }
            }
            if (connectedTo == -1)
            {
                // ����Public�ɂ��q����Ȃ��ꍇ�A�L���ɐڑ������݂�i�ʏ�͂����炪�D�悳���͂��j
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


        // --- 3. ���B�\���̍ŏI�`�F�b�N�ƏC�� [cite: 166] ---
        // (VerifyReachability���ōs���A�K�v�Ȃ炱���ŏC���h�A��ǉ����郍�W�b�N���Ă�)
        if (!VerifyReachability(roomsToPlace, doors))
        {
            Debug.LogWarning("Reachability check failed AFTER initial door placement. Attempting to force connections...");
            // TODO: �����I�ɐڑ���ǉ����郍�W�b�N
            // ��: ���B�s�\�ȕ�������BFS�œ��B�\�ȕ�����T���A���̊Ԃ̍ŒZ�o�H��̗אڃy�A�Ƀh�A��ݒu
            // ���̏C�����W�b�N�͕��G�ɂȂ�\��������
            // ForceReachability(roomsToPlace, doors, connectionsMade);
        }


        Debug.Log($"Connectivity determined. Placed {doors.Count} doors.");
        return doors;
    }

    /// <summary>
    /// �w�肳�ꂽ���������ɉ��炩�̐ڑ��������Ă��邩�m�F
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
    /// �w�肳�ꂽ2�̕����̊ԂɃh�A��z�u���悤�Ǝ��݂�B
    /// ����������doors���X�g�ɒǉ����Atrue��Ԃ��B
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
    /// �w�肳�ꂽ2�̕���ID���O���b�h��ŗאڂ��Ă��邩�ǂ����𔻒肷��
    /// </summary>
    private bool AreRoomsAdjacent(int roomID1, int roomID2)
    {
        Vector2Int? c1, c2;
        // FindAdjacentWallSegment�͗אډӏ���������֐��Ȃ̂ŁA
        // ���ꂪtrue��Ԃ��Ηאڂ��Ă���Ɣ��f�ł���B
        return FindAdjacentWallSegment(roomID1, roomID2, out c1, out c2);
    }


    // --- �⏕�֐� ---

    /// <summary>
    /// �w�肳�ꂽ2�̕���ID���אڂ���ǃZ�O�����g��T���A
    /// �h�A��ݒu����̂ɓK�����אڃZ���y�A(cell1, cell2)��Ԃ��B
    /// �����̌�₪����ꍇ�̓����_���ɑI������B
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
            // �����̌�₩�烉���_���ɑI�� [cite: 172]
            var chosenLocation = potentialDoorLocations[_random.Next(potentialDoorLocations.Count)];
            cell1 = chosenLocation.Item1;
            cell2 = chosenLocation.Item2;
            return true;
        }

        return false; // �אډӏ���������Ȃ�����
    }


    private bool VerifyAdjacencyConstraints(List<RoomDefinition> rooms)
    {
        // TODO: ���� (PlaceInitialSeeds �� DetermineConnectivity �ŗאڂ͍l���ς݂����A�ŏI�m�F�Ƃ���)
        // �e�����ɂ��āAConnectivityConstraints �Ɋ܂܂�镔�������ۂɗאڂ��Ă��邩�`�F�b�N
        bool allConstraintsMet = true;
        foreach (var kvp in _roomDefinitions)
        {
            int roomID1 = kvp.Key;
            RoomDefinition room1 = kvp.Value;
            if (room1.ConnectivityConstraints == null) continue;

            foreach (int requiredNeighborId in room1.ConnectivityConstraints)
            {
                if (!_roomDefinitions.ContainsKey(requiredNeighborId)) continue; // ���肪���݂��Ȃ�

                if (!AreRoomsAdjacent(roomID1, requiredNeighborId))
                {
                    Debug.LogWarning($"Adjacency constraint NOT MET: Room {roomID1} ({room1.Type}) and Room {requiredNeighborId} ({_roomDefinitions[requiredNeighborId].Type}) are not adjacent.");
                    allConstraintsMet = false;
                    // ������ false ��Ԃ��Ď��s���s�ɂ��邩�A�x���ɗ��߂邩�͑I��
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
            // ������1�ȉ��A�܂��̓h�A���Ȃ��ꍇ�͓��B�\ (�܂��̓`�F�b�N�s�v)
            return true;
        }

        // �h�A��񂩂�אڃO���t���\�z
        var reachabilityGraph = new AdjacencyGraph<int, Edge<int>>();
        foreach (int roomId in _roomDefinitions.Keys)
        {
            reachabilityGraph.AddVertex(roomId); // �S�Ă̕����𒸓_�Ƃ��Ēǉ�
        }

        HashSet<Tuple<int, int>> addedEdges = new HashSet<Tuple<int, int>>();
        foreach (var door in doors)
        {
            int id1 = _grid[door.Cell1.x, door.Cell1.y];
            int id2 = _grid[door.Cell2.x, door.Cell2.y];

            if (id1 > 0 && id2 > 0 && id1 != id2)
            {
                var pair = id1 < id2 ? Tuple.Create(id1, id2) : Tuple.Create(id2, id1);
                if (!addedEdges.Contains(pair)) // �����h�A�ɂ��d���G�b�W��h��
                {
                    reachabilityGraph.AddEdge(new Edge<int>(id1, id2));
                    reachabilityGraph.AddEdge(new Edge<int>(id2, id1)); // �����O���t�Ƃ��Ĉ������ߋt�������ǉ�
                    addedEdges.Add(pair);
                }
            }
        }

        // �A���������`�F�b�N (BFS/DFS)
        if (reachabilityGraph.VertexCount == 0) return true; // ���_���Ȃ��Ȃ�OK

        HashSet<int> visited = new HashSet<int>();
        Queue<int> queue = new Queue<int>();

        // �J�n�_��I�� (��: �ŏ��̕���ID�A�܂��͓���̕����^�C�v������΂���)
        int startNode = reachabilityGraph.Vertices.First();
        // �܂��́AEntrance �� Hallway ������΂�������J�n
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

        // �S�Ă̕���ID���K��ς݂��`�F�b�N
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
