using QuikGraph; // QuikGraph の名前空間を使用します。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// --- ステップ3: DetermineConnectivity とその補助関数 ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// 部屋間の接続性（ドアの配置）を決定する関数。
    /// </summary>
    /// <param name="roomsToPlace">配置対象の部屋定義のリスト（現在は直接使用していませんが、将来の拡張性を考慮して残しています）。</param>
    /// <param name="connectivityGraph">部屋間の必須接続を示すグラフ。</param>
    /// <returns>配置されたドアのリスト。必須接続が満たせない場合はnullを返すことがあります。</returns>
    private List<Door> DetermineConnectivity(List<RoomDefinition> roomsToPlace, AdjacencyGraph<int, Edge<int>> connectivityGraph)
    {
        Debug.Log("Determining connectivity..."); // ログ：接続性決定処理の開始。
        List<Door> doors = new List<Door>(); // 配置されるドアを格納するリストを初期化します。
        HashSet<Tuple<int, int>> connectionsMade = new HashSet<Tuple<int, int>>(); // (id1, id2) where id1 < id2 既に接続が作成された部屋のペアを記録するHashSet。重複接続を防ぎます。

        // --- 1. 入力グラフに基づく必須接続 ---
        // 提供された接続グラフが存在し、そのエッジリストも存在する場合に処理を行います。
        if (connectivityGraph != null && connectivityGraph.Edges != null)
        {
            foreach (var edge in connectivityGraph.Edges) // グラフ内の各エッジ（必須接続）について処理します。
            {
                int roomID1 = edge.Source; // エッジの始点となる部屋のID。
                int roomID2 = edge.Target; // エッジの終点となる部屋のID。

                // 無効なエッジ（自己ループや存在しない部屋IDへの接続）はスキップします。
                if (roomID1 == roomID2 || !_roomDefinitions.ContainsKey(roomID1) || !_roomDefinitions.ContainsKey(roomID2))
                {
                    // Debug.LogWarning($"Skipping invalid or self-connecting edge from graph: {roomID1} -> {roomID2}"); // デバッグログ（コメントアウトされています）。
                    continue; // 次のエッジへ。
                }

                // 部屋IDのペアを作成し、IDが小さい方をItem1にすることで一意性を保ちます。
                var pair = roomID1 < roomID2 ? Tuple.Create(roomID1, roomID2) : Tuple.Create(roomID2, roomID1);
                if (connectionsMade.Contains(pair)) continue; // 既にこのペア間の接続が作成されていればスキップします。

                // 2つの部屋間にドアを配置しようと試みます。
                if (PlaceDoorBetweenRooms(roomID1, roomID2, doors))
                {
                    connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                    Debug.Log($"Placed required door between {roomID1} and {roomID2}"); // ログ：必須ドアの配置成功。
                }
                else
                {
                    // 必須接続が隣接していないため配置できなかった場合の処理。
                    Debug.LogError($"REQUIRED connection between {roomID1} and {roomID2} FAILED. Rooms are not adjacent."); // エラーログ：必須接続の失敗。
                    // ここで生成失敗とするべきか、要件によります。
                    return null; // 必須接続が満たせないならnullを返して失敗とします。
                }
            }
        }

        // --- 2. 論文アルゴリズムに基づく接続ルール ---

        // 2a. 廊下(Hallway)と隣接する全てのパブリック(Public)部屋を接続します。
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // タイプがHallwayの部屋を全て取得します。kvp1.KeyはRoomDefinition.IDです。
        {
            int hallwayId = kvp1.Key; // 廊下の部屋ID。
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // タイプがPublicの部屋を全て取得します。kvp2.KeyはRoomDefinition.IDです。
            {
                int publicId = kvp2.Key; // パブリック部屋のID。
                // 部屋IDのペアを作成し、IDが小さい方をItem1にすることで一意性を保ちます。
                var pair = hallwayId < publicId ? Tuple.Create(hallwayId, publicId) : Tuple.Create(publicId, hallwayId);
                if (connectionsMade.Contains(pair)) continue; // 既に接続が作成されていればスキップします。

                // 廊下とパブリック部屋が隣接しているか確認します。
                if (AreRoomsAdjacent(hallwayId, publicId))
                {
                    // ドアを配置しようと試みます。
                    if (PlaceDoorBetweenRooms(hallwayId, publicId, doors))
                    {
                        connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                        Debug.Log($"Placed Hallway-Public door between {hallwayId} and {publicId}"); // ログ：廊下-パブリックドアの配置成功。
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to place optional door between Hallway {hallwayId} and Public {publicId} despite adjacency."); // 警告ログ：隣接しているにも関わらずドア配置失敗。
                    }
                }
            }
        }

        // 2b. 未接続のプライベート(Private)部屋を、隣接するパブリック部屋に接続します (可能な場合)。
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Private)) // タイプがPrivateの部屋を全て取得します。kvp1.KeyはRoomDefinition.IDです。
        {
            int privateId = kvp1.Key; // プライベート部屋のID。
            if (IsRoomConnected(privateId, connectionsMade)) continue; // 既にどこかに繋がっている場合はスキップします。

            // 隣接するパブリック部屋を探します。
            int connectedTo = -1; // 接続先の部屋IDを記録する変数。
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // タイプがPublicの部屋を全て取得します。kvp2.KeyはRoomDefinition.IDです。
            {
                int publicId = kvp2.Key; // パブリック部屋のID。
                if (AreRoomsAdjacent(privateId, publicId)) // プライベート部屋とパブリック部屋が隣接しているか確認します。
                {
                    var pair = privateId < publicId ? Tuple.Create(privateId, publicId) : Tuple.Create(publicId, privateId); // 部屋IDのペアを作成します。
                    if (PlaceDoorBetweenRooms(privateId, publicId, doors)) // ドアを配置しようと試みます。
                    {
                        connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                        connectedTo = publicId; // 接続先のIDを記録します。
                        Debug.Log($"Connected Private {privateId} to Public {publicId}"); // ログ：プライベート-パブリックドアの配置成功。
                        break; // 1つ接続できればOKなのでループを抜けます。
                    }
                }
            }
            // もしパブリックに接続できなかったら、廊下に接続できないか試します (論文にはないが、実用的かもしれません)。
            if (connectedTo == -1) // まだどの部屋にも接続されていない場合。
            {
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // タイプがHallwayの部屋を全て取得します。kvp2.KeyはRoomDefinition.IDです。
                {
                    int hallwayId = kvp2.Key; // 廊下の部屋ID。
                    if (AreRoomsAdjacent(privateId, hallwayId)) // プライベート部屋と廊下が隣接しているか確認します。
                    {
                        var pair = privateId < hallwayId ? Tuple.Create(privateId, hallwayId) : Tuple.Create(hallwayId, privateId); // 部屋IDのペアを作成します。
                        if (PlaceDoorBetweenRooms(privateId, hallwayId, doors)) // ドアを配置しようと試みます。
                        {
                            connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                            connectedTo = hallwayId; // 接続先のIDを記録します。
                            Debug.Log($"Connected Private {privateId} to Hallway {hallwayId} (fallback)"); // ログ：プライベート-廊下ドアの配置成功（フォールバック）。
                            break; // 1つ接続できればOKなのでループを抜けます。
                        }
                    }
                }
            }

            if (connectedTo == -1) // 最終的にどの部屋にも接続できなかった場合。
            {
                Debug.LogWarning($"Private room {privateId} remains unconnected after trying Public/Hallway."); // 警告ログ：プライベート部屋が未接続。
            }
        }

        // 2c. 未接続のパブリック部屋を、隣接する他のパブリック部屋に接続します (可能な場合)。
        // (ただし、通常は廊下との接続でカバーされることが多いです)。
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // タイプがPublicの部屋を全て取得します。kvp1.KeyはRoomDefinition.IDです。
        {
            int publicId1 = kvp1.Key; // 最初のパブリック部屋のID。
            if (IsRoomConnected(publicId1, connectionsMade)) continue; // 既にどこかに繋がっている場合はスキップします。

            int connectedTo = -1; // 接続先の部屋IDを記録する変数。
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public && r.Key != publicId1)) // タイプがPublicで、かつ最初の部屋とは異なる部屋を全て取得します。kvp2.KeyはRoomDefinition.IDです。
            {
                int publicId2 = kvp2.Key; // 二番目のパブリック部屋のID。
                if (AreRoomsAdjacent(publicId1, publicId2)) // 2つのパブリック部屋が隣接しているか確認します。
                {
                    var pair = publicId1 < publicId2 ? Tuple.Create(publicId1, publicId2) : Tuple.Create(publicId2, publicId1); // 部屋IDのペアを作成します。
                    if (!connectionsMade.Contains(pair)) // まだ接続されていなければ。
                    {
                        if (PlaceDoorBetweenRooms(publicId1, publicId2, doors)) // ドアを配置しようと試みます。
                        {
                            connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                            connectedTo = publicId2; // 接続先のIDを記録します。
                            Debug.Log($"Connected Public {publicId1} to Public {publicId2}"); // ログ：パブリック-パブリックドアの配置成功。
                            break; // 1つ接続できればOKなのでループを抜けます。
                        }
                    }
                    else // 既に接続がある場合。
                    {
                        // 既に接続があるなら、この部屋も接続済みとみなせます。
                        connectedTo = publicId2;
                        break;
                    }
                }
            }
            if (connectedTo == -1) // 他のパブリック部屋に繋がらなかった場合。
            {
                // 他のPublicにも繋がらない場合、廊下に接続を試みます（通常はこちらが優先されるはずです）。
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // タイプがHallwayの部屋を全て取得します。kvp2.KeyはRoomDefinition.IDです。
                {
                    int hallwayId = kvp2.Key; // 廊下の部屋ID。
                    if (AreRoomsAdjacent(publicId1, hallwayId)) // パブリック部屋と廊下が隣接しているか確認します。
                    {
                        var pair = publicId1 < hallwayId ? Tuple.Create(publicId1, hallwayId) : Tuple.Create(hallwayId, publicId1); // 部屋IDのペアを作成します。
                        if (!connectionsMade.Contains(pair)) // まだ接続されていなければ。
                        {
                            if (PlaceDoorBetweenRooms(publicId1, hallwayId, doors)) // ドアを配置しようと試みます。
                            {
                                connectionsMade.Add(pair); // 接続成功した場合、作成済みセットに追加します。
                                connectedTo = hallwayId; // 接続先のIDを記録します。
                                Debug.Log($"Connected Public {publicId1} to Hallway {hallwayId} (fallback)"); // ログ：パブリック-廊下ドアの配置成功（フォールバック）。
                                break; // 1つ接続できればOKなのでループを抜けます。
                            }
                        }
                        else // 既に接続がある場合。
                        {
                            connectedTo = hallwayId;
                            break;
                        }
                    }
                }
            }


            if (connectedTo == -1) // 最終的にどの部屋にも接続できなかった場合。
            {
                Debug.LogWarning($"Public room {publicId1} remains unconnected after trying Public/Hallway."); // 警告ログ：パブリック部屋が未接続。
            }
        }


        // --- 3. 到達可能性の最終チェックと修正 ---
        // (VerifyReachability内で行い、必要ならここで修正ドアを追加するロジックを呼びます)。
        if (!VerifyReachability(roomsToPlace, doors)) // 到達可能性を検証します。
        {
            Debug.LogWarning("Reachability check failed AFTER initial door placement. Attempting to force connections..."); // 警告ログ：到達可能性チェック失敗。
            // TODO: 強制的に接続を追加するロジック。
            // 例: 到達不能な部屋からBFSで到達可能な部屋を探し、その間の最短経路上の隣接ペアにドアを設置。
            // この修正ロジックは複雑になる可能性があります。
            // ForceReachability(roomsToPlace, doors, connectionsMade);
        }


        Debug.Log($"Connectivity determined. Placed {doors.Count} doors."); // ログ：接続性決定処理の終了と配置されたドアの数。
        return doors; // 配置されたドアのリストを返します。
    }

    /// <summary>
    /// 指定された部屋が既に何らかの接続を持っているか確認する関数。
    /// </summary>
    /// <param name="roomId">確認対象の部屋のID。</param>
    /// <param name="connectionsMade">作成済みの接続ペアを格納したHashSet。</param>
    /// <returns>部屋が接続を持っていればtrue、そうでなければfalse。</returns>
    private bool IsRoomConnected(int roomId, HashSet<Tuple<int, int>> connectionsMade) // roomIdはRoomDefinition.IDです。
    {
        foreach (var pair in connectionsMade) // 作成済みの接続ペアを走査します。
        {
            if (pair.Item1 == roomId || pair.Item2 == roomId) // ペアのいずれかのIDが対象の部屋IDと一致すれば。
            {
                return true; // 接続を持っていると判断しtrueを返します。
            }
        }
        return false; // どの接続ペアにも含まれていなければfalseを返します。
    }


    /// <summary>
    /// 指定された2つの部屋の間にドアを配置しようと試みる関数。
    /// 成功したらdoorsリストに追加し、trueを返す。
    /// </summary>
    /// <param name="roomID1">一方の部屋のID。</param>
    /// <param name="roomID2">もう一方の部屋のID。</param>
    /// <param name="doors">配置されたドアを格納するリスト。</param>
    /// <returns>ドアの配置に成功した場合はtrue、そうでない場合はfalse。</returns>
    private bool PlaceDoorBetweenRooms(int roomID1, int roomID2, List<Door> doors) // roomID1, roomID2はRoomDefinition.IDです。
    {
        Vector2Int? cell1, cell2; // ドアを配置する2つの隣接セルを格納する変数。
        // 2つの部屋が隣接する壁セグメントを探します。
        if (FindAdjacentWallSegment(roomID1, roomID2, out cell1, out cell2))
        {
            if (cell1.HasValue && cell2.HasValue) // 有効なセルペアが見つかった場合。
            {
                // 新しいドアオブジェクトを作成します。
                Door newDoor = new Door
                {
                    Cell1 = cell1.Value, // ドアが接続する一方のセルの座標。
                    Cell2 = cell2.Value, // ドアが接続するもう一方のセルの座標。
                };
                doors.Add(newDoor); // ドアリストに追加します。
                return true; // ドア配置成功。
            }
        }
        return false; // ドア配置失敗。
    }

    /// <summary>
    /// 指定された2つの部屋IDがグリッド上で隣接しているかどうかを判定する関数。
    /// </summary>
    /// <param name="roomID1">一方の部屋のID。</param>
    /// <param name="roomID2">もう一方の部屋のID。</param>
    /// <returns>部屋が隣接していればtrue、そうでなければfalse。</returns>
    private bool AreRoomsAdjacent(int roomID1, int roomID2) // roomID1, roomID2はRoomDefinition.IDです。
    {
        Vector2Int? c1, c2; // 隣接セルの座標を格納する変数（ここでは使用しませんが、FindAdjacentWallSegmentの出力引数として必要）。
        // FindAdjacentWallSegmentは隣接箇所を見つける関数なので、
        // これがtrueを返せば隣接していると判断できます。
        return FindAdjacentWallSegment(roomID1, roomID2, out c1, out c2);
    }


    // --- 補助関数 ---

    /// <summary>
    /// 指定された2つの部屋IDが隣接する壁セグメントを探し、
    /// ドアを設置するのに適した隣接セルペア(cell1, cell2)を返す。
    /// 複数の候補がある場合はランダムに選択する。
    /// </summary>
    /// <param name="roomID1">一方の部屋のID。</param>
    /// <param name="roomID2">もう一方の部屋のID。</param>
    /// <param name="cell1">ドアを構成する一方のセルの座標（出力）。</param>
    /// <param name="cell2">ドアを構成するもう一方のセルの座標（出力）。</param>
    /// <returns>隣接箇所が見つかればtrue、そうでなければfalse。</returns>
    private bool FindAdjacentWallSegment(int roomID1, int roomID2, out Vector2Int? cell1, out Vector2Int? cell2) // roomID1, roomID2はRoomDefinition.IDです。
    {
        cell1 = null; // 出力パラメータを初期化。
        cell2 = null; // 出力パラメータを初期化。
        List<Tuple<Vector2Int, Vector2Int>> potentialDoorLocations = new List<Tuple<Vector2Int, Vector2Int>>(); // ドア設置候補地点のリスト。

        // グリッド全体を走査します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1) // 現在のセルがroomID1に属する場合。
                {
                    Vector2Int currentCell = new Vector2Int(x, y); // 現在のセルの座標。
                    // 上下左右の隣接セルを定義します。
                    Vector2Int[] neighbors = {
                        new Vector2Int(x + 1, y), new Vector2Int(x - 1, y),
                        new Vector2Int(x, y + 1), new Vector2Int(x, y - 1)
                    };

                    foreach (var neighbor in neighbors) // 各隣接セルについて処理します。
                    {
                        // 隣接セルがグリッド範囲内であるかを確認します。
                        if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                        {
                            if (_grid[neighbor.x, neighbor.y] == roomID2) // 隣接セルがroomID2に属する場合。
                            {
                                // ドア設置候補として、現在のセルと隣接セルのペアを追加します。
                                potentialDoorLocations.Add(Tuple.Create(currentCell, neighbor));
                            }
                        }
                    }
                }
            }
        }

        if (potentialDoorLocations.Count > 0) // ドア設置候補が見つかった場合。
        {
            // 複数の候補からランダムに1つを選択します。
            var chosenLocation = potentialDoorLocations[_random.Next(potentialDoorLocations.Count)];
            cell1 = chosenLocation.Item1; // 選択されたペアの一方のセル。
            cell2 = chosenLocation.Item2; // 選択されたペアのもう一方のセル。
            return true; // 隣接箇所発見。
        }

        return false; // 隣接箇所が見つからなかった場合。
    }


    /// <summary>
    /// 部屋間の隣接制約が満たされているか検証する関数。
    /// </summary>
    /// <param name="rooms">検証対象の部屋定義のリスト（現在は_roomDefinitionsを使用）。</param>
    /// <returns>全ての隣接制約が満たされていればtrue、そうでなければfalse。</returns>
    private bool VerifyAdjacencyConstraints(List<RoomDefinition> rooms) // roomsパラメータは現在直接使用されていません。
    {
        // TODO: 実装 (PlaceInitialSeeds と DetermineConnectivity で隣接は考慮済みだが、最終確認として)
        // 各部屋について、ConnectivityConstraints に含まれる部屋が実際に隣接しているかチェック
        bool allConstraintsMet = true; // 全ての制約が満たされているかを示すフラグ。
        foreach (var kvp in _roomDefinitions) // _roomDefinitions内の全ての部屋について処理します。kvp.KeyはRoomDefinition.IDです。
        {
            int roomID1 = kvp.Key; // 現在の部屋のID。
            RoomDefinition room1 = kvp.Value; // 現在の部屋の定義。
            if (room1.ConnectivityConstraints == null) continue; // 接続制約がなければスキップ。

            foreach (int requiredNeighborId in room1.ConnectivityConstraints) // 各必須隣接部屋IDについて処理します。
            {
                if (!_roomDefinitions.ContainsKey(requiredNeighborId)) continue; // 相手の部屋が存在しない場合はスキップ。

                if (!AreRoomsAdjacent(roomID1, requiredNeighborId)) // 2つの部屋が隣接していない場合。
                {
                    Debug.LogWarning($"Adjacency constraint NOT MET: Room {roomID1} ({room1.Type}) and Room {requiredNeighborId} ({_roomDefinitions[requiredNeighborId].Type}) are not adjacent."); // 警告ログ：隣接制約違反。
                    allConstraintsMet = false; // フラグをfalseに設定。
                    // ここで false を返して試行失敗にするか、警告に留めるかは選択によります。
                }
            }
        }
        if (allConstraintsMet) // 全ての制約が満たされている場合。
        {
            // Debug.Log("All adjacency constraints are met."); // デバッグログ（コメントアウトされています）。
        }
        return allConstraintsMet; // 検証結果を返します。
    }

    /// <summary>
    /// 全ての部屋が互いに到達可能か検証する関数。
    /// </summary>
    /// <param name="rooms">検証対象の部屋定義のリスト（現在は_roomDefinitionsを使用）。</param>
    /// <param name="doors">配置されたドアのリスト。</param>
    /// <returns>全ての部屋が到達可能であればtrue、そうでなければfalse。</returns>
    private bool VerifyReachability(List<RoomDefinition> rooms, List<Door> doors) // roomsパラメータは現在直接使用されていません。
    {
        // 部屋が1つ以下、またはドアが存在しない場合は、到達可能とみなすか、チェック不要とします。
        if (_roomDefinitions == null || _roomDefinitions.Count <= 1 || doors == null)
        {
            return true;
        }

        // ドア情報から到達可能性を判定するための隣接グラフを構築します。
        var reachabilityGraph = new AdjacencyGraph<int, Edge<int>>();
        foreach (int roomId in _roomDefinitions.Keys) // _roomDefinitionsのキー（RoomDefinition.ID）を頂点としてグラフに追加します。
        {
            reachabilityGraph.AddVertex(roomId); // 全ての部屋を頂点として追加します。
        }

        HashSet<Tuple<int, int>> addedEdges = new HashSet<Tuple<int, int>>(); // 追加済みのエッジを記録し、重複を防ぎます。
        foreach (var door in doors) // 配置された各ドアについて処理します。
        {
            // ドアが接続する2つのセルの部屋IDを取得します。
            int id1 = _grid[door.Cell1.x, door.Cell1.y];
            int id2 = _grid[door.Cell2.x, door.Cell2.y];

            // 両方のセルが有効な部屋IDを持ち、かつ異なる部屋IDである場合。
            if (id1 > 0 && id2 > 0 && id1 != id2)
            {
                // IDのペアを作成し、小さい方をItem1にすることで一意性を保ちます。
                var pair = id1 < id2 ? Tuple.Create(id1, id2) : Tuple.Create(id2, id1);
                if (!addedEdges.Contains(pair)) // 同じドアによる重複エッジを防ぎます。
                {
                    reachabilityGraph.AddEdge(new Edge<int>(id1, id2)); // グラフにエッジを追加します。
                    reachabilityGraph.AddEdge(new Edge<int>(id2, id1)); // 無向グラフとして扱うため逆方向も追加します。
                    addedEdges.Add(pair); // 追加済みエッジとして記録します。
                }
            }
        }

        // 連結成分をチェック (BFS/DFS) します。
        if (reachabilityGraph.VertexCount == 0) return true; // グラフに頂点がなければ到達可能とみなします。

        HashSet<int> visited = new HashSet<int>(); // 訪問済みの部屋IDを格納するHashSet。
        Queue<int> queue = new Queue<int>(); // BFSのためのキュー。

        // 開始点を選択します (例: 最初の部屋ID、または特定の部屋タイプがあればそれ)。
        int startNode = reachabilityGraph.Vertices.First(); // グラフ内の最初の頂点を開始点とします。
        // または、Entrance や Hallway があればそこから開始することも考慮できます。
        var startCandidates = _roomDefinitions.Where(kvp => kvp.Value.Type == RoomType.Hallway || kvp.Value.Type == RoomType.Public).Select(kvp => kvp.Key); // kvp.KeyはRoomDefinition.IDです。
        if (startCandidates.Any()) // 候補が存在する場合。
        {
            startNode = startCandidates.First(); // 最初の候補を開始点とします。
        }


        queue.Enqueue(startNode); // 開始点をキューに追加します。
        visited.Add(startNode); // 開始点を訪問済みとして記録します。

        // BFSを実行します。
        while (queue.Count > 0)
        {
            int current = queue.Dequeue(); // キューから現在の部屋IDを取り出します。
            if (reachabilityGraph.TryGetOutEdges(current, out var outEdges)) // 現在の部屋からの出エッジを取得します。
            {
                foreach (var edge in outEdges) // 各出エッジについて処理します。
                {
                    int neighbor = edge.Target; // 隣接する部屋IDを取得します。
                    if (!visited.Contains(neighbor)) // 隣接部屋が未訪問の場合。
                    {
                        visited.Add(neighbor); // 訪問済みとして記録します。
                        queue.Enqueue(neighbor); // キューに追加します。
                    }
                }
            }
        }

        // 全ての部屋IDが訪問済みかチェックします。
        bool allReachable = visited.Count == reachabilityGraph.VertexCount;

        if (!allReachable) // 全ての部屋が到達可能でない場合。
        {
            var unreachable = reachabilityGraph.Vertices.Except(visited).ToList(); // 到達不能な部屋のリストを取得します。
            Debug.LogWarning($"Reachability check failed. Unreachable rooms: {string.Join(", ", unreachable)} (started from {startNode})"); // 警告ログ：到達可能性チェック失敗。
        }
        else // 全ての部屋が到達可能な場合。
        {
            // Debug.Log("Reachability check passed."); // デバッグログ（コメントアウトされています）。
        }

        return allReachable; // 到達可能性の検証結果を返します。
    }
}