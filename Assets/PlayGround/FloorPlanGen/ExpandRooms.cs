using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
//ステップ2: 部屋を拡張するメインのコルーチン.
public partial class FloorPlanGenerator : MonoBehaviour
{
    public float MaxSizeGrowRect = 0.5f;//GrowRectで拡張される部屋の限界.0.5なら、全ての部屋が拡張されても建物の半分のサイズとなる.

    private enum ExpansionType { Up, Down, Right, Left, Lshape }
    private bool[,] IsExpanded;//最初は設定されていない.GrowRect,GrowShapeの直前でそれぞれ初期化.
    private void SetIsExpanded(int roomNum)
    {
        IsExpanded = new bool[roomNum, 5];//5はUp, Down, Right, Left, Lshape,ただしLshapeはGrowRectでは使われない.
        //RoomIdは1から始まることに注意.
    }
    /// <summary>
    /// SizeRatioに従って確率的に部屋を選択し、リスト内でのインデックスを返す.
    /// </summary>
    private int SelectRoom(List<RoomDefinition> rooms)
    {
        // リストが空の場合は-1を返す //
        if (rooms == null || rooms.Count == 0)
        {
            return -1;
        }

        float totalSizeRatio = 0f;
        // 全ての部屋のSizeRatioの合計を計算する //
        foreach (var room in rooms)
        {
            totalSizeRatio += room.SizeRatio;
        }

        if (totalSizeRatio <= 0f)
        {
            return -1; // 全てのSizeRatioが0または負の場合、選択不可とする
        }
        // 0からtotalSizeRatioまでの乱数を生成する //
        float randomPoint = UnityEngine.Random.Range(0f, totalSizeRatio);

        float currentSum = 0f;
        // 各部屋のSizeRatioの範囲内に乱数が含まれるかを確認して部屋を選択する //
        for (int i = 0; i < rooms.Count; i++)
        {
            currentSum += rooms[i].SizeRatio;
            // 乱数が現在の累積合計以下であれば、この部屋を選択する //
            if (randomPoint <= currentSum) // randomPointがcurrentSumの区間に落ちた場合
            {
                return i; // 選択された部屋のインデックスを返す
            }
        }
        Debug.Log("Something went wrong on Selectroom");
        return -1;
    }

    /// <summary>
    /// ステップ2: 部屋を拡張するメインのコルーチン.
    /// 部屋を矩形に拡張し、その後L字型に拡張し、最後に残りのギャップを埋めます。
    /// </summary>
    private bool ExpandRooms()
    {
        Debug.Log("Starting room expansion...");
        List<RoomDefinition> _roomsToExpandP1 = _roomDefinitions.ToList();
        List<RoomDefinition> _roomsToExpandP2 = _roomDefinitions.ToList();
        SetIsExpanded(_roomDefinitions.Count);
        // フェーズ1: 矩形拡張
        Debug.Log("Phase 1: Rectangular Expansion");
        int iterationCount = 0;
        while (_roomsToExpandP1.Any()) // _roomsToExpandに部屋がある限り.
        {
            // このイテレーションで処理する部屋を確率的に選択し、拡張を試みます。

            var room = _roomsToExpandP1[SelectRoom(_roomsToExpandP1)];
            if (GrowRect(room)) // 部屋を矩形に拡張できるか試みます。
            {
                // 部屋が拡張できた場合、引き続き _roomDefinitions に残しておく
            }
            else // 拡張できなかった場合 (canGrow -- "いいえ" --> removeRoom)
            {
                // 拡張できなくなった部屋を候補リストから除外します。
                // _roomDefinitions から直接削除します。
                _roomsToExpandP1.Remove(room);
            }
            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // 無限ループ防止のための安全策
            {
                Debug.LogWarning("Rectangular expansion phase reached max iterations. Breaking early.");
                break; // ループを強制終了します。
            }
        }

        if (MVinstance.todebug == ToDebug.GrowRect)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }

