using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
//�X�e�b�v2: �������g�����郁�C���̃R���[�`��.
public partial class FloorPlanGenerator : MonoBehaviour
{
    public float MaxSizeGrowRect = 0.5f;//GrowRect�Ŋg������镔���̌��E.0.5�Ȃ�A�S�Ă̕������g������Ă������̔����̃T�C�Y�ƂȂ�.
    // �ǉ�����v���C�x�[�g�ϐ�
    private List<RoomDefinition> _roomsToExpand; // �g���Ώۂ̕����̃��X�g
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
    /// �����̊g���v���Z�X���J�n����O�ɁA�K�v�ȕϐ����������܂��̓��Z�b�g.
    /// 2-0.���̎��_�ŁA_grid�ɂ́A0(�ǁA�z�u�s��),-1(���z�u),ID(>0,ID�ɑΉ�����e�����̃V�[�h�ʒu.�e�l�ɂ���������݂��Ȃ�)������.
    /// �e������CurrentSize�͂��傤��1.
    /// </summary>
    private void InitializeRoomExpansion()
    {
        _roomsToExpand = new List<RoomDefinition>();

        // �S�Ă̕�����`���g�����X�g�ɒǉ����A���v�T�C�Y�䗦���v�Z.
        foreach (var room in _roomDefinitions)
        {
            room.Bounds = new RectInt(0, 0, 0, 0); // �o�E���f�B���O�{�b�N�X�����Z�b�g
            _roomsToExpand.Add(room);
        }
        Debug.Log("Room expansion initialized.");
    }

