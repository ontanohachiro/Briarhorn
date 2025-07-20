using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// QuikGraphライブラリの名前空間。グラフ構造を扱うために必要です。
using QuikGraph;

// --- ステップ3: DetermineConnectivity とその補助関数 ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// クラスが保持する接続グラフ(_ConnectivityGraph)に基づき、部屋間にドアを設置する壁を決定する関数。
    /// </summary>
    public bool DetermineConnectivity()
    {
        // --- (0) 辺のリストを生成 ---
        // クラスが持つ有向グラフを無向グラフの辺リストに変換
        _doors.Clear();
        List<Tuple<int, int>> connections = ConvertToUndirectedEdgeList(_ConnectivityGraph);
      

        // グローバルな重み減少マップ。ドア設置による影響を蓄積する。
        var doorWeightsReductionH = new float[_gridSize.x, _gridSize.y + 1];
        var doorWeightsReductionV = new float[_gridSize.x + 1, _gridSize.y];

        // --- (2, 3, 4) ドアの決定、設置、周辺重みの更新を各接続について行う ---
        foreach (var connection in connections)
        {
            // --- (辺ごとに固有の)ステップ2: 壁空間に重みを付ける ---
            var positiveWeightsH = new float[_gridSize.x, _gridSize.y + 1];
            var positiveWeightsV = new float[_gridSize.x + 1, _gridSize.y];

            RoomDefinition room1 = GetRoomById(connection.Item1);
            RoomDefinition room2 = GetRoomById(connection.Item2);

            if (room1 == null || room2 == null)
            {
                Debug.LogError($"Error: Room not found for connection ({connection.Item1}, {connection.Item2})");
                continue;
            }

            // グリッドを走査し、この接続ペアに該当する共有壁にのみ重み(+1)を付ける
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int x = 0; x < _gridSize.x; x++)
                {
                    int id1 = _grid[x, y];
                    // 右隣のセルとの境界をチェック
                    if (x + 1 < _gridSize.x)
                    {
                        int id2 = _grid[x + 1, y];
                        // 現在の接続ペア(room1, room2)の境界かチェック
                        if ((id1 == room1.ID && id2 == room2.ID) || (id1 == room2.ID && id2 == room1.ID))
                        {
                            positiveWeightsV[x + 1, y] += 1.0f;
                        }
                    }
                    // 下隣のセルとの境界をチェック
                    if (y + 1 < _gridSize.y)
                    {
                        int id2 = _grid[x, y + 1];
                        // 現在の接続ペア(room1, room2)の境界かチェック
                        if ((id1 == room1.ID && id2 == room2.ID) || (id1 == room2.ID && id2 == room1.ID))
                        {
                            positiveWeightsH[x, y + 1] += 1.0f;
                        }
                    }
                }
            }

            // --- (辺ごとに固有の)ステップ3: ドアの決定 ---
            // ドア候補となる壁のリスト
            var potentialDoors = new List<Tuple<Vector2Int, bool>>();//二個目のbool値は、VかHかを表す.true=V
            float maxEffectiveWeight = -1f;

            // この接続が共有する可能性のある壁をすべてチェック
            // （Boundsは完全ではないが、探索範囲を限定するには十分）
            RectInt combinedBounds = new RectInt(
                Mathf.Min(room1.Bounds.xMin, room2.Bounds.xMin),
                Mathf.Min(room1.Bounds.yMin, room2.Bounds.yMin),
                Mathf.Max(room1.Bounds.xMax, room2.Bounds.xMax) - Mathf.Min(room1.Bounds.xMin, room2.Bounds.xMin),
                Mathf.Max(room1.Bounds.yMax, room2.Bounds.yMax) - Mathf.Min(room1.Bounds.yMin, room2.Bounds.yMin)
            );

            // 垂直な壁の評価
            for (int y = combinedBounds.yMin; y < combinedBounds.yMax; y++)
            {
                for (int x = combinedBounds.xMin; x < combinedBounds.xMax + 1; x++)
                {
                    if (positiveWeightsV[x, y] > 0)
                    {
                        float effectiveWeight = positiveWeightsV[x, y] + doorWeightsReductionV[x, y];
                        if (effectiveWeight > maxEffectiveWeight)
                        {
                            maxEffectiveWeight = effectiveWeight;
                            potentialDoors.Clear();
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), true));
                        }
                        else if (effectiveWeight == maxEffectiveWeight)
                        {
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), true));
                        }
                    }
                }
            }

            // 水平な壁の評価
            for (int y = combinedBounds.yMin; y < combinedBounds.yMax + 1; y++)
            {
                for (int x = combinedBounds.xMin; x < combinedBounds.xMax; x++)
                {
                    if (positiveWeightsH[x, y] > 0)
                    {
                        float effectiveWeight = positiveWeightsH[x, y] + doorWeightsReductionH[x, y];
                        if (effectiveWeight > maxEffectiveWeight)
                        {
                            maxEffectiveWeight = effectiveWeight;
                            potentialDoors.Clear();
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), false));
                        }
                        else if (effectiveWeight == maxEffectiveWeight)
                        {
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), false));
                        }
                    }
                }
            }

            // ドアを設置できる壁が見つからなかった場合
            if (potentialDoors.Count == 0)
            {
                return false;
            }

            // 重みが最大の壁の中からランダムに1つを選択してドアを設置
            var chosenDoor = potentialDoors[UnityEngine.Random.Range(0, potentialDoors.Count)];
            Vector2Int doorPos = chosenDoor.Item1;
            bool isVertical = chosenDoor.Item2;

            Door newDoor = new Door();
            if (isVertical)
            {
                newDoor.Cell1 = new Vector2Int(doorPos.x - 1, doorPos.y);
                newDoor.Cell2 = new Vector2Int(doorPos.x, doorPos.y);
            }
            else
            {
                newDoor.Cell1 = new Vector2Int(doorPos.x, doorPos.y - 1);
                newDoor.Cell2 = new Vector2Int(doorPos.x, doorPos.y);
            }
            newDoor.edge = connection;
            _doors.Add(newDoor);

            // --- ステップ4: 周りの壁の重みを下げる ---
            // グローバルな重み減少マップを更新する
            DecreaseSurroundingWeights(doorPos.x, doorPos.y, isVertical, -0.1f, doorWeightsReductionV, doorWeightsReductionH);
        }
        Debug.Log("Complete Determine Conectivity");
        return true;
    }

    

    /// <summary>
    /// この関数は、ドアの周りにある8つの壁の重みを下げる補助関数。
    /// </summary>
    private void DecreaseSurroundingWeights(int doorX, int doorY, bool isVertical, float reduction, float[,] reductionV, float[,] reductionH)
    {
        // 指定された重み減少マップに対して、壁の重みを更新する
        Action<int, int, bool, float> AddReductionWeight = (x, y, isVert, val) =>
        {
            if (isVert)
            {
                if (x >= 0 && x < _gridSize.x + 1 && y >= 0 && y < _gridSize.y)
                    reductionV[x, y] += val;
            }
            else
            {
                if (x >= 0 && x < _gridSize.x && y >= 0 && y < _gridSize.y + 1)
                    reductionH[x, y] += val;
            }
        };

        if (isVertical)
        {
            AddReductionWeight(doorX, doorY - 1, true, reduction);
            AddReductionWeight(doorX, doorY + 1, true, reduction);
            AddReductionWeight(doorX - 1, doorY, false, reduction);
            AddReductionWeight(doorX - 1, doorY + 1, false, reduction);
            AddReductionWeight(doorX - 1, doorY, true, reduction);
            AddReductionWeight(doorX, doorY, false, reduction);
            AddReductionWeight(doorX, doorY + 1, false, reduction);
            AddReductionWeight(doorX + 1, doorY, true, reduction);
        }
        else
        {
            AddReductionWeight(doorX - 1, doorY, false, reduction);
            AddReductionWeight(doorX + 1, doorY, false, reduction);
            AddReductionWeight(doorX, doorY - 1, false, reduction);
            AddReductionWeight(doorX, doorY - 1, true, reduction);
            AddReductionWeight(doorX + 1, doorY - 1, true, reduction);
            AddReductionWeight(doorX, doorY + 1, false, reduction);
            AddReductionWeight(doorX, doorY, true, reduction);
            AddReductionWeight(doorX + 1, doorY, true, reduction);
        }
    }

    /// <summary>
    /// _roomDefinitionsの中から指定されたIDを持つ部屋を返す補助関数。
    /// </summary>
    private RoomDefinition GetRoomById(int id)
    {
        return _roomDefinitions.FirstOrDefault(r => r.ID == id);
    }
}