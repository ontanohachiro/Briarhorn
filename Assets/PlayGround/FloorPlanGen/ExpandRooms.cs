using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
//�X�e�b�v2: �������g�����郁�C���̃R���[�`��.
public partial class FloorPlanGenerator : MonoBehaviour
{
    public float MaxSizeGrowRect = 0.5f;//GrowRect�Ŋg������镔���̌��E.0.5�Ȃ�A�S�Ă̕������g������Ă������̔����̃T�C�Y�ƂȂ�.

    private enum ExpansionType { Up, Down, Right, Left, Lshape }
    private bool[,] IsExpanded;//�ŏ��͐ݒ肳��Ă��Ȃ�.GrowRect,GrowShape�̒��O�ł��ꂼ�ꏉ����.
    private void SetIsExpanded(int roomNum)
    {
        IsExpanded = new bool[roomNum, 5];//5��Up, Down, Right, Left, Lshape,������Lshape��GrowRect�ł͎g���Ȃ�.
        //RoomId��1����n�܂邱�Ƃɒ���.
    }
    /// <summary>
    /// SizeRatio�ɏ]���Ċm���I�ɕ�����I�����A���X�g���ł̃C���f�b�N�X��Ԃ�.
    /// </summary>
    private int SelectRoom(List<RoomDefinition> rooms)
    {
        // ���X�g����̏ꍇ��-1��Ԃ� //
        if (rooms == null || rooms.Count == 0)
        {
            return -1;
        }

        float totalSizeRatio = 0f;
        // �S�Ă̕�����SizeRatio�̍��v���v�Z���� //
        foreach (var room in rooms)
        {
            totalSizeRatio += room.SizeRatio;
        }

        if (totalSizeRatio <= 0f)
        {
            return -1; // �S�Ă�SizeRatio��0�܂��͕��̏ꍇ�A�I��s�Ƃ���
        }
        // 0����totalSizeRatio�܂ł̗����𐶐����� //
        float randomPoint = UnityEngine.Random.Range(0f, totalSizeRatio);

        float currentSum = 0f;
        // �e������SizeRatio�͈͓̔��ɗ������܂܂�邩���m�F���ĕ�����I������ //
        for (int i = 0; i < rooms.Count; i++)
        {
            currentSum += rooms[i].SizeRatio;
            // ���������݂̗ݐύ��v�ȉ��ł���΁A���̕�����I������ //
            if (randomPoint <= currentSum) // randomPoint��currentSum�̋�Ԃɗ������ꍇ
            {
                return i; // �I�����ꂽ�����̃C���f�b�N�X��Ԃ�
            }
        }
        Debug.Log("Something went wrong on Selectroom");
        return -1;
    }

    /// <summary>
    /// �X�e�b�v2: �������g�����郁�C���̃R���[�`��.
    /// ��������`�Ɋg�����A���̌�L���^�Ɋg�����A�Ō�Ɏc��̃M���b�v�𖄂߂܂��B
    /// </summary>
    private bool ExpandRooms()
    {
        Debug.Log("Starting room expansion...");
        List<RoomDefinition> _roomsToExpandP1 = _roomDefinitions.ToList();
        List<RoomDefinition> _roomsToExpandP2 = _roomDefinitions.ToList();
        SetIsExpanded(_roomDefinitions.Count);
        // �t�F�[�Y1: ��`�g��
        Debug.Log("Phase 1: Rectangular Expansion");
        int iterationCount = 0;
        while (_roomsToExpandP1.Any()) // _roomsToExpand�ɕ������������.
        {
            // ���̃C�e���[�V�����ŏ������镔�����m���I�ɑI�����A�g�������݂܂��B

            var room = _roomsToExpandP1[SelectRoom(_roomsToExpandP1)];
            if (GrowRect(room)) // ��������`�Ɋg���ł��邩���݂܂��B
            {
                // �������g���ł����ꍇ�A�������� _roomDefinitions �Ɏc���Ă���
            }
            else // �g���ł��Ȃ������ꍇ (canGrow -- "������" --> removeRoom)
            {
                // �g���ł��Ȃ��Ȃ�����������⃊�X�g���珜�O���܂��B
                // _roomDefinitions ���璼�ڍ폜���܂��B
                _roomsToExpandP1.Remove(room);
            }
            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // �������[�v�h�~�̂��߂̈��S��
            {
                Debug.LogWarning("Rectangular expansion phase reached max iterations. Breaking early.");
                break; // ���[�v�������I�����܂��B
            }
        }

        if (MVinstance.todebug == ToDebug.GrowRect)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }

