using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --- ステップ2: ExpandRooms とその補助関数 ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    private bool ExpandRooms(List<RoomDefinition> roomsToPlace)
    {
        Debug.Log("Expanding rooms...");

        // --- フェーズ1: 矩形拡張 (Rectangular Growth) [cite: 119, 128] ---
        HashSet<int> availableRoomsPhase1 = new HashSet<int>(_roomDefinitions.Keys);
        int totalPlaceable = _totalPlaceableCells; // 全体の配置可能セル数
        int cellsAssigned = _roomDefinitions.Count; // 初期シード分
        int maxIterationsPhase1 = totalPlaceable * 2; // 無限ループ防止用
        int iterationPhase1 = 0;


        while (availableRoomsPhase1.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase1 < maxIterationsPhase1)
        {
            iterationPhase1++;
            // 面積比率に基づいて次に拡張する部屋を選択
            int roomId = SelectRoomToExpand(availableRoomsPhase1, true);
            if (roomId == -1) break; // 選択できる部屋がない

            // 矩形拡張を試みる
            bool grew = GrowRect(roomId);

            if (!grew)
            {
                availableRoomsPhase1.Remove(roomId); // これ以上矩形拡張できない
            }
            else
            {
                // 拡張に成功した場合、再度拡張の候補に入れることで、連続的な拡張を促す
                // (ただし、無限ループにならないように注意が必要。SelectRoomToExpandの確率的選択である程度緩和される)
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新
                                                                             // Debug.Log($"Phase 1, Iter {iterationPhase1}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");

        }
        if (iterationPhase1 >= maxIterationsPhase1) Debug.LogWarning("Phase 1 reached max iterations.");


        // --- フェーズ2: L字型拡張 (L-Shape Growth) ---
        HashSet<int> availableRoomsPhase2 = new HashSet<int>(_roomDefinitions.Keys);
        int maxIterationsPhase2 = (totalPlaceable - cellsAssigned) * _roomDefinitions.Count; // 隙間埋め的な処理なので多めに見積もる
        int iterationPhase2 = 0;

        while (availableRoomsPhase2.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase2 < maxIterationsPhase2)
        {
            iterationPhase2++;
            // 面積比率はもうあまり考慮せず、拡張可能な部屋を優先？ [cite: 138] では最大サイズ考慮せず
            int roomId = SelectRoomToExpand(availableRoomsPhase2, false); // 比率考慮なしで選択
            if (roomId == -1) break;

            // L字型拡張を試みる [cite: 120, 137, 139]
            bool grew = GrowLShape(roomId);

            if (!grew)
            {
                availableRoomsPhase2.Remove(roomId); // これ以上L字拡張できない
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新
                                                                             // Debug.Log($"Phase 2, Iter {iterationPhase2}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");
        }
        if (iterationPhase2 >= maxIterationsPhase2) Debug.LogWarning("Phase 2 reached max iterations.");


        // --- フェーズ3: 隙間埋め (Fill Gaps) [cite: 121, 140] ---
        FillGaps();
        cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新


        // 最終チェック: 未割り当てセルがないか？
        bool allCellsFilled = AreAllCellsFilled();
        Debug.Log($"Room expansion finished. Assigned cells: {cellsAssigned}/{totalPlaceable}. All filled: {allCellsFilled}");

        // 全てのセルが埋まらなかった場合、失敗とみなすか、許容するかは要件次第
        // if (!allCellsFilled) return false;

        return true;
    }


    /// <summary>
    /// 拡張フェーズで次に拡張する部屋を選択する
    /// </summary>
    /// <param name="availableRoomIds">拡張可能な部屋IDのセット</param>
    /// <param name="useSizeRatio">面積比率を考慮するかどうか</param>
    /// <returns>選択された部屋ID、候補がなければ-1</returns>
    private int SelectRoomToExpand(HashSet<int> availableRoomIds, bool useSizeRatio)
    {
        if (availableRoomIds.Count == 0) return -1;

        // 現在のサイズと目標サイズの差に基づいて選択するアプローチも考えられる
        // float GetPriority(int id) {
        //     RoomDefinition room = _roomDefinitions[id];
        //     float targetRatio = room.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio);
        //     float currentRatio = (float)room.CurrentSize / _totalPlaceableCells;
        //     return Mathf.Max(0, targetRatio - currentRatio); // 目標に達していないほど優先度高
        // }
        // var candidates = availableRoomIds.Select(id => new { id, priority = GetPriority(id) }).Where(x => x.priority > 0).ToList();
        // if (candidates.Count == 0) return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count)); // 優先度0ならランダム
        // float totalPriority = candidates.Sum(x => x.priority);
        // double randomValue = _random.NextDouble() * totalPriority;
        // float cumulative = 0;
        // foreach (var candidate in candidates) {
        //      cumulative += candidate.priority;
        //      if (randomValue < cumulative) return candidate.id;
        // }
        // return candidates.Last().id; // フォールバック


        // 論文 に近い実装 (面積比率に基づく確率的選択)
        if (useSizeRatio)
        {
            var candidates = availableRoomIds.ToList();
            float totalRatio = candidates.Sum(id => _roomDefinitions[id].SizeRatio);

            if (totalRatio <= 0)
            {
                // 比率がない場合はランダムに選択
                return candidates[_random.Next(candidates.Count)];
            }

            double randomValue = _random.NextDouble() * totalRatio;
            float cumulativeRatio = 0;

            foreach (int roomId in candidates)
            {
                cumulativeRatio += _roomDefinitions[roomId].SizeRatio;
                if (randomValue < cumulativeRatio)
                {
                    return roomId;
                }
            }
            return candidates.Last(); // フォールバック
        }
        else
        {
            // 比率を考慮しない場合はランダムに選択
            return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count));
        }
    }


    private bool GrowRect(int roomId)
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合に部屋が矩形を維持できるかチェック
        // 4. 矩形を維持できるラインの中で最も長いものを選択 (複数あればランダム)
        // 5. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 6. 目標サイズ(推定)に達したら拡張を止めるオプション [cite: 133] (フェーズ1のみ)
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー
    }

    private bool GrowLShape(int roomId)
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合にL字型になるか、または既存のL字型を維持するかチェック
        // 4. U字型にならないようにチェック [cite: 139]
        // 5. 拡張可能なラインの中で最も長いものを選択 (複数あればランダム) [cite: 137]
        // 6. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー
    }

    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps...");
        List<Vector2Int> gaps = FindEmptyCells();
        int filledCount = 0;

        // 各未割り当てセルについて処理 [cite: 140]
        foreach (Vector2Int gapPos in gaps)
        {
            if (_grid[gapPos.x, gapPos.y] != -1) continue; // すでに埋まっている場合

            Dictionary<int, int> neighborCounts = new Dictionary<int, int>();
            int maxCount = 0;
            int bestNeighborId = -1;

            Vector2Int[] neighbors = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            // 隣接する部屋IDをカウント
            foreach (var offset in neighbors)
            {
                Vector2Int nPos = gapPos + offset;
                if (nPos.x >= 0 && nPos.x < _gridSize.x && nPos.y >= 0 && nPos.y < _gridSize.y)
                {
                    int neighborId = _grid[nPos.x, nPos.y];
                    if (neighborId > 0) // 有効な部屋ID
                    {
                        if (!neighborCounts.ContainsKey(neighborId)) neighborCounts[neighborId] = 0;
                        neighborCounts[neighborId]++;

                        if (neighborCounts[neighborId] > maxCount)
                        {
                            maxCount = neighborCounts[neighborId];
                            bestNeighborId = neighborId;
                        }
                        // 同数の場合は、より面積の小さい部屋を優先するなどのルールも考えられる
                        else if (neighborCounts[neighborId] == maxCount && bestNeighborId != -1)
                        {
                            // 例: 面積比率が小さい方を優先 (隙間は小さい部屋が吸収しやすい?)
                            if (_roomDefinitions[neighborId].SizeRatio < _roomDefinitions[bestNeighborId].SizeRatio)
                            {
                                bestNeighborId = neighborId;
                            }
                        }
                    }
                }
            }

            // 最も多く隣接している部屋に割り当て
            if (bestNeighborId != -1)
            {
                _grid[gapPos.x, gapPos.y] = bestNeighborId;
                if (_roomDefinitions.ContainsKey(bestNeighborId))
                {
                    _roomDefinitions[bestNeighborId].CurrentSize++;
                }
                filledCount++;
            }
            else
            {
                Debug.LogWarning($"Could not fill gap at {gapPos} - no valid neighbors found.");
            }
        }
        Debug.Log($"Filled {filledCount} gap cells.");
    }


    /// <summary>
    /// グリッド内に未割り当てセル(-1)が残っているか確認
    /// </summary>
    private bool AreAllCellsFilled()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    return false; // 未割り当てセル発見
                }
            }
        }
        return true; // 全て割り当て済み
    }

    /// <summary>
    /// グリッド内の未割り当てセル(-1)のリストを取得
    /// </summary>
    private List<Vector2Int> FindEmptyCells()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }
        return emptyCells;
    }


}
