using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --- ステップ2: ExpandRooms とその補助関数 ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// 部屋を拡張するメインの関数。複数のフェーズで構成されます。
    /// </summary>
    /// <param name="roomsToPlace">配置および拡張対象の部屋定義のリスト。</param>
    /// <returns>拡張が成功した場合はtrue、そうでない場合はfalse。</returns>
    private bool ExpandRooms(List<RoomDefinition> roomsToPlace) // roomsToPlaceは現在直接使用されていませんが、将来的な拡張のために残しています。
    {
        Debug.Log("Expanding rooms..."); // ログ：部屋拡張処理の開始。

        // --- フェーズ1: 矩形拡張 (Rectangular Growth) ---
        // 拡張可能な部屋のIDを格納するHashSetを作成し、_roomDefinitionsからIDを取得して初期化します。
        HashSet<int> availableRoomsPhase1 = new HashSet<int>(_roomDefinitions.Values.Select(r => r.ID));
        int totalPlaceable = _totalPlaceableCells; // 全体の配置可能セル数を取得します。
        int cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 初期シードによって既に割り当てられているセル数を計算します。
        int maxIterationsPhase1 = totalPlaceable * 2; // 無限ループを防止するための最大イテレーション回数を設定します。
        int iterationPhase1 = 0; // 現在のイテレーション回数を初期化します。


        // 拡張可能な部屋が存在し、かつ割り当てられたセル数が全配置可能セル数未満で、
        // さらに最大イテレーション回数に達していない間、ループを続けます。
        while (availableRoomsPhase1.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase1 < maxIterationsPhase1)
        {
            iterationPhase1++; // イテレーション回数をインクリメントします。
            // 面積比率を考慮して、次に拡張する部屋を選択します。
            int roomId = SelectRoomToExpand(availableRoomsPhase1, true);
            if (roomId == -1) break; // 選択できる部屋がない場合はループを終了します。

            // 選択された部屋に対して矩形拡張を試みます。
            bool grew = GrowRect(roomId);

            if (!grew) // 部屋が拡張されなかった場合
            {
                availableRoomsPhase1.Remove(roomId); // この部屋はこれ以上矩形拡張できないため、候補から除外します。
            }
            else // 部屋が拡張された場合
            {
                // 拡張に成功した場合、再度拡張の候補に入れることで、連続的な拡張を促します。
                // (ただし、無限ループにならないように注意が必要。SelectRoomToExpandの確率的選択である程度緩和されます)
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数を更新します。
                                                                             // Debug.Log($"Phase 1, Iter {iterationPhase1}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}"); // デバッグログ（コメントアウトされています）。
        }
        if (iterationPhase1 >= maxIterationsPhase1) Debug.LogWarning("Phase 1 reached max iterations."); // 最大イテレーション回数に達した場合は警告ログを出力します。


        // --- フェーズ2: L字型拡張 (L-Shape Growth) ---
        // フェーズ1と同様に、拡張可能な部屋のIDを格納するHashSetを初期化します。
        HashSet<int> availableRoomsPhase2 = new HashSet<int>(_roomDefinitions.Values.Select(r => r.ID));
        int maxIterationsPhase2 = (totalPlaceable - cellsAssigned) * _roomDefinitions.Count; // 無限ループ防止用の最大イテレーション回数を設定します。隙間埋め的な処理なので多めに見積もります。
        int iterationPhase2 = 0; // 現在のイテレーション回数を初期化します。

        // 拡張可能な部屋が存在し、かつ割り当てられたセル数が全配置可能セル数未満で、
        // さらに最大イテレーション回数に達していない間、ループを続けます。
        while (availableRoomsPhase2.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase2 < maxIterationsPhase2)
        {
            iterationPhase2++; // イテレーション回数をインクリメントします。
            // 面積比率はもうあまり考慮せず、拡張可能な部屋を優先します？ では最大サイズ考慮せず。
            // 比率を考慮せずに次に拡張する部屋を選択します。
            int roomId = SelectRoomToExpand(availableRoomsPhase2, false);
            if (roomId == -1) break; // 選択できる部屋がない場合はループを終了します。

            // 選択された部屋に対してL字型拡張を試みます。
            bool grew = GrowLShape(roomId);

            if (!grew) // 部屋が拡張されなかった場合
            {
                availableRoomsPhase2.Remove(roomId); // この部屋はこれ以上L字拡張できないため、候補から除外します。
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数を更新します。
                                                                             // Debug.Log($"Phase 2, Iter {iterationPhase2}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}"); // デバッグログ（コメントアウトされています）。
        }
        if (iterationPhase2 >= maxIterationsPhase2) Debug.LogWarning("Phase 2 reached max iterations."); // 最大イテレーション回数に達した場合は警告ログを出力します。


        // --- フェーズ3: 隙間埋め (Fill Gaps) ---
        FillGaps(); // 残っている隙間を埋める処理を呼び出します。
        cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数を再度更新します。


        // 最終チェック: 未割り当てセルがないか確認します。
        bool allCellsFilled = AreAllCellsFilled();
        Debug.Log($"Room expansion finished. Assigned cells: {cellsAssigned}/{totalPlaceable}. All filled: {allCellsFilled}"); // ログ：部屋拡張処理の終了と結果。

        // 全てのセルが埋まらなかった場合、失敗とみなすか、許容するかは要件次第です。
        // if (!allCellsFilled) return false; // 例えば、全てのセルが埋まらない場合は失敗とすることも可能です。

        return true; // 部屋拡張処理の完了。
    }


    /// <summary>
    /// 拡張フェーズで次に拡張する部屋を選択する関数。
    /// </summary>
    /// <param name="availableRoomIds">拡張可能な部屋IDのセット。</param>
    /// <param name="useSizeRatio">面積比率を考慮するかどうか。</param>
    /// <returns>選択された部屋ID。候補がなければ-1を返す。</returns>
    private int SelectRoomToExpand(HashSet<int> availableRoomIds, bool useSizeRatio)
    {
        if (availableRoomIds.Count == 0) return -1; // 拡張可能な部屋がない場合は-1を返します。

        // 現在のサイズと目標サイズの差に基づいて選択するアプローチも考えられます。
        // float GetPriority(int id) {
        //     RoomDefinition room = _roomDefinitions[id]; // 部屋IDを使って部屋定義を取得します。
        //     float targetRatio = room.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio); // 目標の面積比率を計算します。
        //     float currentRatio = (float)room.CurrentSize / _totalPlaceableCells; // 現在の面積比率を計算します。
        //     return Mathf.Max(0, targetRatio - currentRatio); // 目標に達していないほど優先度が高くなります（0以上）。
        // }
        // var candidates = availableRoomIds.Select(id => new { id, priority = GetPriority(id) }).Where(x => x.priority > 0).ToList(); // 優先度が0より大きい候補を選択します。
        // if (candidates.Count == 0) return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count)); // 優先度0の候補しかない場合はランダムに選択します。
        // float totalPriority = candidates.Sum(x => x.priority); // 全候補の優先度の合計を計算します。
        // double randomValue = _random.NextDouble() * totalPriority; // 0からtotalPriorityまでのランダムな値を生成します。
        // float cumulative = 0; // 累積優先度を初期化します。
        // foreach (var candidate in candidates) { // 各候補について処理します。
        //      cumulative += candidate.priority; // 累積優先度に現在の候補の優先度を加算します。
        //      if (randomValue < cumulative) return candidate.id; // ランダムな値が累積優先度未満になったら、その候補のIDを返します。
        // }
        // return candidates.Last().id; // フォールバックとして最後の候補のIDを返します。


        // 論文 に近い実装 (面積比率に基づく確率的選択)。
        if (useSizeRatio) // 面積比率を考慮する場合。
        {
            var candidates = availableRoomIds.ToList(); // 拡張可能な部屋IDのリストを作成します。
            // 候補の部屋の面積比率の合計を計算します。
            float totalRatio = candidates.Sum(id => _roomDefinitions[id].SizeRatio); // idはRoomDefinition.IDです。

            if (totalRatio <= 0) // 比率の合計が0以下の場合（通常は発生しないはず）。
            {
                // 比率がない場合はランダムに選択します。
                return candidates[_random.Next(candidates.Count)];
            }

            // 0からtotalRatioまでのランダムな値を生成します。
            double randomValue = _random.NextDouble() * totalRatio;
            float cumulativeRatio = 0; // 累積比率を初期化します。

            // 各候補の部屋について処理します。
            foreach (int roomId in candidates) // roomIdはRoomDefinition.IDです。
            {
                cumulativeRatio += _roomDefinitions[roomId].SizeRatio; // 累積比率に現在の部屋の面積比率を加算します。
                if (randomValue < cumulativeRatio) // ランダムな値が累積比率未満になったら、その部屋IDを返します。
                {
                    return roomId;
                }
            }
            return candidates.Last(); // フォールバックとして最後の候補のIDを返します。
        }
        else // 面積比率を考慮しない場合。
        {
            // ランダムに選択します。
            return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count));
        }
    }

    /// <summary>
    /// 指定された部屋IDの部屋を矩形に拡張しようと試みる関数。
    /// </summary>
    /// <param name="roomId">拡張対象の部屋のID。</param>
    /// <returns>拡張に成功した場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowRect(int roomId) // roomIdはRoomDefinition.IDです。
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合に部屋が矩形を維持できるかチェック
        // 4. 矩形を維持できるラインの中で最も長いものを選択 (複数あればランダム)
        // 5. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 6. 目標サイズ(推定)に達したら拡張を止めるオプション (フェーズ1のみ)
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー実装です。実際のロジックに置き換えてください。
    }

    /// <summary>
    /// 指定された部屋IDの部屋をL字型に拡張しようと試みる関数。
    /// </summary>
    /// <param name="roomId">拡張対象の部屋のID。</param>
    /// <returns>拡張に成功した場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowLShape(int roomId) // roomIdはRoomDefinition.IDです。
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合にL字型になるか、または既存のL字型を維持するかチェック
        // 4. U字型にならないようにチェック
        // 5. 拡張可能なラインの中で最も長いものを選択 (複数あればランダム)
        // 6. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー実装です。実際のロジックに置き換えてください。
    }

    /// <summary>
    /// グリッド上の未割り当ての隙間セルを埋める関数。
    /// </summary>
    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps..."); // ログ：隙間埋め処理の開始。
        List<Vector2Int> gaps = FindEmptyCells(); // 未割り当てセル（隙間）のリストを取得します。
        int filledCount = 0; // 埋められたセル数をカウントする変数を初期化します。

        // 各未割り当てセルについて処理します。
        foreach (Vector2Int gapPos in gaps)
        {
            if (_grid[gapPos.x, gapPos.y] != -1) continue; // もしセルが既に埋まっている場合はスキップします（通常は起こり得ない）。

            Dictionary<int, int> neighborCounts = new Dictionary<int, int>(); // 隣接する部屋IDとその出現回数を格納する辞書。
            int maxCount = 0; // 隣接する部屋の最大出現回数。
            int bestNeighborId = -1; // 最も多く隣接している部屋のID。

            // 上下左右の隣接セルを定義します。
            Vector2Int[] neighbors = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            // 隣接する部屋IDをカウントします。
            foreach (var offset in neighbors)
            {
                Vector2Int nPos = gapPos + offset; // 隣接セルの位置を計算します。
                // 隣接セルがグリッド範囲内であるかを確認します。
                if (nPos.x >= 0 && nPos.x < _gridSize.x && nPos.y >= 0 && nPos.y < _gridSize.y)
                {
                    int neighborId = _grid[nPos.x, nPos.y]; // 隣接セルの部屋IDを取得します。
                    if (neighborId > 0) // 有効な部屋ID（0や-1でない）の場合。
                    {
                        if (!neighborCounts.ContainsKey(neighborId)) neighborCounts[neighborId] = 0; // 辞書にIDがなければ初期化。
                        neighborCounts[neighborId]++; // IDの出現回数をインクリメント。

                        if (neighborCounts[neighborId] > maxCount) // 現在のIDの出現回数が最大値より大きい場合。
                        {
                            maxCount = neighborCounts[neighborId]; // 最大値を更新。
                            bestNeighborId = neighborId; // 最適な隣接部屋IDを更新。
                        }
                        // 同数の場合は、より面積の小さい部屋を優先するなどのルールも考えられます。
                        else if (neighborCounts[neighborId] == maxCount && bestNeighborId != -1)
                        {
                            // 例: 面積比率が小さい方を優先 (隙間は小さい部屋が吸収しやすいかもしれないという仮定)。
                            // bestNeighborIdの部屋とneighborIdの部屋の面積比率を比較します。
                            if (_roomDefinitions[neighborId].SizeRatio < _roomDefinitions[bestNeighborId].SizeRatio)
                            {
                                bestNeighborId = neighborId; // より面積比率が小さい部屋を最適とします。
                            }
                        }
                    }
                }
            }

            // 最も多く隣接している部屋に現在の隙間セルを割り当てます。
            if (bestNeighborId != -1)
            {
                _grid[gapPos.x, gapPos.y] = bestNeighborId; // グリッドに部屋IDを割り当てます。
                if (_roomDefinitions.ContainsKey(bestNeighborId)) // 念のため、部屋定義が存在するか確認。
                {
                    _roomDefinitions[bestNeighborId].CurrentSize++; // 割り当てた部屋の現在のサイズを増やします。
                }
                filledCount++; // 埋めたセル数をインクリメントします。
            }
            else
            {
                Debug.LogWarning($"Could not fill gap at {gapPos} - no valid neighbors found."); // 適切な隣接部屋が見つからなかった場合の警告。
            }
        }
        Debug.Log($"Filled {filledCount} gap cells."); // ログ：埋められた隙間セルの数。
    }


    /// <summary>
    /// グリッド内に未割り当てセル(-1)が残っているか確認する関数。
    /// </summary>
    /// <returns>全てのセルが割り当て済みであればtrue、そうでなければfalse。</returns>
    private bool AreAllCellsFilled()
    {
        // グリッド全体を走査します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // 未割り当てセル（値が-1）が見つかった場合。
                {
                    return false; // 未割り当てセルが存在するためfalseを返します。
                }
            }
        }
        return true; // 全てのセルが割り当て済み（-1のセルが存在しない）ためtrueを返します。
    }

    /// <summary>
    /// グリッド内の未割り当てセル(-1)のリストを取得する関数。
    /// </summary>
    /// <returns>未割り当てセルの座標リスト。</returns>
    private List<Vector2Int> FindEmptyCells()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>(); // 未割り当てセルの座標を格納するリストを初期化します。
        // グリッド全体を走査します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // 未割り当てセル（値が-1）が見つかった場合。
                {
                    emptyCells.Add(new Vector2Int(x, y)); // その座標をリストに追加します。
                }
            }
        }
        return emptyCells; // 未割り当てセルのリストを返します。
    }
}