        // �t�F�[�Y2: L���^�g��
        SetIsExpanded(_roomDefinitions.Count);
        Debug.Log("Phase 2: L-Shape Expansion");
        iterationCount = 0; // �C�e���[�V�����J�E���g�����Z�b�g���܂��B
        while (_roomsToExpandP2.Any()) //_roomsToExpand�ɕ������������.
        {

            var room = _roomsToExpandP2[SelectRoom(_roomsToExpandP2)];
            if (GrowLShape(room)) // ������L���^�Ɋg���ł��邩���݂܂��B
            {

            }
            else // �g���ł��Ȃ������ꍇ
            {
                // �g���ł��Ȃ��Ȃ�����������⃊�X�g���珜�O���܂��B
                // _roomDefinitions ���璼�ڍ폜���܂��B
                _roomsToExpandP2.Remove(room);
            }


            iterationCount++;
            if (iterationCount > _totalPlaceableCells * 2) // �������[�v�h�~�̂��߂̈��S��
            {
                Debug.LogWarning("L-Shape expansion phase reached max iterations. Breaking early.");
                break; // ���[�v�������I�����܂��B
            }
        }

        if (MVinstance.todebug == ToDebug.GrowLShape)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }
        // �t�F�[�Y3: �M���b�v����
        Debug.Log("Phase 3: Filling Gaps");
        FillGaps();
        if (MVinstance.todebug == ToDebug.FillGaps || MVinstance.todebug == ToDebug.DetermineConnectivity)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }
        Debug.Log("Room expansion completed.");
        return true;
    }



    /// <summary>
    /// ��������`�Ɋg�����悤�Ƃ��܂��B
    /// �_���́uGrowRect�v�ɑ������A�ő�̋�`�̈�ւ̊g�������݂܂��B
    /// </summary>
    /// <returns>�������g�����ꂽ�ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool GrowRect(RoomDefinition room)
    {
        // �����̃o�E���f�B���O�{�b�N�X���擾
        RectInt currentBounds = room.Bounds;
        if (room.CurrentSize == 0) // �V�[�h���܂��z�u����Ă��Ȃ������͊g���ł��܂���
        {
            Debug.Log("false in growrect");
            return false;
        }

        // �g���\�ȕ����ƍő�̒����`�̈�������܂��B
        List<(RectInt newRect, int addedCells, ExpansionType type)> possibleExpansions = new List<(RectInt, int, ExpansionType)>();

        // ������ւ̊g��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Up] == false)
        {
            for (int h = 1; ; h++) // �V��������
            {
                if (currentBounds.yMax + h > _gridSize.y) break; // �O���b�h��Y�����̋��E�`�F�b�N.yMax��y+height��Ԃ�.

                bool canExpandRow = true;
                for (int x = currentBounds.xMin; x < currentBounds.xMax; x++)//x����x+width-1�܂ł̒l���Ƃ�.
                {
                    if (_grid[x, currentBounds.yMax + h - 1] != -1) // �����蓖�Ă̔z�u�\�Z���ł��邱��max��height��width�𑫂��Ƃ�-1.
                    {
                        canExpandRow = false;
                        break;
                    }
                }
                if (!canExpandRow) break;
                //�����ɓ��B�����Ȃ�A(x,x+width-1,y,y+height+h-1)�܂ł̃}�X��-1�ł���A������ɒǉ�����]�n�����݂���.
                possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.yMax, currentBounds.width, h), currentBounds.width * h, ExpansionType.Up));
                //��`���Ƒ�������ʐς����X�g�ɒǉ�.������h�ɂ��đ��݂�����.
            }
        }
        // �������ւ̊g��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Down] == false)
        {
            for (int h = 1; ; h++) // �V��������
            {
                if (currentBounds.yMin - h < 0) break; // �O���b�h��Y�����̋��E�`�F�b�N

                bool canExpandRow = true;
                for (int x = currentBounds.xMin; x < currentBounds.xMax; x++)
                {
                    if (_grid[x, currentBounds.yMin - h] != -1) // �����蓖�Ă̔z�u�\�Z���ł��邱��
                    {
                        canExpandRow = false;
                        break;
                    }
                }
                if (!canExpandRow) break;

                possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y - h, currentBounds.width, h), currentBounds.width * h, ExpansionType.Down));
            }
        }

        // �E�����ւ̊g��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Right] == false)
        {
            for (int w = 1; ; w++) // �V������
            {
                if (currentBounds.xMax + w > _gridSize.x) break; // �O���b�h��X�����̋��E�`�F�b�N

                bool canExpandCol = true;
                for (int y = currentBounds.yMin; y < currentBounds.yMax; y++)
                {
                    if (_grid[currentBounds.xMax + w - 1, y] != -1) // �����蓖�Ă̔z�u�\�Z���ł��邱��
                    {
                        canExpandCol = false;
                        break;
                    }
                }
                if (!canExpandCol) break;

                possibleExpansions.Add((new RectInt(currentBounds.xMax, currentBounds.y, w, currentBounds.height), currentBounds.height * w, ExpansionType.Right));
            }
        }
        // �������ւ̊g��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Left] == false)
        {
            for (int w = 1; ; w++) // �V������
            {
                if (currentBounds.xMin - w < 0) break; // �O���b�h��X�����̋��E�`�F�b�N

                bool canExpandCol = true;
                for (int y = currentBounds.yMin; y < currentBounds.yMax; y++)
                {
                    if (_grid[currentBounds.xMin - w, y] != -1) // �����蓖�Ă̔z�u�\�Z���ł��邱��
                    {
                        canExpandCol = false;
                        break;
                    }
                }
                if (!canExpandCol) break;

                possibleExpansions.Add((new RectInt(currentBounds.x - w, currentBounds.y, w, currentBounds.height), currentBounds.height * w, ExpansionType.Left));
            }
        }

        // �ł��傫���g���ł���@���I�����܂��B
        // ��������ꍇ�̓����_���ɑI�����邱�Ƃő��l�����m�ۂ��܂��B
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCells > 0) // �ǉ��Z����0���傫�����̂̂�
            .OrderByDescending(e => e.addedCells) // �ǉ��Z�����Ń\�[�g
            .ThenBy(e => _random.Next()) // �����ǉ��Z�����̏ꍇ�̓����_��
            .ToList();

        var totalSizeRatio = _roomDefinitions.Sum(r => r.SizeRatio);
        var maxSizeForRoom = _totalPlaceableCells * (room.SizeRatio / totalSizeRatio) * MaxSizeGrowRect; // // ���̕����̍ő勖�e�T�C�Y�����O�Ɍv�Z.

        // bestExpansions���X�g�͒ǉ��Z�����̑������Ƀ\�[�g����Ă��܂��B
        // ���̃��X�g��擪���珇�ɒT�����A�����𖞂����ŏ��̊g���Ă��̗p���܂��B
        // ���p�\�Ȋg����������m�F���܂��B
        foreach (var expansion in bestExpansions)
        {
            // ���̊g����K�p������̕����̃T�C�Y���A���O�Ɍv�Z�����ő勖�e�T�C�Y�𒴂��Ȃ������m�F����
            // ����: (���݂̕����̃T�C�Y + �g���ɂ��ǉ��Z����) <= �ő勖�e�T�C�Y
            if (room.CurrentSize + expansion.addedCells <= maxSizeForRoom)
            {
                // �����𖞂����ŏ��̊g���i= ���X�g�̕��я��ɂ��ł������I�Ȋg���j�����������̂ŁA�����K�p���܂��B
                var selectedExpansion = expansion; // �K�p����g�������̕ϐ��Ɋi�[���܂��B
                ApplyGrowth(room, selectedExpansion.newRect, selectedExpansion.addedCells); // ���������ۂɊg������֐����Ăяo���܂��B
                IsExpanded[room.ID - 1, (int)selectedExpansion.type] = true;
                Debug.Log($"Room {room.ID} expanded rectangularly to {selectedExpansion.newRect} adding {selectedExpansion.addedCells} cells. Current size: {room.CurrentSize},ExpansionType: {selectedExpansion.type}"); // �g�����ʂ����O�ɏo�͂��܂��B
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ������L���^�Ɋg�����悤�Ƃ��܂��B
    /// </summary>
    /// <param name="room">�g�����镔���̒�`�B</param>
    /// <returns>�������g�����ꂽ�ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool GrowLShape(RoomDefinition room)
    {
        // �����̃o�E���f�B���O�{�b�N�X���擾
        RectInt currentBounds = room.Bounds;
        if (room.CurrentSize == 0) // �V�[�h���܂��z�u����Ă��Ȃ������͊g���ł��܂���
        {
            Debug.Log("false in growrect");
            return false;
        }


        // �g�����.GrowRect�̂Ƃ͈Ⴄ.
        List<(RectInt? fullLineRect, RectInt? partialLineRect, int addedfullCells, int addedpartialCells, ExpansionType type, bool isLshaped)> possibleExpansions = new List<(RectInt?, RectInt?, int,int, ExpansionType, bool)>();
        (RectInt UpLine, RectInt DownLine, RectInt RightLine, RectInt LeftLine) EdgeLines = GetFullLines(room);
        RectInt? fullLineRect, partialLineRect;
        RectInt TemporaryLine;//�b��I�ȍő僉�C��.Lshape
        int h, w, hatLS, watLS, LeftLastx, RightLastx, DownLasty, UpLasty, Leftwidth, Rightwidth, Downheight, Upheight;
        bool LshapeMode, canExpandRow, canExpandCol, IsLeftLineExsits, IsRightLineExsits, IsDownLineExsits, IsUpLineExsits;
        float randomNum;

        // ��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Up] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; h = 1; hatLS = 0;
            TemporaryLine = EdgeLines.UpLine;
            while (currentBounds.yMax + h <= _gridSize.y)
            {
                canExpandRow = true;
                if (LshapeMode == false)
                {
                    for (int x = TemporaryLine.xMin; x < TemporaryLine.xMax; x++)//x����x+width-1�܂ł̒l���Ƃ�.
                    {
                        if (_grid[x, TemporaryLine.yMax + h - 1] != -1) // �����蓖�Ă̔z�u�\�Z���ł��邱��max��height��width�𑫂��Ƃ�-1.
                        {
                            canExpandRow = false;
                            break;
                        }
                    }
                    if (!canExpandRow)
                    {
                        LshapeMode = true;//LshapeMode�Ɉڍs.h�X�V����.
                        hatLS = h;//hatLs��LshapeMode���n�܂����������L�^����.���̍����͊��ɑS���C���g���ł͂Ȃ�.
                        continue;
                    }
                    else
                    {
                        fullLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMax, TemporaryLine.width, h);
                    }
                }
                else//L���g��
                {
                  if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;//����L���g������Ă�����I��.

                    if (h == hatLS)//�ŏ�.���s���ƕ����Œ�ł�2�ł���K�v������.�O���b�h�𐸍����ĕ����I����L���I�ȍő僉�C���̓���.TemporaryLine�̍X�V.
                    {
                        //���[�ƉE�[�ɂ��ꂼ��g���\(2*2�ȏ�)�̃G���A�����邩�����
                        for (LeftLastx = TemporaryLine.x; LeftLastx < TemporaryLine.xMax; LeftLastx++)
                        {
                            IsLeftLineExsits = true;
                            for (int y = TemporaryLine.yMax; y < TemporaryLine.yMax + 2; y++)
                            {
                                if (CheckGrid(LeftLastx,y,-1) == false) IsLeftLineExsits = false;
                            }
                            if (!IsLeftLineExsits) break; //���s����2�}�X�����݂��Ȃ�������A�����ŏI��.
                        }
                        for (RightLastx = TemporaryLine.xMax - 1; RightLastx >= TemporaryLine.x; RightLastx--)
                        {
                            IsRightLineExsits = true;
                            for (int y = TemporaryLine.yMax; y < TemporaryLine.yMax + 2; y++)
                            {
                                if (y > _gridSize.y)
                                {
                                    IsLeftLineExsits = false;
                                    break;
                                }
                                if (CheckGrid(RightLastx, y, -1) == false) IsRightLineExsits = false;
                            }
                            if (!IsRightLineExsits) break; //���s����2�}�X�����݂��Ȃ�������A�����ŏI��.
                        }
                        Leftwidth = LeftLastx - TemporaryLine.x;//LeftLastx�́A���߂ĉ��s����2�}�X�����݂��Ȃ��}�X�̍��W.width>=0
                        Rightwidth = (TemporaryLine.xMax - 1) - RightLastx;

                        if (Leftwidth < 2 && Rightwidth < 2) break;//�ǂ���̕���2�}�X��菬�����Ȃ�,L���g���ł���,���[�v���̂��I��
                        else
                        {
                            if (Leftwidth > Rightwidth)
                            {
                                TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                            }
                            else if (Leftwidth < Rightwidth)
                            {
                                TemporaryLine = new RectInt(RightLastx + 1, TemporaryLine.y, Rightwidth, 1);//RightLastx < TemoporaryLine.xmax-1�ɒ���.RightLastx��,L���g���o���Ȃ������ꏊ�ł��邱�Ƃɂ�����
                            }
                            else//Leftwidth == Rightwidth
                            {
                                randomNum = Random.Range(0f, 1f);
                                {
                                    if ( randomNum < 0.5f)//��
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                                    }
                                    else//�E
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
        // ��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Down] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; h = 1; hatLS = 0;
            TemporaryLine = EdgeLines.DownLine;
            while (currentBounds.yMin - h >= 0)
            {
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
                else//L���g��
                {
                   if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (h == hatLS)
                    {
                        //���[�ƉE�[�ɂ��ꂼ��g���\(2*2�ȏ�)�̃G���A�����邩�����
                        for (LeftLastx = TemporaryLine.x; LeftLastx < TemporaryLine.xMax; LeftLastx++)
                        {
                            IsLeftLineExsits = true;
                            for (int y = TemporaryLine.yMin - 1; y >= TemporaryLine.yMin - 2; y--)
                            {
                                if (CheckGrid(LeftLastx, y, -1) == false) IsLeftLineExsits = false;
                            }
                            if (!IsLeftLineExsits) break;
                        }
                        for (RightLastx = TemporaryLine.xMax - 1; RightLastx >= TemporaryLine.x; RightLastx--)
                        {
                            IsRightLineExsits = true;
                            for (int y = TemporaryLine.yMin - 1; y >= TemporaryLine.yMin - 2; y--)
                            {
                                if (CheckGrid(RightLastx, y, -1) == false) IsRightLineExsits = false;
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
                                    if (randomNum < 0.5f)//��
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, Leftwidth, 1);
                                    }
                                    else//�E
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
                        else partialLineRect = new RectInt(TemporaryLine.x, TemporaryLine.yMin - h, TemporaryLine.width, (h - hatLS + 1));
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Down, LshapeMode));
                h++;
            }
        }

        // �E
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Right] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; w = 1; watLS = 0;
            TemporaryLine = EdgeLines.RightLine;
            while (currentBounds.xMax + w <= _gridSize.x)
            {
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
                else//L���g��
                {
                    if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (w == watLS)
                    {
                        //��[�Ɖ��[�ɂ��ꂼ��g���\(2*2�ȏ�)�̃G���A�����邩�����
                        for (DownLasty = TemporaryLine.y; DownLasty < TemporaryLine.yMax; DownLasty++)
                        {
                            IsDownLineExsits = true;
                            for (int x = TemporaryLine.xMax; x < TemporaryLine.xMax + 2; x++)
                            {
                                if (CheckGrid(x, DownLasty, -1) == false) IsDownLineExsits = false;
                            }
                            if (!IsDownLineExsits) break;
                        }
                        for (UpLasty = TemporaryLine.yMax - 1; UpLasty >= TemporaryLine.y; UpLasty--)
                        {
                            IsUpLineExsits = true;
                            for (int x = TemporaryLine.xMax; x < TemporaryLine.xMax + 2; x++)
                            {
                                if (CheckGrid(x, UpLasty, -1) == false) IsUpLineExsits = false;
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
                                    if (randomNum < 0.5f)//��
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                                    }
                                    else//��
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

        // ��
        if (IsExpanded[room.ID - 1, (int)ExpansionType.Left] == false)
        {
            fullLineRect = null; partialLineRect = null; LshapeMode = false; w = 1; watLS = 0;
            TemporaryLine = EdgeLines.LeftLine;
            while (currentBounds.xMin - w >= 0)
            {
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
                else//L���g��
                {
                  if (IsExpanded[room.ID - 1, (int)ExpansionType.Lshape]) break;

                    if (w == watLS)
                    {
                        //��[�Ɖ��[�ɂ��ꂼ��g���\(2*2�ȏ�)�̃G���A�����邩�����
                        for (DownLasty = TemporaryLine.y; DownLasty < TemporaryLine.yMax; DownLasty++)
                        {
                            IsDownLineExsits = true;
                            for (int x = TemporaryLine.xMin - 1; x >= TemporaryLine.xMin - 2; x--)
                            {
                                if (CheckGrid(x, DownLasty, -1) == false) IsDownLineExsits = false;
                            }
                            if (!IsDownLineExsits) break;
                        }
                        for (UpLasty = TemporaryLine.yMax - 1; UpLasty >= TemporaryLine.y; UpLasty--)
                        {
                            IsUpLineExsits = true;
                            for (int x = TemporaryLine.xMin - 1; x >= TemporaryLine.xMin - 2; x--)
                            {
                                if (CheckGrid(x, UpLasty, -1) == false) IsUpLineExsits = false;
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
                                    if (randomNum < 0.5f)//��
                                    {
                                        TemporaryLine = new RectInt(TemporaryLine.x, TemporaryLine.y, 1, Downheight);
                                    }
                                    else//��
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
                        else partialLineRect = new RectInt(TemporaryLine.xMin - w, TemporaryLine.y, w - watLS + 1, TemporaryLine.height);
                    }
                }
                possibleExpansions.Add((fullLineRect, partialLineRect, CalculateRectArea(fullLineRect), CalculateRectArea(partialLineRect), ExpansionType.Left, LshapeMode));
                w++;
            }
        }

        // �ł��傫���g���ł���@���I�����܂��B
        // ��������ꍇ�̓����_���ɑI�����邱�Ƃő��l�����m�ۂ��܂��B
        var bestExpansions = possibleExpansions
            .Where(e => e.addedfullCells + e.addedpartialCells > 0) // �ǉ��Z����0���傫�����̂̂�
            .OrderByDescending(e => (e.addedfullCells + e.addedpartialCells)) // �ǉ��Z�����Ń\�[�g
            .ThenBy(e => _random.Next()) // �����ǉ��Z�����̏ꍇ�̓����_��
            .ToList();

        var totalSizeRatio = _roomDefinitions.Sum(r => r.SizeRatio);
        var maxSizeForRoom = _totalPlaceableCells * (room.SizeRatio / totalSizeRatio); // // ���̕����̍ő勖�e�T�C�Y�����O�Ɍv�Z.

        // bestExpansions���X�g�͒ǉ��Z�����̑������Ƀ\�[�g����Ă��܂��B
        // ���̃��X�g��擪���珇�ɒT�����A�����𖞂����ŏ��̊g���Ă��̗p���܂��B
        // ���p�\�Ȋg����������m�F���܂��B
        foreach (var expansion in bestExpansions)
        {
            // ���̊g����K�p������̕����̃T�C�Y���A���O�Ɍv�Z�����ő勖�e�T�C�Y�𒴂��Ȃ������m�F����
            // ����: (���݂̕����̃T�C�Y + �g���ɂ��ǉ��Z����) <= �ő勖�e�T�C�Y
            if (room.CurrentSize + expansion.addedfullCells + expansion.addedpartialCells <= maxSizeForRoom)
            {
                // �����𖞂����ŏ��̊g���i= ���X�g�̕��я��ɂ��ł������I�Ȋg���j�����������̂ŁA�����K�p���܂��B
                var selectedExpansion = expansion; // �K�p����g�������̕ϐ��Ɋi�[���܂��B
                ApplyGrowth(room, selectedExpansion.fullLineRect, selectedExpansion.addedfullCells); // ���������ۂɊg������֐����Ăяo���܂��B
                ApplyGrowth(room, selectedExpansion.partialLineRect, selectedExpansion.addedpartialCells);
                IsExpanded[room.ID - 1, (int)selectedExpansion.type] = true;
                if (selectedExpansion.isLshaped)
                {
                    IsExpanded[room.ID - 1, (int)ExpansionType.Lshape] = true;
                }
                
                Debug.Log($"Room {room.ID} expanded in GrowLshape  ,ExpansionType: {selectedExpansion.type},isLshaped: {selectedExpansion.isLshaped}"); 
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  �Ώۂ̗̈�(newRect)�̃Z���𕔉���ID�Ŗ��߁A�����̃T�C�Y�A�o�E���f�B���O�{�b�N�X���X�V
    /// </summary>
    /// <param name="room">�g�����镔���̒�`�B</param>
    /// <param name="newRect">�ǉ����ꂽ�V������`�̈�B</param>
    /// <param name="addedCells">�ǉ������Z���̐��B</param>
    private void ApplyGrowth(RoomDefinition room, RectInt? newRect, int addedCells)
    {
        if (newRect == null)
        {
            return;//�������Ȃ�.
        }
        // �V�����̈�̃Z���𕔉���ID�Ŗ��߂܂��B
        for (int x = newRect.Value.xMin; x < newRect.Value.xMax; x++)
        {
            for (int y = newRect.Value.yMin; y < newRect.Value.yMax; y++)
            {
                // �����̕����̃Z���ł͂Ȃ��A�������蓖�ẴZ���݂̂��X�V.�{���͂���K�v�Ȃ�����.
                if (_grid[x, y] == -1)
                {
                    _grid[x, y] = room.ID;
                }
            }
        }
        room.CurrentSize += addedCells; // �����̃T�C�Y���X�V
        RectInt CurrentRect = room.Bounds;
        room.Bounds = MergeRectInt(CurrentRect, newRect.Value); // �����̃o�E���f�B���O�{�b�N�X���X�V
    }

    /// <summary>
    /// �����̊g����Ɏc���������蓖�ẴZ���𖄂߂܂��B
    /// �_���́uFillGaps�v�ɑ������܂��B
    /// </summary>
    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps in the grid.");
        while (true)
        {
            // ���̃C�e���[�V�����ŕύX������������ǐՂ���t���O
            bool changedInIteration = false;
            // ���̃C�e���[�V�����ōs���X�V���ꎞ�I�ɕۑ����郊�X�g
            var updates = new List<(Vector2Int position, int roomId)>();

            // --- �v�Z�t�F�[�Y ---
            // �O���b�h�S�̂𑖍����A�ǂ̃Z�����ǂ̕���ID�ɍX�V���ׂ��������肷��
            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    // �����蓖�ẴZ��(-1)�݂̂�ΏۂƂ���
                    if (_grid[x, y] == -1)
                    {
                        // �אڂ��镔��ID�Ƃ��̏o���񐔂��J�E���g���邽�߂̎���
                        var neighborCounts = new Dictionary<int, int>();

                        // 8�����̗אڃZ�����`�F�b�N���܂��B
                        foreach (var offset in new[] {
                        Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left,
                        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                    })
                        {
                            Vector2Int neighborPos = new Vector2Int(x, y) + offset;

                            // �O���b�h�͈͓̔����`�F�b�N
                            if (neighborPos.x >= 0 && neighborPos.x < _gridSize.x &&
                                neighborPos.y >= 0 && neighborPos.y < _gridSize.y)
                            {
                                int neighborRoomId = _grid[neighborPos.x, neighborPos.y];
                                // �L���ȕ���ID(>0)�̏ꍇ�A�אڃJ�E���g�𑝂₷
                                if (neighborRoomId > 0)
                                {
                                    if (neighborCounts.ContainsKey(neighborRoomId))
                                    {
                                        neighborCounts[neighborRoomId]++;
                                    }
                                    else
                                    {
                                        neighborCounts[neighborRoomId] = 1;
                                    }
                                }
                            }
                        }

                        // �אڂ��镔����1�ȏ㌩�������ꍇ
                        if (neighborCounts.Count > 0)
                        {
                            // �ł������אڂ��Ă��镔��ID��������
                            int bestRoomId = neighborCounts.OrderByDescending(kv => kv.Value).First().Key;
                            // �X�V���X�g�ɁA���̃Z���̍X�V����ǉ�
                            updates.Add((new Vector2Int(x, y), bestRoomId));
                            changedInIteration = true;
                        }
                    }
                }
            }

            // ���̃C�e���[�V�����ŃO���b�h�ɑS���ύX���Ȃ������ꍇ�A�����͊����Ȃ̂Ń��[�v�𔲂���
            if (!changedInIteration)
            {
                break;
            }

            // --- �X�V�t�F�[�Y ---
            // �v�Z�t�F�[�Y�Ō��肵���S�Ă̍X�V���O���b�h�Ɉꊇ�œK�p����
            foreach (var update in updates)
            {
                ApplyGrowth(_roomDefinitions[update.roomId - 1], new RectInt(update.position.x, update.position.y, 1, 1), 1);
            }
        }

        // ���[�v�I����A����ł��c���Ă��関���蓖�ăZ������������
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    // �ǂ̕����ɂ��אڂ��Ȃ��Ǘ����������蓖�ăZ��
                    Debug.LogWarning($"Isolated unassigned cell at ({x},{y}). Setting to 0 (unusable).");
                    _grid[x, y] = 0; // �ǂ̕����ɂ����蓖�Ă��Ȃ��ꍇ�́A�g�p�s�Ƃ��ă}�[�N���܂��B
                }
            }
        }

        Debug.Log("Gap filling completed.");
    }

    //�����̊e�[�Ɉʒu����ӂ̏����擾����.GrowLshape�����s����鎞�_�ł́A�ӂ͕K���e�����ɂ����.
    public (RectInt UpLine, RectInt DownLine, RectInt RightLine, RectInt LeftLine) GetFullLines(RoomDefinition room)
    {
        int roomId = room.ID;

        RectInt upLine = new RectInt();
        RectInt downLine = new RectInt();
        RectInt rightLine = new RectInt();
        RectInt leftLine = new RectInt();

        // ���: ���ۂ̕����̍ŏ㕔�̃Z�����܂ދ�`
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


        // ����: ���ۂ̕����̍ŉ����̃Z�����܂ދ�`
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


        // ����: ���ۂ̕����̍ł����̃Z�����܂ދ�`
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


        // �E��: ���ۂ̕����̍ł��E�̃Z�����܂ދ�`
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
    //rectint�̖ʐς��v�Z,null�Ή�.
    public int CalculateRectArea(RectInt? rect)
    {
        if (rect.HasValue)
        {
            return rect.Value.width * rect.Value.height;
        }
        else return 0;
    }
}