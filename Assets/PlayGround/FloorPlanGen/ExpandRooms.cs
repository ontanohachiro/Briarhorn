using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
//�X�e�b�v2: �������g�����郁�C���̃R���[�`��.
public partial class FloorPlanGenerator : MonoBehaviour
{
    public float MaxSizeGrowRect = 0.5f;//GrowRect�Ŋg������镔���̌��E.0.5�Ȃ�A�S�Ă̕������g������Ă������̔����̃T�C�Y�ƂȂ�.
   
    private enum ExpansionType  {   Up, Down, Right, Left, Lshape }
    private bool[,] IsExpanded;//�ŏ��͐ݒ肳��Ă��Ȃ�.GrowRect,GrowShape�̒��O�ł��ꂼ�ꏉ����.
    private void SetIsExpanded (int roomNum)
    {
        IsExpanded = new bool[roomNum,5];//5��Up, Down, Right, Left, Lshape,������Lshape��GrowRect�ł͎g���Ȃ�.
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
        List<RoomDefinition>  _roomsToExpandP1 = _roomDefinitions.ToList();
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
            matrixToDebug = ConvertMatrix(_grid,"float");
        }

        // �t�F�[�Y2: L���^�g��
        SetIsExpanded(_roomDefinitions.Count);
        Debug.Log("Phase 2: L-Shape Expansion");
        iterationCount = 0; // �C�e���[�V�����J�E���g�����Z�b�g���܂��B
        while (_roomsToExpandP2.Any()) //_roomsToExpand�ɕ������������.
        {

            var  room = _roomsToExpandP2[SelectRoom(_roomsToExpandP2)];
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


        // �t�F�[�Y3: �M���b�v����
        Debug.Log("Phase 3: Filling Gaps");
        FillGaps();

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
                IsExpanded[room.ID-1, (int)selectedExpansion.type] = true;
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
       

        // �g�����.
        List<(RectInt fullLineRect ,RectInt partialLineRect, int addedCells, ExpansionType type, bool isLshaped)> possibleExpansions = new List<(RectInt, RectInt, int, ExpansionType, bool)>();

        // ��
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
    ///  �Ώۂ̗̈�(newRect)�̃Z���𕔉���ID�Ŗ��߁A�����̃T�C�Y�A�o�E���f�B���O�{�b�N�X���X�V
    /// </summary>
    /// <param name="room">�g�����镔���̒�`�B</param>
    /// <param name="newRect">�ǉ����ꂽ�V������`�̈�B</param>
    /// <param name="addedCells">�ǉ������Z���̐��B</param>
    private void ApplyGrowth(RoomDefinition room, RectInt newRect, int addedCells)
    {
        // �V�����̈�̃Z���𕔉���ID�Ŗ��߂܂��B
        for (int x = newRect.xMin; x < newRect.xMax; x++)
        {
            for (int y = newRect.yMin; y < newRect.yMax; y++)
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
        room.Bounds = MergeRectInt(CurrentRect,newRect); // �����̃o�E���f�B���O�{�b�N�X���X�V
    }

    /// <summary>
    /// �����̊g����Ɏc���������蓖�ẴZ���𖄂߂܂��B
    /// �_���́uFillGaps�v�ɑ������܂��B
    /// </summary>
    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps in the grid.");
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // �����蓖�ẴZ���ł���ꍇ
                {
                    // �אڂ��镔���������A�ł������̗אڃZ�����������Ɋ��蓖�Ă܂��B
                    int bestRoomId = -1;
                    int maxAdjacentCount = 0;

                    // 8�����̗אڃZ�����`�F�b�N���܂��B
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
                            if (neighborRoomId > 0) // �L���ȕ���ID�̏ꍇ
                            {
                                // ���̕���ID�̗אڐ����J�E���g���܂��B
                                // ���̊ȗ������ꂽ�����ł́A�P�ɍŏ��ɑ������������Ɋ��蓖�Ă܂����A
                                // ��茘�S�Ȏ����ł́A�e�אڕ����̗אڃZ���̍��v���v�Z���A�ő�̂��̂�I�т܂��B
                                // �����ł́A�ł��߂��ɂ���L���ȕ���ID�Ɋ��蓖�Ă܂��B
                                if (maxAdjacentCount == 0) // �ŏ��̗L���ȗאڕ���
                                {
                                    bestRoomId = neighborRoomId;
                                    maxAdjacentCount = 1; // ���Ȃ��Ƃ�1�̗אڃZ������������
                                }
                                // �����ŁA����ɗאڃZ��������������D�悷�郍�W�b�N��ǉ��ł��܂��B
                                // ��: float currentRoomAdjacentCount = GetAdjacentCellsCount(neighborRoomId, new Vector2Int(x, y));
                                // if (currentRoomAdjacentCount > maxAdjacentCount) { ... }
                            }
                        }
                    }

                    if (bestRoomId > 0)
                    {
                        _grid[x, y] = bestRoomId;
                        _roomDefinitions[bestRoomId].CurrentSize++; // �����̃T�C�Y���X�V
                        // _roomDefinitions[bestRoomId].Bounds ���X�V���邱�Ƃ��������Ă��������B
                        // �������AFillGaps�̌�ňꊇ����CalculateRoomBounds���Ăԕ��������I��������܂���B
                    }
                    else
                    {
                        // �ǂ̕����ɂ��אڂ��Ȃ��Ǘ����������蓖�ăZ��
                        Debug.LogWarning($"Isolated unassigned cell at ({x},{y}). Setting to 0 (unusable).");
                        _grid[x, y] = 0; // �ǂ̕����ɂ����蓖�Ă��Ȃ��ꍇ�́A�g�p�s�Ƃ��ă}�[�N���܂��B
                    }
                }
            }
        }
        Debug.Log("Gap filling completed.");
    }


    /// <summary>
    /// �����V�[�h�z�u��ɗאڐ��񂪖�������Ă��邱�Ƃ��m�F���܂��B
    /// �_���ł́A���񂪖�������Ȃ��ꍇ�ɐ����v���Z�X�����Z�b�g����\�������y����Ă��܂��B
    /// </summary>
    /// <returns>�S�Ă̗אڐ��񂪖�������Ă���ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool VerifyAdjacencyConstraints()
    {
        Debug.Log("Verifying adjacency constraints...");
        bool allConstraintsMet = true;

        foreach (var edge in settings.ConnectivityGraph.Edges) // �ڑ��O���t�̊e�Ӂi�אڐ���j���`�F�b�N
        {
            RoomDefinition roomA = _roomDefinitions[edge.Source]; // �ӂ̎n�_�ɑΉ����镔��
            RoomDefinition roomB = _roomDefinitions[edge.Target]; // �ӂ̏I�_�ɑΉ����镔��

            if (!roomA.InitialSeedPosition.HasValue || !roomB.InitialSeedPosition.HasValue)
            {
                // �V�[�h���z�u����Ă��Ȃ������ɂ��ẮA�����ł̓`�F�b�N�ł��܂���B
                // �����PlaceInitialSeeds()�̐ӔC�ł��B
                continue;
            }

            // 2�̕������אڂ��Ă��邩���m�F���܂��B
            // �����ł͊ȈՓI�ɁA�������̃o�E���f�B���O�{�b�N�X���d�Ȃ邩�A�܂��͔��ɋ߂������`�F�b�N���܂��B
            // ��茵���ɂ́A�������̃Z�������ڗאڂ��Ă��邩���m�F����K�v������܂��B
            bool areAdjacent = AreRoomsDirectlyAdjacent(roomA.ID, roomB.ID);

            if (!areAdjacent)
            {
                Debug.LogWarning($"Adjacency constraint between Room {roomA.ID} ({roomA.Type}) and Room {roomB.ID} ({roomB.Type}) NOT met after initial placement. Their cells are not adjacent.");
                allConstraintsMet = false;
                // �����ŁA��������Ȃ���������Ɋ�Â��āA�����̔z�u�𒲐�����Ȃǂ̃��W�b�N��ǉ��ł��܂��B
                // �܂��́A�P���ɂ��̎��s�����s�Ƃ��ă}�[�N���܂��B
            }
            else
            {
                Debug.Log($"Adjacency constraint between Room {roomA.ID} ({roomA.Type}) and Room {roomB.ID} ({roomB.Type}) MET.");
            }
        }
        return allConstraintsMet;
    }

    /// <summary>
    /// �w�肳�ꂽ2�̕������O���b�h��Œ��ڗאڂ��Ă��邩�𔻒f����⏕�֐��B
    /// </summary>
    /// <param name="roomID1">����1��ID�B</param>
    /// <param name="roomID2">����2��ID�B</param>
    /// <returns>���������ڗאڂ��Ă���ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool AreRoomsDirectlyAdjacent(int roomID1, int roomID2)
    {
        // ����1�̑S�ẴZ���𑖍�
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1)
                {
                    // ���̃Z���̗אڃZ�����`�F�b�N
                    foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left })
                    {
                        Vector2Int neighborPos = new Vector2Int(x, y) + offset;

                        if (neighborPos.x >= 0 && neighborPos.x < _gridSize.x &&
                            neighborPos.y >= 0 && neighborPos.y < _gridSize.y)
                        {
                            if (_grid[neighborPos.x, neighborPos.y] == roomID2)
                            {
                                return true; // �אڂ���Z������������
                            }
                        }
                    }
                }
            }
        }
        return false;
    }


    /// <summary>
    /// �O���b�h��̎w�肳�ꂽ�Z���ɗאڂ��镔����ID�̃��X�g��Ԃ��܂��B
    /// </summary>
    /// <param name="x">�Z����X���W�B</param>
    /// <param name="y">�Z����Y���W�B</param>
    /// <returns>�אڂ��镔����ID��HashSet�i�d���Ȃ��j�B</returns>
    private HashSet<int> GetAdjacentRooms(int x, int y)
    {
        HashSet<int> adjacentRoomIds = new HashSet<int>();

        // 8�����̗אڃZ�����`�F�b�N
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
                if (neighborRoomId > 0) // �L���ȕ���ID�̏ꍇ
                {
                    adjacentRoomIds.Add(neighborRoomId);
                }
            }
        }
        return adjacentRoomIds;
    }
    
    //�����̊e�[�Ɉʒu����ӂ̏��wo���擾����.GrowLshape�����s����鎞�_�ł́A�ӂ͕K���e�����ɂ����.
    public (RectInt UpLine, RectInt DownLine, RectInt RightLine, RectInt LeftLine) GetFullLines(RectInt RoomBounds)
    {

    }

}