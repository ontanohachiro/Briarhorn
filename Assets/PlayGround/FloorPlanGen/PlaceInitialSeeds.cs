using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �X�e�b�v1: �����̏����V�[�h�ʒu��z�u
    /// </summary>
    private bool PlaceInitialSeeds(List<RoomDefinition> roomsToPlace) 
    {
        Debug.Log("Placing initial seeds...");
        // �e������ID���L�[�Ƃ����d�݃}�b�v�̎��������������܂��B
        Dictionary<int, float[,]> weightGrids = InitializeWeightGrids();
        // �ǂ܂ł̋��������O�Ɍv�Z���܂��B����͑S�����ʂ̏d�݌v�Z�Ɏg�p����܂��B
        float[,] _distanceToWall = CalculateDistanceToWall();

        if (todebug == ToDebug.CalculateDistanceToWall)
        {
            MVinstance.Execute(_distanceToWall);
        }

        // �e�����̃V�[�h��z�u���܂��B
        // ID���Ŕz�u���邱�ƂŁA�Č����̂��錋�ʂ𓾂₷�����܂��B
        // �z�u���������ʂɉe����^����\�������邽�߁A�����_���������̏����i��F�傫�ȕ�������j�������\�ł��B
        foreach (var room in _roomDefinitions.Values.OrderBy(r => r.ID)) // RoomDefinition��ID�v���p�e�B�Ń\�[�g���ď������܂��B
        {
            // 1. ���̕����iroom�j�p�̏d�݃O���b�h���v�Z���܂��B
            // CalculateWeightsForRoom���\�b�h�́A�w�肳�ꂽroom.ID�Ɋ�Â��āA�Ή�����weightGrids���̏d�݃}�b�v���X�V���܂��B
            CalculateWeightsForRoom(room.ID, weightGrids, _distanceToWall);
            // 2. �v�Z���ꂽ�d�݃}�b�v�iweightGrids[room.ID]�j�Ɋ�Â��āA�œK�ȃV�[�h�ʒu��I�����܂��B
            Vector2Int? seedPos = SelectBestSeedPosition(weightGrids[room.ID]);

            if (seedPos.HasValue)
            {
                room.InitialSeedPosition = seedPos.Value; // �I�����ꂽ�ʒu�𕔉��̏����V�[�h�ʒu�Ƃ��ċL�^���܂��B
                _grid[seedPos.Value.x, seedPos.Value.y] = room.ID; // �O���b�h��̑Ή�����Z���ɕ�����ID�����蓖�Ă܂��B
                room.CurrentSize = 1; // �V�[�h�Z�����z�u���ꂽ�̂ŁA�����̌��݂̃T�C�Y��1�Ƃ��܂��B
                Debug.Log($"Placed seed for room {room.ID} ({room.Type}) at {seedPos.Value}");


                // 3. �z�u�����V�[�h�Z���̎��͂̏d�݂��A���̕����̏d�݃O���b�h�ŉ����܂��B
                // ����ɂ��A���̕����̃V�[�h���߂�����ʒu�ɔz�u�����̂�h���܂��B
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
        foreach (var room in _roomDefinitions.Values) // RoomDefinition��ID���L�[�Ƃ��Ďg�p���܂��B
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
        // �Ώۂ̕����̒�`�����擾���܂��B
        RoomDefinition targetRoom = _roomDefinitions[targetRoomId];

        // �ڕW�ʐς���u���z�I�ȁv�ǂ���̋����𐄒肵�܂��B
        // �S�z�u�\�Z�����ƕ����̃T�C�Y�䗦����A���̕�������߂�ׂ������悻�̃Z�������v�Z���܂��B
        float estimatedTargetCells = _totalPlaceableCells * (targetRoom.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio));
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
                    currentWeight *= Mathf.Clamp01(dist / idealDistanceFromWall);
                    // ��: �K�E�V�A���I�ȕ��z (idealDistance�ōő�l1�ƂȂ�悤��)�B
                    // currentWeight *= Mathf.Exp(-(dist - idealDistanceFromWall) * (dist - idealDistanceFromWall));
                }
                else // �ǂ��瓞�B�s�\�A�܂��̓O���b�h�O�����̏ꍇ�B
                {
                    currentWeight = 0f; // �O�̂��߁iInitializeGrid��0�ɂȂ��Ă���͂��ł����A�����ł�0�ɂ��܂��j�B
                }


                // 3. �אڐ���Ɋ�Â��ďd�݂𒲐����܂��B
                // targetRoom���ڑ�����iConnectivityConstraints�j�������Ă���ꍇ�B
                if (targetRoom.ConnectivityConstraints != null)
                {
                    foreach (int constraintRoomId in targetRoom.ConnectivityConstraints)
                    {
                        // �ڑ����ׂ�����̕���(constraintRoomId)�����݂��A�����̏����V�[�h�ʒu�����Ɍ��肵�Ă���ꍇ�B
                        // ���ݐڑ�����̏ꍇ�A���葤�ɂ����񂪂��邩�m�F���������ǂ��ł����A�����ł͕Е����ōl�����܂��B
                        if (_roomDefinitions.ContainsKey(constraintRoomId) && _roomDefinitions[constraintRoomId].InitialSeedPosition.HasValue)
                        {
                            Vector2Int neighborSeedPos = _roomDefinitions[constraintRoomId].InitialSeedPosition.Value;
                            // �אڕ����̃V�[�h�ʒu����̋������v�Z���܂��B
                            float distanceToNeighborSeed = Vector2Int.Distance(new Vector2Int(x, y), neighborSeedPos);
                            // ��: ���͈͓��iidealDistanceFromWall�����j�Ȃ�{�[�i�X�����Z���܂��B�͈͂͒����\�ł��B
                            if (distanceToNeighborSeed < idealDistanceFromWall)
                            {
                                // �������߂��قǑ傫�ȃ{�[�i�X�����Z���܂��B
                                currentWeight += settings.SeedPlacementAdjacencyBonus * (1.0f - Mathf.Clamp01(distanceToNeighborSeed / idealDistanceFromWall));
                            }
                        }
                    }
                }
                // �v�Z���ꂽ�d�݂��i�[���܂��B�d�݂͕��ɂȂ�Ȃ��悤�ɂ��܂��i���݂̓R�����g�A�E�g�j�B
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
        // �d�݂�������͈͂̔��a��ݒ肩��擾���܂��B
        int radius = settings.SeedExclusionRadius;

        // �w�肳�ꂽ���a���̐����`�̈�𑖍����܂��B
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                // �~�`�͈̔͂ɂ���ꍇ�́A�ȉ��̃R�����g�A�E�g���������A������ǉ����܂��B
                // if (dx * dx + dy * dy > radius * radius) continue;

                // �V�[�h�ʒu����̑��Έʒu���v�Z���܂��B
                Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                // �v�Z���ꂽ�ʒu���O���b�h�͈͓��ł��邩���m�F���܂��B
                if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                {
                    // ���̑S�Ă̕����i�z�u���ꂽ�������g�������j�̏d�݃O���b�h�ɑ΂��ď������s���܂��B
                    foreach (var kvp in weightGrids)
                    {
                        int otherRoomId = kvp.Key; // ���̕�����ID�B
                        // �z�u���ꂽ�������g�̃O���b�h�͕ύX���܂���B
                        if (otherRoomId != placedRoomId)
                        {
                            // ���̕����̏d�݃}�b�v�ɂ����āA�w��͈͓��̃Z���̏d�݂�float.MinValue�ɐݒ肵�A�����I�ɑI��s�ɂ��܂��B
                            kvp.Value[pos.x, pos.y] = float.MinValue;
                        }
                    }
                }
            }
        }
        // Debug.Log($"Updated weights around {seedPos} for other rooms."); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j
    }


    /// <summary>
    /// PlaceInitialSeeds�����s�����ꍇ�ȂǂɃO���b�h�ƃV�[�h����������Ԃɖ߂�,1-4
    /// </summary>
    private void ResetGridAndSeeds()
    {
        InitializeGrid(); // �O���b�h��-1�i�����蓖�Ĕz�u�\�j��0�i�z�u�s�j�̏�Ԃɖ߂��܂��B
        // �S�Ă̕�����`�ɂ��āA�����V�[�h�ʒu�ƌ��݂̃T�C�Y�����Z�b�g���܂��B
        foreach (var room in _roomDefinitions.Values)
        {
            room.InitialSeedPosition = null; // �����V�[�h�ʒu��null�ɐݒ�B
            room.CurrentSize = 0; // ���݂̃T�C�Y��0�ɐݒ�B
            // Bounds�i���E���j�����Z�b�g���K�v�ȏꍇ�͂����ōs���܂��B
        }
        Debug.Log("Grid and seeds reset."); // ���Z�b�g�����̃��O���o�͂��܂��B
    }
}