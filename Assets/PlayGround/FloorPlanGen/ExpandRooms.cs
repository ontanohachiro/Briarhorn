using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
//ステップ2: 部屋を拡張するメインのコルーチン.
public partial class FloorPlanGenerator : MonoBehaviour
{
    // 追加するプライベート変数
    private List<RoomDefinition> _roomsToExpand; // 拡張対象の部屋のリスト
    private float _totalRequestedSizeRatio; // 全部屋の要求サイズ比率の合計

    /// <summary>
    /// 部屋の拡張プロセスを開始する前に、必要な変数を初期化またはリセットします。
    /// </summary>
    private void InitializeRoomExpansion()
    {
        _roomsToExpand = new List<RoomDefinition>();
        _totalRequestedSizeRatio = 0f;

        // 全ての部屋定義を拡張リストに追加し、合計サイズ比率を計算します。
        foreach (var room in _roomDefinitions)
        {
            room.CurrentSize = 0; // 現在のセル数をリセット
            room.Bounds = new RectInt(0, 0, 0, 0); // バウンディングボックスをリセット
            _roomsToExpand.Add(room);
            _totalRequestedSizeRatio += room.SizeRatio;
        }

        // シードが配置されたグリッド上のセルに対応する部屋のCurrentSizeを更新します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] > 0) // 部屋IDが割り当てられている場合
                {
                    //_roomDefinitionsはリストなので、IDとインデックスが一致している前提
                    _roomDefinitions[_grid[x, y]].CurrentSize++;
                }
            }
        }
        Debug.Log("Room expansion initialized.");
    }

    /// <summary>
    /// ステップ2: 部屋を拡張するメインのコルーチン.
    /// 部屋を矩形に拡張し、その後L字型に拡張し、最後に残りのギャップを埋めます。
    /// </summary>
    private bool ExpandRooms()
    {
        Debug.Log("Starting room expansion...");
        InitializeRoomExpansion();

        // フェーズ1: 矩形拡張
        Debug.Log("Phase 1: Rectangular Expansion");
        bool changedInRectPhase;
        int iterationCount = 0;
        do
        {
            changedInRectPhase = false;
            // 部屋のリストをシャッフルして、毎回異なる拡張順序を試みます。
            // これにより、生成されるフロアプランの多様性が増します。
            _roomsToExpand = _roomsToExpand.OrderBy(r => _random.Next()).ToList();

            foreach (var room in _roomsToExpand.ToList()) // ToList() でコピーを作成し、ループ中にリストが変更されても安全にします。
            {
                if (GrowRect(room)) // 部屋を矩形に拡張できるか試みます。
                {
                    changedInRectPhase = true; // 少なくとも1つの部屋が拡張されました。
                }
            }
            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // 無限ループ防止のための安全策
            {
                Debug.LogWarning("Rectangular expansion phase reached max iterations. Breaking early.");
                break;
            }
        } while (changedInRectPhase); // 変更がなくなるまで繰り返します。

        // フェーズ2: L字型拡張
        Debug.Log("Phase 2: L-Shape Expansion");
        bool changedInLShapePhase;
        iterationCount = 0;
        do
        {
            changedInLShapePhase = false;
            _roomsToExpand = _roomsToExpand.OrderBy(r => _random.Next()).ToList(); // 再度シャッフル

            foreach (var room in _roomsToExpand.ToList())
            {
                if (GrowLShape(room)) // 部屋をL字型に拡張できるか試みます。
                {
                    changedInLShapePhase = true;
                }
            }
            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // 無限ループ防止のための安全策
            {
                Debug.LogWarning("L-Shape expansion phase reached max iterations. Breaking early.");
                break;
            }
        } while (changedInLShapePhase);


        // フェーズ3: ギャップ埋め
        Debug.Log("Phase 3: Filling Gaps");
        FillGaps();

        Debug.Log("Room expansion completed.");
        return true;
    }


    /// <summary>
    /// 次に拡張する部屋を選択します。
    /// 論文の「SelectRoom」に相当し、部屋の要求サイズ比率に基づいて選択されます。
    /// </summary>
    /// <param name="availableRooms">拡張可能な部屋のリスト。</param>
    /// <returns>選択された部屋の定義。</returns>
    private RoomDefinition SelectRoomToExpand(List<RoomDefinition> availableRooms)
    {
        if (availableRooms == null || !availableRooms.Any())
        {
            return null;
        }

        // 部屋のサイズ比率に基づいて重み付きランダム選択を行います。
        // これにより、大きい部屋がより高い確率で選ばれますが、小さい部屋も選ばれる可能性があります。
        float totalWeight = availableRooms.Sum(r => r.SizeRatio);
        float randomNumber = (float)_random.NextDouble() * totalWeight;

        foreach (var room in availableRooms)
        {
            if (randomNumber <= room.SizeRatio)
            {
                return room;
            }
            randomNumber -= room.SizeRatio;
        }

        // ここには到達しないはずですが、念のためリストの最初の部屋を返します。
        return availableRooms.First();
    }


    /// <summary>
    /// 部屋を矩形に拡張しようとします。
    /// 論文の「GrowRect」に相当し、最大の矩形領域への拡張を試みます。
    /// </summary>
    /// <param name="room">拡張する部屋の定義。</param>
    /// <returns>部屋が拡張された場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowRect(RoomDefinition room)
    {
        // 部屋のバウンディングボックスを取得（初回呼び出し時はシード位置から計算）
        RectInt currentBounds = room.Bounds;
        if (room.CurrentSize == 1 && !room.InitialSeedPosition.HasValue)
        {
            Debug.LogError($"Room {room.ID} has CurrentSize 1 but no InitialSeedPosition.");
            return false;
        }
        if (room.CurrentSize == 1 && room.InitialSeedPosition.HasValue && room.Bounds.width == 0) // 初回拡張時
        {
            currentBounds = new RectInt(room.InitialSeedPosition.Value.x, room.InitialSeedPosition.Value.y, 1, 1);
            room.Bounds = currentBounds;
        }
        else if (room.CurrentSize == 0) // シードがまだ配置されていない部屋は拡張できません
        {
            return false;
        }

        // 拡張可能な方向と最大の長方形領域を見つけます。
        List<(RectInt newRect, int addedCells)> possibleExpansions = new List<(RectInt, int)>();

        // 上方向への拡張
        for (int h = 1; ; h++) // 新しい高さ
        {
            if (currentBounds.yMax + h > _gridSize.y) break; // グリッドのY方向の境界チェック

            bool canExpandRow = true;
            for (int x = currentBounds.xMin; x < currentBounds.xMax; x++)
            {
                if (_grid[x, currentBounds.yMax + h - 1] != -1) // 未割り当ての配置可能セルであること
                {
                    canExpandRow = false;
                    break;
                }
            }
            if (!canExpandRow) break;

            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y, currentBounds.width, currentBounds.height + h), currentBounds.width * h));
        }

        // 下方向への拡張
        for (int h = 1; ; h++) // 新しい高さ
        {
            if (currentBounds.yMin - h < 0) break; // グリッドのY方向の境界チェック

            bool canExpandRow = true;
            for (int x = currentBounds.xMin; x < currentBounds.xMax; x++)
            {
                if (_grid[x, currentBounds.yMin - h] != -1) // 未割り当ての配置可能セルであること
                {
                    canExpandRow = false;
                    break;
                }
            }
            if (!canExpandRow) break;

            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y - h, currentBounds.width, currentBounds.height + h), currentBounds.width * h));
        }


        // 右方向への拡張
        for (int w = 1; ; w++) // 新しい幅
        {
            if (currentBounds.xMax + w > _gridSize.x) break; // グリッドのX方向の境界チェック

            bool canExpandCol = true;
            for (int y = currentBounds.yMin; y < currentBounds.yMax; y++)
            {
                if (_grid[currentBounds.xMax + w - 1, y] != -1) // 未割り当ての配置可能セルであること
                {
                    canExpandCol = false;
                    break;
                }
            }
            if (!canExpandCol) break;

            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y, currentBounds.width + w, currentBounds.height), currentBounds.height * w));
        }

        // 左方向への拡張
        for (int w = 1; ; w++) // 新しい幅
        {
            if (currentBounds.xMin - w < 0) break; // グリッドのX方向の境界チェック

            bool canExpandCol = true;
            for (int y = currentBounds.yMin; y < currentBounds.yMax; y++)
            {
                if (_grid[currentBounds.xMin - w, y] != -1) // 未割り当ての配置可能セルであること
                {
                    canExpandCol = false;
                    break;
                }
            }
            if (!canExpandCol) break;

            possibleExpansions.Add((new RectInt(currentBounds.x - w, currentBounds.y, currentBounds.width + w, currentBounds.height), currentBounds.height * w));
        }


        // 最も大きく拡張できる機会を選択します。
        // 複数ある場合はランダムに選択することで多様性を確保します。
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCells > 0) // 追加セルが0より大きいもののみ
            .OrderByDescending(e => e.addedCells) // 追加セル数でソート
            .ThenBy(e => _random.Next()) // 同じ追加セル数の場合はランダム
            .ToList();

        if (bestExpansions.Any())
        {
            // 最も追加セルが多い拡張を適用します。
            var selectedExpansion = bestExpansions.First();
            ApplyGrowth(room, selectedExpansion.newRect, selectedExpansion.addedCells);
            Debug.Log($"Room {room.ID} expanded rectangularly to {selectedExpansion.newRect} adding {selectedExpansion.addedCells} cells. Current size: {room.CurrentSize}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 部屋をL字型に拡張しようとします。
    /// 論文の「GrowLShape」に相当し、非矩形の拡張を許可します。
    /// </summary>
    /// <param name="room">拡張する部屋の定義。</param>
    /// <returns>部屋が拡張された場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowLShape(RoomDefinition room)
    {
        // 現在の部屋の境界に基づいて、拡張可能なL字型領域を見つけます。
        // これはGrowRectよりも複雑で、既存の部屋の隣接セルから新しい矩形領域を探すことになります。
        // 実際の実装では、部屋の境界を構成するすべてのセルを調べ、それぞれの隣接する空のセルから矩形拡張を試みることで実現できます。

        // 簡略化のため、ここでは部屋の既存の各セルに隣接する未割り当てのセルを探し、
        // そこから可能な限り最大の1xNまたはNx1の直線的な拡張を試みます。

        List<(List<Vector2Int> newCells, int addedCount)> possibleExpansions = new List<(List<Vector2Int>, int)>();

        // 部屋が占めるすべてのセルを探索し、その隣接セルをチェックします。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == room.ID) // 部屋のセルである場合
                {
                    // このセルから四方に隣接する未割り当てセルを探します。
                    Vector2Int currentCell = new Vector2Int(x, y);

                    // 各方向 (上下左右) に直線的に拡張を試みます。
                    foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left })
                    {
                        List<Vector2Int> newSegment = new List<Vector2Int>();
                        Vector2Int testPos = currentCell + offset;
                        int count = 0;

                        while (testPos.x >= 0 && testPos.x < _gridSize.x &&
                               testPos.y >= 0 && testPos.y < _gridSize.y &&
                               _grid[testPos.x, testPos.y] == -1) // 未割り当ての配置可能セル
                        {
                            newSegment.Add(testPos);
                            count++;
                            testPos += offset;
                        }

                        if (count > 0)
                        {
                            possibleExpansions.Add((newSegment, count));
                        }
                    }
                }
            }
        }

        // 最も大きく拡張できる機会を選択します。
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCount > 0)
            .OrderByDescending(e => e.addedCount)
            .ThenBy(e => _random.Next())
            .ToList();

        if (bestExpansions.Any())
        {
            // 最も追加セルが多い拡張を適用します。
            var selectedExpansion = bestExpansions.First();
            foreach (var cell in selectedExpansion.newCells)
            {
                _grid[cell.x, cell.y] = room.ID;
                room.CurrentSize++;
            }
            // 部屋のBoundsを更新する必要があります。
            UpdateRoomBounds(room);
            Debug.Log($"Room {room.ID} expanded L-shapely, adding {selectedExpansion.addedCount} cells. Current size: {room.CurrentSize}");
            return true;
        }

        return false;
    }


    /// <summary>
    /// 部屋の境界（Bounds）を更新する補助関数。
    /// </summary>
    /// <param name="room">更新する部屋の定義。</param>
    private void UpdateRoomBounds(RoomDefinition room)
    {
        int minX = _gridSize.x, minY = _gridSize.y, maxX = -1, maxY = -1;
        bool foundAnyCell = false;

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == room.ID)
                {
                    if (!foundAnyCell)
                    {
                        minX = x;
                        maxX = x;
                        minY = y;
                        maxY = y;
                        foundAnyCell = true;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }
        }

        if (foundAnyCell)
        {
            room.Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        else
        {
            room.Bounds = new RectInt(0, 0, 0, 0); // セルが見つからない場合は無効な境界を設定
        }
    }


    /// <summary>
    /// 実際にグリッド内の部屋を拡張します。
    /// </summary>
    /// <param name="room">拡張する部屋の定義。</param>
    /// <param name="newRect">新しい矩形領域。</param>
    /// <param name="addedCells">追加されるセルの数。</param>
    private void ApplyGrowth(RoomDefinition room, RectInt newRect, int addedCells)
    {
        // 新しい領域のセルを部屋のIDで埋めます。
        for (int x = newRect.xMin; x < newRect.xMax; x++)
        {
            for (int y = newRect.yMin; y < newRect.yMax; y++)
            {
                // 既存の部屋のセルではない、かつ未割り当てのセルのみを更新します。
                if (_grid[x, y] == -1)
                {
                    _grid[x, y] = room.ID;
                }
            }
        }
        room.CurrentSize += addedCells; // 部屋のサイズを更新
        room.Bounds = newRect; // 部屋のバウンディングボックスを更新
    }

    /// <summary>
    /// 部屋の拡張後に残った未割り当てのセルを埋めます。
    /// 論文の「FillGaps」に相当します。
    /// </summary>
    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps in the grid.");
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // 未割り当てのセルである場合
                {
                    // 隣接する部屋を見つけ、最も多くの隣接セルを持つ部屋に割り当てます。
                    int bestRoomId = -1;
                    int maxAdjacentCount = 0;

                    // 8方向の隣接セルをチェックします。
                    foreach (var offset in new[] {
                        Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left,
                        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                    })
                    {
                        Vector2Int neighborPos = new Vector2Int(x, y) + offset;

                        if (neighborPos.x >= 0 && neighborPos.x < _gridSize.x &&
                            neighborPos.y >= 0 && neighborPos.y < _gridSize.y)
                        {
                            int neighborRoomId = _grid[neighborPos.x, neighborPos.y];
                            if (neighborRoomId > 0) // 有効な部屋IDの場合
                            {
                                // この部屋IDの隣接数をカウントします。
                                // この簡略化された実装では、単に最初に遭遇した部屋に割り当てますが、
                                // より堅牢な実装では、各隣接部屋の隣接セルの合計を計算し、最大のものを選びます。
                                // ここでは、最も近くにある有効な部屋IDに割り当てます。
                                if (maxAdjacentCount == 0) // 最初の有効な隣接部屋
                                {
                                    bestRoomId = neighborRoomId;
                                    maxAdjacentCount = 1; // 少なくとも1つの隣接セルが見つかった
                                }
                                // ここで、さらに隣接セルが多い部屋を優先するロジックを追加できます。
                                // 例: float currentRoomAdjacentCount = GetAdjacentCellsCount(neighborRoomId, new Vector2Int(x, y));
                                // if (currentRoomAdjacentCount > maxAdjacentCount) { ... }
                            }
                        }
                    }

                    if (bestRoomId > 0)
                    {
                        _grid[x, y] = bestRoomId;
                        _roomDefinitions[bestRoomId].CurrentSize++; // 部屋のサイズを更新
                        // _roomDefinitions[bestRoomId].Bounds を更新することも検討してください。
                        // ただし、FillGapsの後で一括してCalculateRoomBoundsを呼ぶ方が効率的かもしれません。
                    }
                    else
                    {
                        // どの部屋にも隣接しない孤立した未割り当てセル
                        Debug.LogWarning($"Isolated unassigned cell at ({x},{y}). Setting to 0 (unusable).");
                        _grid[x, y] = 0; // どの部屋にも割り当てられない場合は、使用不可としてマークします。
                    }
                }
            }
        }
        Debug.Log("Gap filling completed.");
    }


    /// <summary>
    /// 初期シード配置後に隣接制約が満たされていることを確認します。
    /// 論文では、制約が満たされない場合に生成プロセスをリセットする可能性が言及されています。
    /// </summary>
    /// <returns>全ての隣接制約が満たされている場合はtrue、そうでない場合はfalse。</returns>
    private bool VerifyAdjacencyConstraints()
    {
        Debug.Log("Verifying adjacency constraints...");
        bool allConstraintsMet = true;

        foreach (var edge in settings.ConnectivityGraph.Edges) // 接続グラフの各辺（隣接制約）をチェック
        {
            RoomDefinition roomA = _roomDefinitions[edge.Source]; // 辺の始点に対応する部屋
            RoomDefinition roomB = _roomDefinitions[edge.Target]; // 辺の終点に対応する部屋

            if (!roomA.InitialSeedPosition.HasValue || !roomB.InitialSeedPosition.HasValue)
            {
                // シードが配置されていない部屋については、ここではチェックできません。
                // これはPlaceInitialSeeds()の責任です。
                continue;
            }

            // 2つの部屋が隣接しているかを確認します。
            // ここでは簡易的に、両部屋のバウンディングボックスが重なるか、または非常に近いかをチェックします。
            // より厳密には、両部屋のセルが直接隣接しているかを確認する必要があります。
            bool areAdjacent = AreRoomsDirectlyAdjacent(roomA.ID, roomB.ID);

            if (!areAdjacent)
            {
                Debug.LogWarning($"Adjacency constraint between Room {roomA.ID} ({roomA.Type}) and Room {roomB.ID} ({roomB.Type}) NOT met after initial placement. Their cells are not adjacent.");
                allConstraintsMet = false;
                // ここで、満たされなかった制約に基づいて、部屋の配置を調整するなどのロジックを追加できます。
                // または、単純にこの試行を失敗としてマークします。
            }
            else
            {
                Debug.Log($"Adjacency constraint between Room {roomA.ID} ({roomA.Type}) and Room {roomB.ID} ({roomB.Type}) MET.");
            }
        }
        return allConstraintsMet;
    }

    /// <summary>
    /// 指定された2つの部屋がグリッド上で直接隣接しているかを判断する補助関数。
    /// </summary>
    /// <param name="roomID1">部屋1のID。</param>
    /// <param name="roomID2">部屋2のID。</param>
    /// <returns>部屋が直接隣接している場合はtrue、そうでない場合はfalse。</returns>
    private bool AreRoomsDirectlyAdjacent(int roomID1, int roomID2)
    {
        // 部屋1の全てのセルを走査
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1)
                {
                    // このセルの隣接セルをチェック
                    foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left })
                    {
                        Vector2Int neighborPos = new Vector2Int(x, y) + offset;

                        if (neighborPos.x >= 0 && neighborPos.x < _gridSize.x &&
                            neighborPos.y >= 0 && neighborPos.y < _gridSize.y)
                        {
                            if (_grid[neighborPos.x, neighborPos.y] == roomID2)
                            {
                                return true; // 隣接するセルが見つかった
                            }
                        }
                    }
                }
            }
        }
        return false;
    }


    /// <summary>
    /// グリッド上の指定されたセルに隣接する部屋のIDのリストを返します。
    /// </summary>
    /// <param name="x">セルのX座標。</param>
    /// <param name="y">セルのY座標。</param>
    /// <returns>隣接する部屋のIDのHashSet（重複なし）。</returns>
    private HashSet<int> GetAdjacentRooms(int x, int y)
    {
        HashSet<int> adjacentRoomIds = new HashSet<int>();

        // 8方向の隣接セルをチェック
        foreach (var offset in new[] {
            Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        })
        {
            Vector2Int neighborPos = new Vector2Int(x, y) + offset;

            if (neighborPos.x >= 0 && neighborPos.x < _gridSize.x &&
                neighborPos.y >= 0 && neighborPos.y < _gridSize.y)
            {
                int neighborRoomId = _grid[neighborPos.x, neighborPos.y];
                if (neighborRoomId > 0) // 有効な部屋IDの場合
                {
                    adjacentRoomIds.Add(neighborRoomId);
                }
            }
        }
        return adjacentRoomIds;
    }

}