        // フェーズ2: L字型拡張
        SetIsExpanded(_roomDefinitions.Count);
        Debug.Log("Phase 2: L-Shape Expansion");
        iterationCount = 0; // イテレーションカウントをリセットします。
        while (_roomsToExpandP2.Any()) //_roomsToExpandに部屋がある限り.
        {

            var room = _roomsToExpandP2[SelectRoom(_roomsToExpandP2)];
            if (GrowLShape(room)) // 部屋をL字型に拡張できるか試みます。
            {

            }
            else // 拡張できなかった場合
            {
                // 拡張できなくなった部屋を候補リストから除外します。
                // _roomDefinitions から直接削除します。
                _roomsToExpandP2.Remove(room);
            }


            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // 無限ループ防止のための安全策
            {
                Debug.LogWarning("L-Shape expansion phase reached max iterations. Breaking early.");
                break; // ループを強制終了します。
            }
        }


        // フェーズ3: ギャップ埋め
        Debug.Log("Phase 3: Filling Gaps");
        FillGaps();

        Debug.Log("Room expansion completed.");
        return true;
    }



    /// <summary>
    /// 部屋を矩形に拡張しようとします。
    /// 論文の「GrowRect」に相当し、最大の矩形領域への拡張を試みます。
    /// </summary>
    /// <returns>部屋が拡張された場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowRect(RoomDefinition room)
    {
        // 部屋のバウンディングボックスを取得
        RectInt currentBounds = room.Bounds;
        if (room.CurrentSize == 0) // シードがまだ配置されていない部屋は拡張できません
        {
            Debug.Log("false in growrect");
            return false;
        }

        // 拡張可能な方向と最大の長方形領域を見つけます。
        List<(RectInt newRect, int addedCells, ExpansionType type)> possibleExpansions = new List<(RectInt, int, ExpansionType)>();

        // 上方向への拡張
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Up] == false)
        {
            for (int h = 1; ; h++) // 新しい高さ
            {
                if (currentBounds.yMax + h > _gridSize.y) break; // グリッドのY方向の境界チェック.yMaxはy+heightを返す.

                bool canExpandRow = true;
                for (int x = currentBounds.xMin; x < currentBounds.xMax; x++)//xからx+width-1までの値をとる.
                {
                    if (_grid[x, currentBounds.yMax + h - 1] != -1) // 未割り当ての配置可能セルであることmaxでheightやwidthを足すとき-1.
                    {
                        canExpandRow = false;
                        break;
                    }
                }
                if (!canExpandRow) break;
                //ここに到達したなら、(x,x+width-1,y,y+height+h-1)までのマスは-1であり、上方向に追加する余地が存在する.
                possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.yMax, currentBounds.width, h), currentBounds.width * h, ExpansionType.Up));
                //矩形情報と増加する面積をリストに追加.複数のhについて存在しうる.
            }
        }
        // 下方向への拡張
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Down] == false)
        {
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

                possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y - h, currentBounds.width, h), currentBounds.width * h, ExpansionType.Down));
            }
        }

        // 右方向への拡張
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Right] == false)
        {
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

                possibleExpansions.Add((new RectInt(currentBounds.xMax, currentBounds.y, w, currentBounds.height), currentBounds.height * w, ExpansionType.Right));
            }
        }
        // 左方向への拡張
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Left] == false)
        {
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

                possibleExpansions.Add((new RectInt(currentBounds.x - w, currentBounds.y, w, currentBounds.height), currentBounds.height * w, ExpansionType.Left));
            }
        }

        // 最も大きく拡張できる機会を選択します。
        // 複数ある場合はランダムに選択することで多様性を確保します。
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCells > 0) // 追加セルが0より大きいもののみ
            .OrderByDescending(e => e.addedCells) // 追加セル数でソート
            .ThenBy(e => _random.Next()) // 同じ追加セル数の場合はランダム
            .ToList();

        var totalSizeRatio = _roomDefinitions.Sum(r => r.SizeRatio);
        var maxSizeForRoom = _totalPlaceableCells * (room.SizeRatio / totalSizeRatio) * MaxSizeGrowRect; // // この部屋の最大許容サイズを事前に計算.

        // bestExpansionsリストは追加セル数の多い順にソートされています。
        // このリストを先頭から順に探索し、条件を満たす最初の拡張案を採用します。
        // 利用可能な拡張候補を一つずつ確認します。
        foreach (var expansion in bestExpansions)
        {
            // この拡張を適用した後の部屋のサイズが、事前に計算した最大許容サイズを超えないかを確認する
            // 条件: (現在の部屋のサイズ + 拡張による追加セル数) <= 最大許容サイズ
            if (room.CurrentSize + expansion.addedCells <= maxSizeForRoom)
            {
                // 条件を満たす最初の拡張（= リストの並び順により最も効率的な拡張）が見つかったので、これを適用します。
                var selectedExpansion = expansion; // 適用する拡張をこの変数に格納します。
                ApplyGrowth(room, selectedExpansion.newRect, selectedExpansion.addedCells); // 部屋を実際に拡張する関数を呼び出します。
                IsExpanded[room.ID - 1, (int)selectedExpansion.type] = true;
                Debug.Log($"Room {room.ID} expanded rectangularly to {selectedExpansion.newRect} adding {selectedExpansion.addedCells} cells. Current size: {room.CurrentSize},ExpansionType: {selectedExpansion.type}"); // 拡張結果をログに出力します。
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 部屋をL字型に拡張しようとします。
    /// </summary>
    /// <param name="room">拡張する部屋の定義。</param>
    /// <returns>部屋が拡張された場合はtrue、そうでない場合はfalse。</returns>
    private bool GrowLShape(RoomDefinition room)
    {
        // 部屋のバウンディングボックスを取得
        RectInt currentBounds = room.Bounds;
        if (room.CurrentSize == 0) // シードがまだ配置されていない部屋は拡張できません
        {
            Debug.Log("false in growrect");
            return false;
        }


        // 拡張情報.GrowRectのとは違う.
        List<(RectInt? fullLineRect, RectInt? partialLineRect, int addedfullCells, int addedpartialCells, ExpansionType type, bool isLshaped)> possibleExpansions = new List<(RectInt?, RectInt?, int, ExpansionType, bool)>();
        (RectInt UpLine, RectInt DownLine, RectInt RightLine, RectInt LeftLine) EdgeLines = GetFullLines(room);
        RectInt? fullLineRect, partialLineRect;
        RectInt TemporaryLine;//暫定的な最大ライン.Lshape
        int h, w, hatLS, watLS, LeftLastx, RightLastx, DownLasty, UpLasty, Leftwidth, Rightwidth, Downheight, Upheight;
        bool LshapeMode, canExpandRow, canExpandCol, IsLeftLineExsits, IsRightLineExsits, IsDownLineExsits, IsUpLineExsits;
        float randomNum

        // 上
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Up] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; h = 1; hatLS = 0;
            TemporaryLine = EdgeLines.UpLine;
            while (true)
            {
                if (currentBounds.yMax + h > _gridSize.y) break;

                canExpandRow = true;
                if (LshapeMode == false)
                {
                    for (int x = TemporaryLine.xMin; x < TemporaryLine.xMax; x++)//xからx+width-1までの値をとる.
                    {
                        if (_grid[x, TemporaryLine.yMax + h - 1] != -1) // 未割り当ての配置可能セルであることmaxでheightやwidthを足すとき-1.
                        {
                            canExpandRow = false;
                            break;
                        }
                    }
                    if (!canExpandRow)
                    {
                        LshapeMode = true;//LshapeModeに移行.h更新せず.
                        hatLS = h;//hatLsはLshapeModeが始まった高さを記録する.その高さは既に全ライン拡張ではない.
                        continue;
                    }
                    else
                    {
                        fullLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMax, TemporaryLine.width, h);
                    }
                }
                else//L字拡張
                {
                    if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;//既にL字拡張されていたら終了.

                    if (h == hatLS)//最初.奥行きと幅が最低でも2である必要がある.グリッドを精査して部分的かつL字的な最大ラインの特定.TemporaryLineの更新.
                    {
                        //左端と右端にそれぞれ拡張可能(2*2以上)のエリアがあるかを特定
                        for (LeftLastx = TemporaryLine.x; LeftLastx < TemporaryLine.xMax; LeftLastx++)
                        {
                            IsLeftLineExsits = true;
                            for (int y = TemporaryLine.yMax; y < TemporaryLine.yMax + 2; y++)
                            {
                                if (_grid[LeftLastx, y] != -1) IsLeftLineExsits = false;
                            }
                            if (!IsLeftLineExsits) break; //奥行きに2マスが存在しなかったら、そこで終了.
                        }
                        for (RightLastx = TemporaryLine.xMax - 1; RightLastx >= TemporaryLine.x; RightLastx--)
                        {
                            IsRightLineExsits = true;
                            for (int y = TemporaryLine.yMax; y < TemporaryLine.yMax + 2; y++)
                            {
                                if (_grid[RightLastx, y] != -1) IsRightLineExsits = false;
                            }
                            if (!IsRightLineExsits) break; //奥行きに2マスが存在しなかったら、そこで終了.
                        }
                        Leftwidth = LeftLastx - TemporaryLine.x;//LeftLastxは、初めて奥行きに2マスが存在しないマスの座標.width>=0
                        Rightwidth = (TemporaryLine.xMax - 1) - RightLastx;

                        if (Leftwidth < 2 && Rightwidth < 2) break;//どちらの幅も2マスより小さいなら,L字拡張できず,ループ自体を終了
                        else
                        {
                            if (Leftwidth > Rightwidth)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                            }
                            else if (Leftwidth < Rightwidth)
                            {
                                TemporaryLine = new RectInt(RightLastx + 1, TemporaryLine.y, Rightwidth, 1);//RightLastx < TemoporaryLine.xmax-1に注意.RightLastxが,L字拡張出来なかった場所であることにも注意
                            }
                            else//Leftwidth == Rightwidth
                            {
                                randomNum = Random.Range(0f, 1f);
                                {
                                    if ( randomNum < 0.5f)//左
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                                    }
                                    else//右
                                    {
                                        TemporaryLine = new RectInt(RightLastx + 1, TemporaryLine.y, Rightwidth, 1);
                                    }
                                }
                            }
                        }
                        h++;
                        continue;
                    }
                    else
                    {
                        for (int x = TemporaryLine.xMin; x < TemporaryLine.xMax; x++)
                        {
                            if (_grid[x, TemporaryLine.yMax + h - 1] != -1)
                            {
                                canExpandRow = false;
                                break;
                            }
                        }
                        if (!canExpandRow) break;
                        else partialLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMax, TemporaryLine.width, h - hatLS + 1);
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Up, LshapeMode));
                h++;
            }

        }
        // 下
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Down] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; h = 1; hatLS = 0;
            TemporaryLine = EdgeLines.DownLine;
            while (true)
            {
                if (currentBounds.yMin - h < 0) break;

                canExpandRow = true;
                if (LshapeMode == false)
                {
                    for (int x = TemporaryLine.xMin; x < TemporaryLine.xMax; x++)
                    {
                        if (_grid[x, TemporaryLine.yMin - h] != -1)
                        {
                            canExpandRow = false;
                            break;
                        }
                    }
                    if (!canExpandRow)
                    {
                        LshapeMode = true;
                        hatLS = h;
                        continue;
                    }
                    else
                    {
                        fullLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMin - h, TemporaryLine.width, h);
                    }
                }
                else//L字拡張
                {
                    if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (h == hatLS)
                    {
                        //左端と右端にそれぞれ拡張可能(2*2以上)のエリアがあるかを特定
                        for (LeftLastx = TemporaryLine.x; LeftLastx < TemporaryLine.xMax; LeftLastx++)
                        {
                            IsLeftLineExsits = true;
                            for (int y = TemporaryLine.yMin - 1; y >= TemporaryLine.yMin - 2; y--)
                            {
                                if (y < 0 || _grid[LeftLastx, y] != -1) IsLeftLineExsits = false;
                            }
                            if (!IsLeftLineExsits) break;
                        }
                        for (RightLastx = TemporaryLine.xMax - 1; RightLastx >= TemporaryLine.x; RightLastx--)
                        {
                            IsRightLineExsits = true;
                            for (int y = TemporaryLine.yMin - 1; y >= TemporaryLine.yMin - 2; y--)
                            {
                                if (y < 0 || _grid[RightLastx, y] != -1) IsRightLineExsits = false;
                            }
                            if (!IsRightLineExsits) break;
                        }
                        Leftwidth = LeftLastx - TemporaryLine.x;
                        Rightwidth = (TemporaryLine.xMax - 1) - RightLastx;

                        if (Leftwidth < 2 && Rightwidth < 2) break;
                        else
                        {
                            if (Leftwidth > Rightwidth)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                            }
                            else if (Leftwidth < Rightwidth)
                            {
                                TemporaryLine = new RectInt(RightLastx + 1, TemporaryLine.y, Rightwidth, 1);
                            }
                            else//Leftwidth == Rightwidth
                            {
                                randomNum = Random.Range(0f, 1f);
                                {
                                    if (randomNum < 0.5f)//左
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                                    }
                                    else//右
                                    {
                                        TemporaryLine = new RectInt(RightLastx + 1, TemporaryLine.y, Rightwidth, 1);
                                    }
                                }
                            }
                        }
                        h++;
                        continue;
                    }
                    else
                    {
                        for (int x = TemporaryLine.xMin; x < TemporaryLine.xMax; x++)
                        {
                            if (_grid[x, TemporaryLine.yMin - h] != -1)
                            {
                                canExpandRow = false;
                                break;
                            }
                        }
                        if (!canExpandRow) break;
                        else partialLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMin - (h - hatLS + 1), TemporaryLine.width, (h - hatLS + 1));
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Down, LshapeMode));
                h++;
            }
        }

        // 右
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Right] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; w = 1; watLS = 0;
            TemporaryLine = EdgeLines.RightLine;
            while (true)
            {
                if (currentBounds.xMax + w > _gridSize.x) break;

                canExpandCol = true;
                if (LshapeMode == false)
                {
                    for (int y = TemporaryLine.yMin; y < TemporaryLine.yMax; y++)
                    {
                        if (_grid[TemporaryLine.xMax + w - 1, y] != -1)
                        {
                            canExpandCol = false;
                            break;
                        }
                    }
                    if (!canExpandCol)
                    {
                        LshapeMode = true;
                        watLS = w;
                        continue;
                    }
                    else
                    {
                        fullLineRect = new RectInt(TemporaryLine.xMax, TemporaryLine.y, w, TemporaryLine.height);
                    }
                }
                else//L字拡張
                {
                    if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (w == watLS)
                    {
                        //上端と下端にそれぞれ拡張可能(2*2以上)のエリアがあるかを特定
                        for (DownLasty = TemporaryLine.y; DownLasty < TemporaryLine.yMax; DownLasty++)
                        {
                            IsDownLineExsits = true;
                            for (int x = TemporaryLine.xMax; x < TemporaryLine.xMax + 2; x++)
                            {
                                if (x >= _gridSize.x || _grid[x, DownLasty] != -1) IsDownLineExsits = false;
                            }
                            if (!IsDownLineExsits) break;
                        }
                        for (UpLasty = TemporaryLine.yMax - 1; UpLasty >= TemporaryLine.y; UpLasty--)
                        {
                            IsUpLineExsits = true;
                            for (int x = TemporaryLine.xMax; x < TemporaryLine.xMax + 2; x++)
                            {
                                if (x >= _gridSize.x || _grid[x, UpLasty] != -1) IsUpLineExsits = false;
                            }
                            if (!IsUpLineExsits) break;
                        }
                        Downheight = DownLasty - TemporaryLine.y;
                        Upheight = (TemporaryLine.yMax - 1) - UpLasty;

                        if (Downheight < 2 && Upheight < 2) break;
                        else
                        {
                            if (Downheight > Upheight)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                            }
                            else if (Downheight < Upheight)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, UpLasty + 1, 1, Upheight);
                            }
                            else//Downheight == Upheight
                            {
                                randomNum = Random.Range(0f, 1f);
                                {
                                    if (randomNum < 0.5f)//下
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                                    }
                                    else//上
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, UpLasty + 1, 1, Upheight);
                                    }
                                }
                            }
                        }
                        w++;
                        continue;
                    }
                    else
                    {
                        for (int y = TemporaryLine.yMin; y < TemporaryLine.yMax; y++)
                        {
                            if (_grid[TemporaryLine.xMax + w - 1, y] != -1)
                            {
                                canExpandCol = false;
                                break;
                            }
                        }
                        if (!canExpandCol) break;
                        else partialLineRect = new RectInt(TemporaryLine.xMax, TemporaryLine.y, w - watLS + 1, TemporaryLine.height);
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Right, LshapeMode));
                w++;
            }
        }

        // 左
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Left] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; w = 1; watLS = 0;
            TemporaryLine = EdgeLines.LeftLine;
            while (true)
            {
                if (currentBounds.xMin - w < 0) break;

                canExpandCol = true;
                if (LshapeMode == false)
                {
                    for (int y = TemporaryLine.yMin; y < TemporaryLine.yMax; y++)
                    {
                        if (_grid[TemporaryLine.xMin - w, y] != -1)
                        {
                            canExpandCol = false;
                            break;
                        }
                    }
                    if (!canExpandCol)
                    {
                        LshapeMode = true;
                        watLS = w;
                        continue;
                    }
                    else
                    {
                        fullLineRect = new RectInt(TemporaryLine.xMin - w, TemporaryLine.y, w, TemporaryLine.height);
                    }
                }
                else//L字拡張
                {
                    if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (w == watLS)
                    {
                        //上端と下端にそれぞれ拡張可能(2*2以上)のエリアがあるかを特定
                        for (DownLasty = TemporaryLine.y; DownLasty < TemporaryLine.yMax; DownLasty++)
                        {
                            IsDownLineExsits = true;
                            for (int x = TemporaryLine.xMin - 1; x >= TemporaryLine.xMin - 2; x--)
                            {
                                if (x < 0 || _grid[x, DownLasty] != -1) IsDownLineExsits = false;
                            }
                            if (!IsDownLineExsits) break;
                        }
                        for (UpLasty = TemporaryLine.yMax - 1; UpLasty >= TemporaryLine.y; UpLasty--)
                        {
                            IsUpLineExsits = true;
                            for (int x = TemporaryLine.xMin - 1; x >= TemporaryLine.xMin - 2; x--)
                            {
                                if (x < 0 || _grid[x, UpLasty] != -1) IsUpLineExsits = false;
                            }
                            if (!IsUpLineExsits) break;
                        }
                        Downheight = DownLasty - TemporaryLine.y;
                        Upheight = (TemporaryLine.yMax - 1) - UpLasty;

                        if (Downheight < 2 && Upheight < 2) break;
                        else
                        {
                            if (Downheight > Upheight)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                            }
                            else if (Downheight < Upheight)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, UpLasty + 1, 1, Upheight);
                            }
                            else//Downheight == Upheight
                            {
                                randomNum = Random.Range(0f, 1f);
                                {
                                    if (randomNum < 0.5f)//下
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                                    }
                                    else//上
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, UpLasty + 1, 1, Upheight);
                                    }
                                }
                            }
                        }
                        w++;
                        continue;
                    }
                    else
                    {
                        for (int y = TemporaryLine.yMin; y < TemporaryLine.yMax; y++)
                        {
                            if (_grid[TemporaryLine.xMin - w, y] != -1)
                            {
                                canExpandCol = false;
                                break;
                            }
                        }
                        if (!canExpandCol) break;
                        else partialLineRect = new RectInt(TemporaryLine.xMin - (w - watLS + 1), TemporaryLine.y, w - watLS + 1, TemporaryLine.height);
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Left, LshapeMode));
                w++;
            }
        }

        // 最も大きく拡張できる機会を選択します。
        // 複数ある場合はランダムに選択することで多様性を確保します。
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCells > 0) // 追加セルが0より大きいもののみ
            .OrderByDescending(e => e.addedCells) // 追加セル数でソート
            .ThenBy(e => _random.Next()) // 同じ追加セル数の場合はランダム
            .ToList();

        var totalSizeRatio = _roomDefinitions.Sum(r => r.SizeRatio);
        var maxSizeForRoom = _totalPlaceableCells * (room.SizeRatio / totalSizeRatio) * MaxSizeGrowRect; // // この部屋の最大許容サイズを事前に計算.

        // bestExpansionsリストは追加セル数の多い順にソートされています。
        // このリストを先頭から順に探索し、条件を満たす最初の拡張案を採用します。
        // 利用可能な拡張候補を一つずつ確認します。
        foreach (var expansion in bestExpansions)
        {
            // この拡張を適用した後の部屋のサイズが、事前に計算した最大許容サイズを超えないかを確認する
            // 条件: (現在の部屋のサイズ + 拡張による追加セル数) <= 最大許容サイズ
            if (room.CurrentSize + expansion.addedCells <= maxSizeForRoom)
            {
                // 条件を満たす最初の拡張（= リストの並び順により最も効率的な拡張）が見つかったので、これを適用します。
                var selectedExpansion = expansion; // 適用する拡張をこの変数に格納します。
                ApplyGrowth(room, selectedExpansion.newRect, selectedExpansion.addedCells); // 部屋を実際に拡張する関数を呼び出します。
                IsExpanded[room.ID - 1, (int)selectedExpansion.type] = true;
                Debug.Log($"Room {room.ID} expanded rectangularly to {selectedExpansion.newRect} adding {selectedExpansion.addedCells} cells. Current size: {room.CurrentSize},ExpansionType: {selectedExpansion.type}"); // 拡張結果をログに出力します。
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  対象の領域(newRect)のセルを部屋のIDで埋め、部屋のサイズ、バウンディングボックスを更新
    /// </summary>
    /// <param name="room">拡張する部屋の定義。</param>
    /// <param name="newRect">追加された新しい矩形領域。</param>
    /// <param name="addedCells">追加されるセルの数。</param>
    private void ApplyGrowth(RoomDefinition room, RectInt newRect, int addedCells)
    {
        // 新しい領域のセルを部屋のIDで埋めます。
        for (int x = newRect.xMin; x < newRect.xMax; x++)
        {
            for (int y = newRect.yMin; y < newRect.yMax; y++)
            {
                // 既存の部屋のセルではない、かつ未割り当てのセルのみを更新.本当はする必要ないけど.
                if (_grid[x, y] == -1)
                {
                    _grid[x, y] = room.ID;
                }
            }
        }
        room.CurrentSize += addedCells; // 部屋のサイズを更新
        RectInt CurrentRect = room.Bounds;
        room.Bounds = MergeRectInt(CurrentRect, newRect); // 部屋のバウンディングボックスを更新
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

    //部屋の各端に位置する辺の情報を取得する.GrowLshapeが実行される時点では、辺は必ず各方向につき一つ.
    public (RectInt UpLine, RectInt DownLine, RectInt RightLine, RectInt LeftLine) GetFullLines(RoomDefinition room)
    {
        int roomId = room.ID;

        RectInt upLine = new RectInt();
        RectInt downLine = new RectInt();
        RectInt rightLine = new RectInt();
        RectInt leftLine = new RectInt();

        // 上辺: 実際の部屋の最上部のセルを含む矩形
        int upLineMinX = _gridSize.x;
        int upLineMaxX = -1;
        for (int x = room.Bounds.x; x < room.Bounds.xMax; x++)
        {
            if (_grid[x, room.Bounds.yMax - 1] == roomId)
            {
                upLineMinX = Mathf.Min(upLineMinX, x);
                upLineMaxX = Mathf.Max(upLineMaxX, x);
            }
        }
        upLine = new RectInt(upLineMinX, room.Bounds.yMax - 1, upLineMaxX - upLineMinX + 1, 1);


        // 下辺: 実際の部屋の最下部のセルを含む矩形
        int downLineMinX = _gridSize.x;
        int downLineMaxX = -1;
        for (int x = room.Bounds.x; x < room.Bounds.xMax; x++)
        {
            if (_grid[x, room.Bounds.y] == roomId)
            {
                downLineMinX = Mathf.Min(downLineMinX, x);
                downLineMaxX = Mathf.Max(downLineMaxX, x);
            }
        }
        downLine = new RectInt(downLineMinX, room.Bounds.y, downLineMaxX - downLineMinX + 1, 1);


        // 左辺: 実際の部屋の最も左のセルを含む矩形
        int leftLineMinY = _gridSize.y;
        int leftLineMaxY = -1;
        for (int y = room.Bounds.y; y < room.Bounds.yMax; y++)
        {
            if (_grid[room.Bounds.x, y] == roomId)
            {
                leftLineMinY = Mathf.Min(leftLineMinY, y);
                leftLineMaxY = Mathf.Max(leftLineMaxY, y);
            }
        }
        leftLine = new RectInt(room.Bounds.x, leftLineMinY, 1, leftLineMaxY - leftLineMinY + 1);


        // 右辺: 実際の部屋の最も右のセルを含む矩形
        int rightLineMinY = _gridSize.y;
        int rightLineMaxY = -1;
        for (int y = room.Bounds.y; y < room.Bounds.yMax; y++)
        {
            if (_grid[room.Bounds.xMax - 1, y] == roomId)
            {
                rightLineMinY = Mathf.Min(rightLineMinY, y);
                rightLineMaxY = Mathf.Max(rightLineMaxY, y);
            }
        }
        rightLine = new RectInt(room.Bounds.xMax - 1, rightLineMinY, 1, rightLineMaxY - rightLineMinY + 1);


        return (upLine, downLine, rightLine, leftLine);
    }

    public int CalculateRectArea(RectInt? rect)
    {
        if (rect.HasValue)
        {
            return rect.Value.width * rect.Value.height;
        }
        else return 0;
    }
}