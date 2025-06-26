using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �X�e�b�v1: �����̏����V�[�h�ʒu��z�u.
    /// </summary>
    private bool PlaceInitialSeeds() 
    {
        Debug.Log("Placing initial seeds...");
        // �e������ID���L�[�Ƃ����d�݃}�b�v�̎��������������܂��B
        Dictionary<int, float[,]> weightGrids = InitializeWeightGrids();
        // �ǂ܂ł̋��������O�Ɍv�Z���܂��B����͑S�����ʂ̏d�݌v�Z�Ɏg�p����܂��B
        float[,] _distanceToWall = CalculateDistanceToWall();

        if (MVinstance.todebug == ToDebug.CalculateDistanceToWall)
        {
            matrixToDebug = _distanceToWall;
        }

        // �e�����̃V�[�h��z�u���܂��B
        foreach (var room in _roomDefinitions)
        {
            // 1. ���̕����iroom�j�p�̏d�݃O���b�h���v�Z���܂��B
            // CalculateWeightsForRoom���\�b�h�́A�w�肳�ꂽroom.ID�Ɋ�Â��āA�Ή�����weightGrids���̏d�݃}�b�v���X�V���܂��B
            CalculateWeightsForRoom(room.ID, weightGrids, _distanceToWall);
        }
        foreach (var room in _roomDefinitions.OrderBy(r => r.ID)) // RoomDefinition��ID�v���p�e�B�Ń\�[�g���ď������܂��B
        {
            // 2. �v�Z���ꂽ�d�݃}�b�v�iweightGrids[room.ID]�j�Ɋ�Â��āA�œK�ȃV�[�h�ʒu��I�����܂��B
            Vector2Int? seedPos = SelectBestSeedPosition(weightGrids[room.ID]);

            if (seedPos.HasValue)
            {
                room.InitialSeedPosition = seedPos.Value; // �I�����ꂽ�ʒu�𕔉��̏����V�[�h�ʒu�Ƃ��ċL�^���܂��B
                _grid[seedPos.Value.x, seedPos.Value.y] = room.ID; // �O���b�h��̑Ή�����Z���ɕ�����ID�����蓖�Ă܂��B
                room.CurrentSize = 1; // �V�[�h�Z�����z�u���ꂽ�̂ŁA�����̌��݂̃T�C�Y��1�Ƃ��܂��B
                Debug.Log($"Placed seed for room {room.ID} ({room.Type}) at {seedPos.Value}");
                if (MVinstance.todebug == ToDebug.SelectBestSeedPosition)
                {
                    //�O�A����������������ĂȂ����Ƃ�������.
                    matrixToDebug[seedPos.Value.x, seedPos.Value.y] = (float)(room.ID);
                }
                // 3. �z�u�����V�[�h�Z���̎��͂̏d�݂��A���̕����̏d�݃O���b�h�ŉ����܂��B
                // UpdateWeightsAroundSeed���\�b�h�́A�z�u���ꂽ�����iroom.ID�j�ȊO�̑S�Ă̕����̏d�݃}�b�v���X�V���܂��B
                UpdateWeightsAroundSeed(weightGrids, seedPos.Value, room.ID);
            }
            else
            {
                Debug.LogError($"Could not find suitable seed position for room {room.ID} ({room.Type}). Not enough space or constraints too strict?");
                // �z�u���s�B���Z�b�g���Ă�蒼�����A���̎��s�����s�Ƃ��܂��B
                ResetGridAndSeeds(); // �O���b�h�ƃV�[�h�������Z�b�g���܂��B
                return false; // �V�[�h�z�u���s�̂���false��Ԃ��܂��B
            }
        }
        ExpandTo22(_distanceToWall);
        if (MVinstance.todebug == ToDebug.ExpandTo22)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }
        Debug.Log("Initial seeds placed successfully.");
        return true; // �S�ẴV�[�h������ɔz�u���ꂽ����true��Ԃ��܂��B
    }


    /// <summary>
    /// �S�Ă̕���ID�ɑ΂���d�݃O���b�h������������֐��B
    /// �e������ID���L�[�Ƃ��A�l�Ƃ��ĐV����float�^��2�����z��i�d�݃}�b�v�j�����������쐬���܂��B1-0
    /// </summary>
    private Dictionary<int, float[,]> InitializeWeightGrids()
    {
        // ����ID���L�[�A�d�݃}�b�v��l�Ƃ��鎫�������������܂��B
        Dictionary<int, float[,]> weightGrids = new Dictionary<int, float[,]>();
        // _roomDefinitions�Ɋi�[����Ă���S�Ă̕�����`�ɑ΂��ď������s���܂��B
        foreach (var room in _roomDefinitions) // RoomDefinition��ID���L�[�Ƃ��Ďg�p���܂��B
        {
            // �V�����d�݃}�b�v�ifloat�^��2�����z��j���쐬���A�����ɒǉ����܂��B
            // �O���b�h�T�C�Y��_gridSize.x �� _gridSize.y�Ɋ�Â��܂��B
            weightGrids[room.ID] = new float[_gridSize.x, _gridSize.y];
            // �����l�� 0 �ł��ǂ��ł����A���CalculateWeightsForRoom�ŏ㏑������܂��B
        }
        return weightGrids; // ���������ꂽ�d�݃O���b�h�̎�����Ԃ��܂��B
    }

    /// <summary>
    /// ����̕���ID�ɂ��Ă̏d�݃O���b�h���v�Z����֐��B1-1
    /// </summary>
    /// <param name="targetRoomId">�d�݂��v�Z����Ώۂ̕�����ID�B</param>
    /// <param name="weightGrids">�S���̏d�݃O���b�h���i�[���ꂽ�����B</param>
    /// <param name="distanceToWall">�e�Z������ł��߂��ǂ܂ł̋������i�[����2�����z��B</param>
    private void CalculateWeightsForRoom(int targetRoomId, Dictionary<int, float[,]> weightGrids, float[,] distanceToWall)
    {
        // �Ώۂ̕����̏d�݃}�b�v�ւ̎Q�Ƃ��擾���܂��B
        float[,] weights = weightGrids[targetRoomId];
        // �Ώۂ̕����̒�`�����擾���܂��BID>=1���Aid�P�̕��������X�g��0�v�f.����.
        RoomDefinition targetRoom = _roomDefinitions[targetRoomId-1];

        // �ڕW�ʐς���u���z�I�ȁv�ǂ���̋����𐄒肵�܂��B
        // �S�z�u�\�Z�����ƕ����̃T�C�Y�䗦����A���̕�������߂�ׂ������悻�̃Z�������v�Z���܂��B
        float estimatedTargetCells = _totalPlaceableCells * (targetRoom.SizeRatio / _roomDefinitions.Sum(r => r.SizeRatio));
        // 0���Z��h�����߁AestimatedTargetCells��0�ȉ��̏ꍇ��1�Ƃ��܂��B
        if (estimatedTargetCells <= 0) estimatedTargetCells = 1;
        // ���z�I�ȕǂ���̋������A����Z�����̕������ɌW�����悶�ċ��߂܂��B����͕����̈�ӂ̂����悻�̒����̔������x���Ӑ}���Ă��܂��B
        float idealDistanceFromWall = Mathf.Sqrt(estimatedTargetCells) * settings.SeedPlacementWeightDistanceFactor;

        // �O���b�h�̑S�Z���𑖍����܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // 0. �z�u�s�\�ȏꏊ�i_grid[x,y]��0�j��A���ɑ��̃V�[�h���u���ꂽ�ꏊ�i_grid[x,y]�����̒l�j�͏d��0�Ƃ��܂��B
                if (_grid[x, y] != -1) // -1�͖����蓖�Ă̔z�u�\�Z���������܂��B
                {
                    weights[x, y] = 0f;
                    continue; // ���̃Z���ցB
                }
                //����������_grid[x, y] == -1�i�����蓖�Ă̔z�u�\�Z���j�̂ݓ��B�\�ł��B

                // 1. �����d�ݐݒ� (�z�u�\�ȓ����Z���͊�{�d��1.0)
                float currentWeight = 1.0f;

                // 2. �O�ǂ���̋����Ɋ�Â��ďd�݂𒲐����܂��B
                float dist = distanceToWall[x, y]; // ���O�Ɍv�Z���ꂽ�ǂ܂ł̋����B
                if (dist >= 0) // �����Z���̏ꍇ�i�ǂ܂ł̋������v�Z�ł��Ă���j�B
                {
                    // �ǂɋ߂��قǏd�݂����炷���A���z�����Ńs�[�N�ɂȂ�悤�ɒ������܂��B
                    // ��: ���`���� (�Ǎۂ�0�AidealDistanceFromWall�ȏ��1�ɂȂ�悤�ɃN�����v)�B
                    currentWeight += Mathf.Clamp01(dist / idealDistanceFromWall);
                    // ��: �K�E�V�A���I�ȕ��z (idealDistance�ōő�l1�ƂȂ�悤��)�B
                    // currentWeight *= Mathf.Exp(-(dist - idealDistanceFromWall) * (dist - idealDistanceFromWall));
                }
                else // �ǂ��瓞�B�s�\�A�܂��̓O���b�h�O�����̏ꍇ�B
                {
                    currentWeight = 0f; // �O�̂��߁iInitializeGrid��0�ɂȂ��Ă���͂��ł����A�����ł�0�ɂ��܂��j�B
                }

                weights[x, y] = Mathf.Max(0, currentWeight);
            }
        }
    }


    /// <summary>
    /// �e�����Z������ł��߂��O���Z��(��/���E)�܂ł̋������v�Z���� (BFS�x�[�X)
    /// �O���Z���ⓞ�B�s�\�Z���� -1 ��Ԃ�,1-1-1
    /// </summary>
    public float[,] CalculateDistanceToWall()
    {
        float[,] distances = new float[_gridSize.x, _gridSize.y];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();//������o��.
        //BFS:(1)�J�n���_���L���[�ɓ����.(2)�L���[�̐擪�ɂ��钸�_�ɗאڂ��Ă��ĒT���ς݂łȂ����_���A�L���[�̐擪�̒��_�̂��̂�1(�������̓����N�Ɉˑ���������)�������������蓖�āA�L���[�ɓ����.
        //(3) (2)���J��Ԃ�.
        // ������: �����Z���� MaxValue�A�O���Z���� 0
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (settings.InputFootprintGrid[x, y] == 0) // �O���Z��(��)
                {
                    distances[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
                else
                {
                    distances[x, y] = float.MaxValue;
                }
            }
        }

        //�΂߂��l������
        Vector2Int[] neighbors = {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                                                    };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            float currentDist = distances[current.x, current.y];

            foreach (var offset in neighbors)
            {
                Vector2Int neighbor = current + offset;

                if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                {
                    // �΂߂��l������ꍇ�A�����𒲐�
                    float newDist = currentDist + (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > 1 ? 1.414f : 1.0f); // �΂߂͖�1.4�{.�񍀉��Z�q,true�Ȃ獶�Afalse�Ȃ�E.
                    //float newDist = currentDist + 1.0f; // 4�����݂̂̏ꍇ

                    if (distances[neighbor.x, neighbor.y] > newDist)
                    {
                        distances[neighbor.x, neighbor.y] = newDist;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // MaxValue�̂܂܂̃Z���i���������ǂ��瓞�B�s�\�A�܂��̓O���b�h�O�����j�� -1 �ɂ���
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (distances[x, y] == float.MaxValue)
                {
                    distances[x, y] = -1f;
                }
            }
        }


        return distances;
    }


    /// <summary>
    /// �d�݃O���b�h����ł��K�؂ȃV�[�h�ʒu��I������ ,1-2
    /// </summary>
    private Vector2Int? SelectBestSeedPosition(float[,] weightGrid)
    {
        float maxWeight = -1f; // �ő�̏d�݂��L�^����ϐ��B�����l��-1f�B
        List<Vector2Int> bestPositions = new List<Vector2Int>(); // �ő�d�݂����ʒu�̌�⃊�X�g�B

        // �O���b�h�S�̂𑖍����čœK�Ȉʒu��T���܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // �Z�����z�u�\�i_grid[x,y]��-1�j���A�d�݂��v�Z�ς݁iweightGrid[x,y]��0�ȏ�j�̏ꍇ�̂ݑΏۂƂ��܂��B
                if (_grid[x, y] == -1 && weightGrid[x, y] >= 0)
                {
                    // ���݂̃Z���̏d�݂��A����܂ł̍ő�d�݂��傫���ꍇ�B
                    if (weightGrid[x, y] > maxWeight)
                    {
                        maxWeight = weightGrid[x, y]; // �ő�d�݂��X�V�B
                        bestPositions.Clear(); // ��⃊�X�g���N���A�B
                        bestPositions.Add(new Vector2Int(x, y)); // ���݂̈ʒu��B��̌��Ƃ��Ēǉ��B
                    }
                    // ���݂̃Z���̏d�݂��A����܂ł̍ő�d�݂Ɠ����ꍇ�B
                    else if (weightGrid[x, y] == maxWeight)
                    {
                        bestPositions.Add(new Vector2Int(x, y)); // ���݂̈ʒu�����ɒǉ��B
                    }
                }
            }
        }
        // �œK�Ȉʒu�̌�₪���������ꍇ�B
        if (bestPositions.Count > 0)
        {
            // �ō��d�݂̌�₪��������΃����_����1��I�����ĕԂ��܂��B
            return bestPositions[_random.Next(bestPositions.Count)];
        }
        else
        {
            // �K�؂Ȉʒu��������Ȃ������ꍇ��null��Ԃ��܂��B
            return null;
        }
    }

    /// <summary>
    /// �z�u���ꂽ�V�[�h�̎��͂̃Z���̏d�݂��A���̕����̃O���b�h�ŉ�����֐��B1-3
    /// </summary>
    /// <param name="weightGrids">�S���̏d�݃O���b�h���i�[���ꂽ�����B</param>
    /// <param name="seedPos">�z�u���ꂽ�V�[�h�̈ʒu�B</param>
    /// <param name="placedRoomId">�z�u���ꂽ������ID�B</param>
    private void UpdateWeightsAroundSeed(Dictionary<int, float[,]> weightGrids, Vector2Int seedPos, int placedRoomId)
    {
        // --- 1. ���̓v���Z�X (Attraction Process) ---
        // ��ɁA�אڂ��K�v�ȕ����Ɉ��͂�K�p���܂��B
        int inclusionRadius = settings.SeedInclusionRadius;
        float attractionWeight = settings.SeedPlacementAdjacencyBonus;

        // �אڂ��K�v�ȕ����̏d�݃}�b�v�݂̂��X�V
        foreach (var kvp in weightGrids)
        {
            int otherRoomId = kvp.Key;
            if (otherRoomId == placedRoomId) continue;

            // _ConnectivityGraph ���g���ėאڊ֌W���m�F
            if (_ConnectivityGraph.ContainsEdge(placedRoomId, otherRoomId))
            {
                // ���͔��a�͈̔͂𑖍�
                for (int dx = -inclusionRadius; dx <= inclusionRadius; dx++)
                {
                    for (int dy = -inclusionRadius; dy <= inclusionRadius; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                        if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                        {
                            // �L���ȃZ���i�z�u�\�j�ł���΁A�d�݂����Z
                            if (_grid[pos.x, pos.y] == -1)
                            {
                                kvp.Value[pos.x, pos.y] += attractionWeight;
                            }
                        }
                    }
                }
            }
        }

        // --- 2. �˗̓v���Z�X (Repulsion Process) ---
        // ���ɁA�S�Ă̑������ɑ΂��āA�˗͂�K�p���܂��B
        // ����ɂ��A���͔͈͂Ɛ˗͔͈͂��d�Ȃ�ꍇ�A�˗͂��D��i�㏑���j����܂��B
        int exclusionRadius = settings.SeedExclusionRadius;
        for (int dx = -exclusionRadius; dx <= exclusionRadius; dx++)
        {
            for (int dy = -exclusionRadius; dy <= exclusionRadius; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                {
                    foreach (var kvp in weightGrids)
                    {
                        if (kvp.Key != placedRoomId)
                        {
                            kvp.Value[pos.x, pos.y] = float.MinValue;
                        }
                    }
                }
            }
        }
        Debug.Log($"Updated weights around {seedPos} for other rooms.");
    }

    /// <summary>
    /// �z�u���ꂽ�V�[�h���A2�~2�̕����Ɋg������֐��B1-4
    /// �g���ɐ��������ꍇ�A_grid[,]�̍X�V�Aroom.Bounds,room.currentsize,room.InitialSeedPosition�̍X�V���s���B
    /// </summary>
    private void ExpandTo22(float[,] distancetowall)
    {
        // _roomDefinitions��ID�̏����ɕ��בւ��āA�e�������������܂��B
        foreach (var room in _roomDefinitions.OrderBy(r => r.ID))
        {
            // InitialSeedPosition��Vector2Int�^�ŁA�����̒��S�ł͂Ȃ��A2x2�̕����̍����̊J�n�_�Ƃ��Ĉ����܂��B
            int startX = room.InitialSeedPosition.Value.x;
            int startY = room.InitialSeedPosition.Value.y;

            int bestOffsetX = 0;
            int bestOffsetY = 0;
            float maxDistanceSum = -1f; // �ǂ܂ł̋����̍��v�̍ő�l

            // ���ƂȂ�2x2�̕����̍����̊J�n�_��4�p�^�[�������܂�
            // currentSeedPosition��2x2�̕����̂ǂ����ɑ����Ă���Ƃ����O��ŁA����2x2�̍����̍��W��T��
            bool canExpandTo22 = false;
            for (int offsetX = -1; offsetX <= 0; offsetX++) // X�����̃I�t�Z�b�g�i-1�܂���0�j
            {
                for (int offsetY = -1; offsetY <= 0; offsetY++) // Y�����̃I�t�Z�b�g�i-1�܂���0�j
                {
                    int currentRoomStartX = startX + offsetX;
                    int currentRoomStartY = startY + offsetY;

                    // 2x2�̕������O���b�h�͈͓̔��Ɏ��܂邩���m�F
                    if (currentRoomStartX >= 0 && currentRoomStartY >= 0 &&
                        currentRoomStartX + 1 < _gridSize.x && currentRoomStartY + 1 < _gridSize.y)
                    {
                        float currentDistanceSum = 0f;
                        bool canPlaceRoom = true;

                        // 2x2�̊e�}�X�ɂ��ĕ]��
                        for (int x = 0; x < 2; x++)
                        {
                            for (int y = 0; y < 2; y++)
                            {
                                int targetX = currentRoomStartX + x;
                                int targetY = currentRoomStartY + y;

                                // �����̔z�u���\���i��: ���ɑ��̕����Ő�L����Ă��Ȃ����A�ǂłȂ���
                                if (_grid[targetX, targetY] != -1 && _grid[targetX, targetY] != room.ID)
                                {
                                    canPlaceRoom = false; // �O���b�h�͈͊O
                                    break;
                                }
                                currentDistanceSum += distancetowall[targetX, targetY];
                            }
                            if (!canPlaceRoom) break;
                        }

                        // �z�u�\�ŁA������܂ł̍ő勗�����v�����傫���ꍇ�A�œK�Ȉʒu���X�V
                        if (canPlaceRoom && currentDistanceSum > maxDistanceSum)
                        {
                            canExpandTo22 = true;//��x�ł�2*2�Ɋg���ł���g�ݍ��킹���������true�ɂȂ�
                            maxDistanceSum = currentDistanceSum;
                            bestOffsetX = offsetX;
                            bestOffsetY = offsetY;
                        }
                    }
                }
            }
            //�g���ł���Ȃ�A �œK��2x2�̕����̊J�n�_�����肵�A_grid[,]�Aroom.Bounds,room.currentsize,room.InitialSeedPosition�̍X�V
            if (canExpandTo22)
            {
                room.InitialSeedPosition = new Vector2Int(startX + bestOffsetX, startY + bestOffsetY);
                for (int x = 0;x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        int posx = room.InitialSeedPosition.Value.x + x;
                        int posy = room.InitialSeedPosition.Value.y + y;
                        _grid[posx, posy] = room.ID;
                    }
                }
                room.CurrentSize = 4;
                room.Bounds = new RectInt(room.InitialSeedPosition.Value.x, room.InitialSeedPosition.Value.y, 2, 2);
            }
            else Debug.Log($" failed to Expand room{room.ID} To22");
        }
    }
    /// <summary>
    /// PlaceInitialSeeds�����s�����ꍇ�ȂǂɃO���b�h�ƃV�[�h����������Ԃɖ߂�,1-5
    /// </summary>
    private void ResetGridAndSeeds()
    {
        InitializeGrid(); // �O���b�h��-1�i�����蓖�Ĕz�u�\�j��0�i�z�u�s�j�̏�Ԃɖ߂��܂��B
        // �S�Ă̕�����`�ɂ��āA�����V�[�h�ʒu�ƌ��݂̃T�C�Y�����Z�b�g���܂��B
        foreach (var room in _roomDefinitions)
        {
            room.InitialSeedPosition = null; // �����V�[�h�ʒu��null�ɐݒ�B
            room.CurrentSize = 0; // ���݂̃T�C�Y��0�ɐݒ�B
            // Bounds�i���E���j�����Z�b�g���K�v�ȏꍇ�͂����ōs���܂��B
        }
        Debug.Log("Grid and seeds reset."); // ���Z�b�g�����̃��O���o�͂��܂��B
    }
}