    /// <summary>
    /// �X�e�b�v2: �������g�����郁�C���̃R���[�`��.
    /// ��������`�Ɋg�����A���̌�L���^�Ɋg�����A�Ō�Ɏc��̃M���b�v�𖄂߂܂��B
    /// </summary>
    private bool ExpandRooms()
    {
        Debug.Log("Starting room expansion...");
        InitializeRoomExpansion();// �t�F�[�Y0.
        List<RoomDefinition>  _roomsToExpandP1 = _roomsToExpand.ToList();
        List<RoomDefinition> _roomsToExpandP2 = _roomsToExpand.ToList();
        // �t�F�[�Y1: ��`�g��
        Debug.Log("Phase 1: Rectangular Expansion");
        int iterationCount = 0;
        while (_roomsToExpandP1.Any()) // _roomsToExpand�ɕ������������.
        {
            // ���̃C�e���[�V�����ŏ������镔�����m���I�ɑI�����A�g�������݂܂��B

            var room = _roomsToExpandP1[SelectRoom(_roomsToExpandP1)];
            if (GrowRect(room)) // ��������`�Ɋg���ł��邩���݂܂��B
                {
                     // �������g���ł����ꍇ�A�������� _roomsToExpand �Ɏc���Ă���
                }
                else // �g���ł��Ȃ������ꍇ (canGrow -- "������" --> removeRoom)
                {
                    // �g���ł��Ȃ��Ȃ�����������⃊�X�g���珜�O���܂��B
                    // _roomsToExpand ���璼�ڍ폜���܂��B
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
                    // _roomsToExpand ���璼�ڍ폜���܂��B
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
        // �����̃o�E���f�B���O�{�b�N�X���擾�i����Ăяo�����̓V�[�h�ʒu����v�Z�j
        RectInt currentBounds = room.Bounds;//rectint:x,y,width,height.���̂Ƃ��S��0
        if (room.CurrentSize == 1 && !room.InitialSeedPosition.HasValue)
        {
            Debug.LogError($"Room {room.ID} has CurrentSize 1 but no InitialSeedPosition.");
            return false;
        }
        if (room.CurrentSize == 1 && room.InitialSeedPosition.HasValue && room.Bounds.width == 0) // ����g����
        {
            currentBounds = new RectInt(room.InitialSeedPosition.Value.x, room.InitialSeedPosition.Value.y, 1, 1);
            room.Bounds = currentBounds;
        }
        else if (room.CurrentSize == 0) // �V�[�h���܂��z�u����Ă��Ȃ������͊g���ł��܂���
        {
            Debug.Log("false in growrect");
            return false;
        }

        // �g���\�ȕ����ƍő�̒����`�̈�������܂��B
        List<(RectInt newRect, int addedCells)> possibleExpansions = new List<(RectInt, int)>();

        // ������ւ̊g��
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
            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y, currentBounds.width, currentBounds.height + h), currentBounds.width * h));
            //��`���Ƒ�������ʐς����X�g�ɒǉ�.������h�ɂ��đ��݂�����.
        }

        // �������ւ̊g��
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

            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y - h, currentBounds.width, currentBounds.height + h), currentBounds.width * h));
        }


        // �E�����ւ̊g��
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

            possibleExpansions.Add((new RectInt(currentBounds.x, currentBounds.y, currentBounds.width + w, currentBounds.height), currentBounds.height * w));
        }

        // �������ւ̊g��
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

            possibleExpansions.Add((new RectInt(currentBounds.x - w, currentBounds.y, currentBounds.width + w, currentBounds.height), currentBounds.height * w));
        }


        // �ł��傫���g���ł���@���I�����܂��B
        // ��������ꍇ�̓����_���ɑI�����邱�Ƃő��l�����m�ۂ��܂��B
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCells > 0) // �ǉ��Z����0���傫�����̂̂�
            .OrderByDescending(e => e.addedCells) // �ǉ��Z�����Ń\�[�g
            .ThenBy(e => _random.Next()) // �����ǉ��Z�����̏ꍇ�̓����_��
            .ToList();

        var totalSizeRatio = _roomsToExpand.Sum(r => r.SizeRatio);
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
                Debug.Log($"Room {room.ID} expanded rectangularly to {selectedExpansion.newRect} adding {selectedExpansion.addedCells} cells. Current size: {room.CurrentSize}"); // �g�����ʂ����O�ɏo�͂��܂��B
                return true; // �g���ɐ����������߁Atrue��Ԃ��ď������I�����܂��B
            }
        }

        return false;
    }

    /// <summary>
    /// ������L���^�Ɋg�����悤�Ƃ��܂��B
    /// �_���́uGrowLShape�v�ɑ������A���`�̊g���������܂��B
    /// </summary>
    /// <param name="room">�g�����镔���̒�`�B</param>
    /// <returns>�������g�����ꂽ�ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool GrowLShape(RoomDefinition room)
    {
        // ���݂̕����̋��E�Ɋ�Â��āA�g���\��L���^�̈�������܂��B
        // �����GrowRect�������G�ŁA�����̕����̗אڃZ������V������`�̈��T�����ƂɂȂ�܂��B
        // ���ۂ̎����ł́A�����̋��E���\�����邷�ׂẴZ���𒲂ׁA���ꂼ��̗אڂ����̃Z�������`�g�������݂邱�ƂŎ����ł��܂��B

        // �ȗ����̂��߁A�����ł͕����̊����̊e�Z���ɗאڂ��関���蓖�ẴZ����T���A
        // ��������\�Ȍ���ő��1xN�܂���Nx1�̒����I�Ȋg�������݂܂��B

        List<(List<Vector2Int> newCells, int addedCount)> possibleExpansions = new List<(List<Vector2Int>, int)>();

        // ��������߂邷�ׂẴZ����T�����A���̗אڃZ�����`�F�b�N���܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == room.ID) // �����̃Z���ł���ꍇ
                {
                    // ���̃Z������l���ɗאڂ��関���蓖�ăZ����T���܂��B
                    Vector2Int currentCell = new Vector2Int(x, y);

                    // �e���� (�㉺���E) �ɒ����I�Ɋg�������݂܂��B
                    foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left })
                    {
                        List<Vector2Int> newSegment = new List<Vector2Int>();
                        Vector2Int testPos = currentCell + offset;
                        int count = 0;

                        while (testPos.x >= 0 && testPos.x < _gridSize.x &&
                               testPos.y >= 0 && testPos.y < _gridSize.y &&
                               _grid[testPos.x, testPos.y] == -1) // �����蓖�Ă̔z�u�\�Z��
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

        // �ł��傫���g���ł���@���I�����܂��B
        var bestExpansions = possibleExpansions
            .Where(e => e.addedCount > 0)
            .OrderByDescending(e => e.addedCount)
            .ThenBy(e => _random.Next())
            .ToList();

        if (bestExpansions.Any())
        {
            // �ł��ǉ��Z���������g����K�p���܂��B
            var selectedExpansion = bestExpansions.First();
            foreach (var cell in selectedExpansion.newCells)
            {
                _grid[cell.x, cell.y] = room.ID;
                room.CurrentSize++;
            }
            // ������Bounds���X�V����K�v������܂��B
            UpdateRoomBounds(room);
            Debug.Log($"Room {room.ID} expanded L-shapely, adding {selectedExpansion.addedCount} cells. Current size: {room.CurrentSize}");
            return true;
        }

        return false;
    }


    /// <summary>
    /// �����̋��E�iBounds�j���X�V����⏕�֐��B
    /// </summary>
    /// <param name="room">�X�V���镔���̒�`�B</param>
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
            room.Bounds = new RectInt(0, 0, 0, 0); // �Z����������Ȃ��ꍇ�͖����ȋ��E��ݒ�
        }
    }


    /// <summary>
    ///  �Ώۂ̗̈�̃Z���𕔉���ID�Ŗ��߁A�����̃T�C�Y�A�o�E���f�B���O�{�b�N�X���X�V
    /// </summary>
    /// <param name="room">�g�����镔���̒�`�B</param>
    /// <param name="newRect">�V������`�̈�B</param>
    /// <param name="addedCells">�ǉ������Z���̐��B</param>
    private void ApplyGrowth(RoomDefinition room, RectInt newRect, int addedCells)
    {
        // �V�����̈�̃Z���𕔉���ID�Ŗ��߂܂��B
        for (int x = newRect.xMin; x < newRect.xMax; x++)
        {
            for (int y = newRect.yMin; y < newRect.yMax; y++)
            {
                // �����̕����̃Z���ł͂Ȃ��A�������蓖�ẴZ���݂̂��X�V���܂��B
                if (_grid[x, y] == -1)
                {
                    _grid[x, y] = room.ID;
                }
            }
        }
        room.CurrentSize += addedCells; // �����̃T�C�Y���X�V
        room.Bounds = newRect; // �����̃o�E���f�B���O�{�b�N�X���X�V
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

}