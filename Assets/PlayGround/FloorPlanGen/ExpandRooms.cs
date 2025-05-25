using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --- �X�e�b�v2: ExpandRooms �Ƃ��̕⏕�֐� ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �������g�����郁�C���̊֐��B�����̃t�F�[�Y�ō\������܂��B
    /// </summary>
    /// <param name="roomsToPlace">�z�u����ъg���Ώۂ̕�����`�̃��X�g�B</param>
    /// <returns>�g�������������ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool ExpandRooms(List<RoomDefinition> roomsToPlace) // roomsToPlace�͌��ݒ��ڎg�p����Ă��܂��񂪁A�����I�Ȋg���̂��߂Ɏc���Ă��܂��B
    {
        Debug.Log("Expanding rooms..."); // ���O�F�����g�������̊J�n�B

        // --- �t�F�[�Y1: ��`�g�� (Rectangular Growth) ---
        // �g���\�ȕ�����ID���i�[����HashSet���쐬���A_roomDefinitions����ID���擾���ď��������܂��B
        HashSet<int> availableRoomsPhase1 = new HashSet<int>(_roomDefinitions.Values.Select(r => r.ID));
        int totalPlaceable = _totalPlaceableCells; // �S�̂̔z�u�\�Z�������擾���܂��B
        int cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // �����V�[�h�ɂ���Ċ��Ɋ��蓖�Ă��Ă���Z�������v�Z���܂��B
        int maxIterationsPhase1 = totalPlaceable * 2; // �������[�v��h�~���邽�߂̍ő�C�e���[�V�����񐔂�ݒ肵�܂��B
        int iterationPhase1 = 0; // ���݂̃C�e���[�V�����񐔂����������܂��B


        // �g���\�ȕ��������݂��A�����蓖�Ă�ꂽ�Z�������S�z�u�\�Z���������ŁA
        // ����ɍő�C�e���[�V�����񐔂ɒB���Ă��Ȃ��ԁA���[�v�𑱂��܂��B
        while (availableRoomsPhase1.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase1 < maxIterationsPhase1)
        {
            iterationPhase1++; // �C�e���[�V�����񐔂��C���N�������g���܂��B
            // �ʐϔ䗦���l�����āA���Ɋg�����镔����I�����܂��B
            int roomId = SelectRoomToExpand(availableRoomsPhase1, true);
            if (roomId == -1) break; // �I���ł��镔�����Ȃ��ꍇ�̓��[�v���I�����܂��B

            // �I�����ꂽ�����ɑ΂��ċ�`�g�������݂܂��B
            bool grew = GrowRect(roomId);

            if (!grew) // �������g������Ȃ������ꍇ
            {
                availableRoomsPhase1.Remove(roomId); // ���̕����͂���ȏ��`�g���ł��Ȃ����߁A��₩�珜�O���܂��B
            }
            else // �������g�����ꂽ�ꍇ
            {
                // �g���ɐ��������ꍇ�A�ēx�g���̌��ɓ���邱�ƂŁA�A���I�Ȋg���𑣂��܂��B
                // (�������A�������[�v�ɂȂ�Ȃ��悤�ɒ��ӂ��K�v�BSelectRoomToExpand�̊m���I�I���ł�����x�ɘa����܂�)
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�������X�V���܂��B
                                                                             // Debug.Log($"Phase 1, Iter {iterationPhase1}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}"); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j�B
        }
        if (iterationPhase1 >= maxIterationsPhase1) Debug.LogWarning("Phase 1 reached max iterations."); // �ő�C�e���[�V�����񐔂ɒB�����ꍇ�͌x�����O���o�͂��܂��B


        // --- �t�F�[�Y2: L���^�g�� (L-Shape Growth) ---
        // �t�F�[�Y1�Ɠ��l�ɁA�g���\�ȕ�����ID���i�[����HashSet�����������܂��B
        HashSet<int> availableRoomsPhase2 = new HashSet<int>(_roomDefinitions.Values.Select(r => r.ID));
        int maxIterationsPhase2 = (totalPlaceable - cellsAssigned) * _roomDefinitions.Count; // �������[�v�h�~�p�̍ő�C�e���[�V�����񐔂�ݒ肵�܂��B���Ԗ��ߓI�ȏ����Ȃ̂ő��߂Ɍ��ς���܂��B
        int iterationPhase2 = 0; // ���݂̃C�e���[�V�����񐔂����������܂��B

        // �g���\�ȕ��������݂��A�����蓖�Ă�ꂽ�Z�������S�z�u�\�Z���������ŁA
        // ����ɍő�C�e���[�V�����񐔂ɒB���Ă��Ȃ��ԁA���[�v�𑱂��܂��B
        while (availableRoomsPhase2.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase2 < maxIterationsPhase2)
        {
            iterationPhase2++; // �C�e���[�V�����񐔂��C���N�������g���܂��B
            // �ʐϔ䗦�͂������܂�l�������A�g���\�ȕ�����D�悵�܂��H �ł͍ő�T�C�Y�l�������B
            // �䗦���l�������Ɏ��Ɋg�����镔����I�����܂��B
            int roomId = SelectRoomToExpand(availableRoomsPhase2, false);
            if (roomId == -1) break; // �I���ł��镔�����Ȃ��ꍇ�̓��[�v���I�����܂��B

            // �I�����ꂽ�����ɑ΂���L���^�g�������݂܂��B
            bool grew = GrowLShape(roomId);

            if (!grew) // �������g������Ȃ������ꍇ
            {
                availableRoomsPhase2.Remove(roomId); // ���̕����͂���ȏ�L���g���ł��Ȃ����߁A��₩�珜�O���܂��B
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�������X�V���܂��B
                                                                             // Debug.Log($"Phase 2, Iter {iterationPhase2}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}"); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j�B
        }
        if (iterationPhase2 >= maxIterationsPhase2) Debug.LogWarning("Phase 2 reached max iterations."); // �ő�C�e���[�V�����񐔂ɒB�����ꍇ�͌x�����O���o�͂��܂��B


        // --- �t�F�[�Y3: ���Ԗ��� (Fill Gaps) ---
        FillGaps(); // �c���Ă��錄�Ԃ𖄂߂鏈�����Ăяo���܂��B
        cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�������ēx�X�V���܂��B


        // �ŏI�`�F�b�N: �����蓖�ăZ�����Ȃ����m�F���܂��B
        bool allCellsFilled = AreAllCellsFilled();
        Debug.Log($"Room expansion finished. Assigned cells: {cellsAssigned}/{totalPlaceable}. All filled: {allCellsFilled}"); // ���O�F�����g�������̏I���ƌ��ʁB

        // �S�ẴZ�������܂�Ȃ������ꍇ�A���s�Ƃ݂Ȃ����A���e���邩�͗v������ł��B
        // if (!allCellsFilled) return false; // �Ⴆ�΁A�S�ẴZ�������܂�Ȃ��ꍇ�͎��s�Ƃ��邱�Ƃ��\�ł��B

        return true; // �����g�������̊����B
    }


    /// <summary>
    /// �g���t�F�[�Y�Ŏ��Ɋg�����镔����I������֐��B
    /// </summary>
    /// <param name="availableRoomIds">�g���\�ȕ���ID�̃Z�b�g�B</param>
    /// <param name="useSizeRatio">�ʐϔ䗦���l�����邩�ǂ����B</param>
    /// <returns>�I�����ꂽ����ID�B��₪�Ȃ����-1��Ԃ��B</returns>
    private int SelectRoomToExpand(HashSet<int> availableRoomIds, bool useSizeRatio)
    {
        if (availableRoomIds.Count == 0) return -1; // �g���\�ȕ������Ȃ��ꍇ��-1��Ԃ��܂��B

        // ���݂̃T�C�Y�ƖڕW�T�C�Y�̍��Ɋ�Â��đI������A�v���[�`���l�����܂��B
        // float GetPriority(int id) {
        //     RoomDefinition room = _roomDefinitions[id]; // ����ID���g���ĕ�����`���擾���܂��B
        //     float targetRatio = room.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio); // �ڕW�̖ʐϔ䗦���v�Z���܂��B
        //     float currentRatio = (float)room.CurrentSize / _totalPlaceableCells; // ���݂̖ʐϔ䗦���v�Z���܂��B
        //     return Mathf.Max(0, targetRatio - currentRatio); // �ڕW�ɒB���Ă��Ȃ��قǗD��x�������Ȃ�܂��i0�ȏ�j�B
        // }
        // var candidates = availableRoomIds.Select(id => new { id, priority = GetPriority(id) }).Where(x => x.priority > 0).ToList(); // �D��x��0���傫������I�����܂��B
        // if (candidates.Count == 0) return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count)); // �D��x0�̌�₵���Ȃ��ꍇ�̓����_���ɑI�����܂��B
        // float totalPriority = candidates.Sum(x => x.priority); // �S���̗D��x�̍��v���v�Z���܂��B
        // double randomValue = _random.NextDouble() * totalPriority; // 0����totalPriority�܂ł̃����_���Ȓl�𐶐����܂��B
        // float cumulative = 0; // �ݐϗD��x�����������܂��B
        // foreach (var candidate in candidates) { // �e���ɂ��ď������܂��B
        //      cumulative += candidate.priority; // �ݐϗD��x�Ɍ��݂̌��̗D��x�����Z���܂��B
        //      if (randomValue < cumulative) return candidate.id; // �����_���Ȓl���ݐϗD��x�����ɂȂ�����A���̌���ID��Ԃ��܂��B
        // }
        // return candidates.Last().id; // �t�H�[���o�b�N�Ƃ��čŌ�̌���ID��Ԃ��܂��B


        // �_�� �ɋ߂����� (�ʐϔ䗦�Ɋ�Â��m���I�I��)�B
        if (useSizeRatio) // �ʐϔ䗦���l������ꍇ�B
        {
            var candidates = availableRoomIds.ToList(); // �g���\�ȕ���ID�̃��X�g���쐬���܂��B
            // ���̕����̖ʐϔ䗦�̍��v���v�Z���܂��B
            float totalRatio = candidates.Sum(id => _roomDefinitions[id].SizeRatio); // id��RoomDefinition.ID�ł��B

            if (totalRatio <= 0) // �䗦�̍��v��0�ȉ��̏ꍇ�i�ʏ�͔������Ȃ��͂��j�B
            {
                // �䗦���Ȃ��ꍇ�̓����_���ɑI�����܂��B
                return candidates[_random.Next(candidates.Count)];
            }

            // 0����totalRatio�܂ł̃����_���Ȓl�𐶐����܂��B
            double randomValue = _random.NextDouble() * totalRatio;
            float cumulativeRatio = 0; // �ݐϔ䗦�����������܂��B

            // �e���̕����ɂ��ď������܂��B
            foreach (int roomId in candidates) // roomId��RoomDefinition.ID�ł��B
            {
                cumulativeRatio += _roomDefinitions[roomId].SizeRatio; // �ݐϔ䗦�Ɍ��݂̕����̖ʐϔ䗦�����Z���܂��B
                if (randomValue < cumulativeRatio) // �����_���Ȓl���ݐϔ䗦�����ɂȂ�����A���̕���ID��Ԃ��܂��B
                {
                    return roomId;
                }
            }
            return candidates.Last(); // �t�H�[���o�b�N�Ƃ��čŌ�̌���ID��Ԃ��܂��B
        }
        else // �ʐϔ䗦���l�����Ȃ��ꍇ�B
        {
            // �����_���ɑI�����܂��B
            return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count));
        }
    }

    /// <summary>
    /// �w�肳�ꂽ����ID�̕�������`�Ɋg�����悤�Ǝ��݂�֐��B
    /// </summary>
    /// <param name="roomId">�g���Ώۂ̕�����ID�B</param>
    /// <returns>�g���ɐ��������ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool GrowRect(int roomId) // roomId��RoomDefinition.ID�ł��B
    {
        // TODO: ����
        // 1. roomId�ɑ�����Z���ɗאڂ���󂫃Z��(-1)��T��
        // 2. �󂫃Z���𐅕��E���������́u���C���v�Ƃ��ăO���[�v��
        // 3. �e���C���ɂ��āA�g�������ꍇ�ɕ�������`���ێ��ł��邩�`�F�b�N
        // 4. ��`���ێ��ł��郉�C���̒��ōł��������̂�I�� (��������΃����_��)
        // 5. �I���������C���̃Z����roomId�Ɋ��蓖�āA_grid��room.CurrentSize���X�V
        // 6. �ڕW�T�C�Y(����)�ɒB������g�����~�߂�I�v�V���� (�t�F�[�Y1�̂�)
        // 7. �g���ł�����true�A�ł��Ȃ����false
        return false; // �_�~�[�����ł��B���ۂ̃��W�b�N�ɒu�������Ă��������B
    }

    /// <summary>
    /// �w�肳�ꂽ����ID�̕�����L���^�Ɋg�����悤�Ǝ��݂�֐��B
    /// </summary>
    /// <param name="roomId">�g���Ώۂ̕�����ID�B</param>
    /// <returns>�g���ɐ��������ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool GrowLShape(int roomId) // roomId��RoomDefinition.ID�ł��B
    {
        // TODO: ����
        // 1. roomId�ɑ�����Z���ɗאڂ���󂫃Z��(-1)��T��
        // 2. �󂫃Z���𐅕��E���������́u���C���v�Ƃ��ăO���[�v��
        // 3. �e���C���ɂ��āA�g�������ꍇ��L���^�ɂȂ邩�A�܂��͊�����L���^���ێ����邩�`�F�b�N
        // 4. U���^�ɂȂ�Ȃ��悤�Ƀ`�F�b�N
        // 5. �g���\�ȃ��C���̒��ōł��������̂�I�� (��������΃����_��)
        // 6. �I���������C���̃Z����roomId�Ɋ��蓖�āA_grid��room.CurrentSize���X�V
        // 7. �g���ł�����true�A�ł��Ȃ����false
        return false; // �_�~�[�����ł��B���ۂ̃��W�b�N�ɒu�������Ă��������B
    }

    /// <summary>
    /// �O���b�h��̖����蓖�Ă̌��ԃZ���𖄂߂�֐��B
    /// </summary>
    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps..."); // ���O�F���Ԗ��ߏ����̊J�n�B
        List<Vector2Int> gaps = FindEmptyCells(); // �����蓖�ăZ���i���ԁj�̃��X�g���擾���܂��B
        int filledCount = 0; // ���߂�ꂽ�Z�������J�E���g����ϐ������������܂��B

        // �e�����蓖�ăZ���ɂ��ď������܂��B
        foreach (Vector2Int gapPos in gaps)
        {
            if (_grid[gapPos.x, gapPos.y] != -1) continue; // �����Z�������ɖ��܂��Ă���ꍇ�̓X�L�b�v���܂��i�ʏ�͋N���蓾�Ȃ��j�B

            Dictionary<int, int> neighborCounts = new Dictionary<int, int>(); // �אڂ��镔��ID�Ƃ��̏o���񐔂��i�[���鎫���B
            int maxCount = 0; // �אڂ��镔���̍ő�o���񐔁B
            int bestNeighborId = -1; // �ł������אڂ��Ă��镔����ID�B

            // �㉺���E�̗אڃZ�����`���܂��B
            Vector2Int[] neighbors = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            // �אڂ��镔��ID���J�E���g���܂��B
            foreach (var offset in neighbors)
            {
                Vector2Int nPos = gapPos + offset; // �אڃZ���̈ʒu���v�Z���܂��B
                // �אڃZ�����O���b�h�͈͓��ł��邩���m�F���܂��B
                if (nPos.x >= 0 && nPos.x < _gridSize.x && nPos.y >= 0 && nPos.y < _gridSize.y)
                {
                    int neighborId = _grid[nPos.x, nPos.y]; // �אڃZ���̕���ID���擾���܂��B
                    if (neighborId > 0) // �L���ȕ���ID�i0��-1�łȂ��j�̏ꍇ�B
                    {
                        if (!neighborCounts.ContainsKey(neighborId)) neighborCounts[neighborId] = 0; // ������ID���Ȃ���Ώ������B
                        neighborCounts[neighborId]++; // ID�̏o���񐔂��C���N�������g�B

                        if (neighborCounts[neighborId] > maxCount) // ���݂�ID�̏o���񐔂��ő�l���傫���ꍇ�B
                        {
                            maxCount = neighborCounts[neighborId]; // �ő�l���X�V�B
                            bestNeighborId = neighborId; // �œK�ȗאڕ���ID���X�V�B
                        }
                        // �����̏ꍇ�́A���ʐς̏�����������D�悷��Ȃǂ̃��[�����l�����܂��B
                        else if (neighborCounts[neighborId] == maxCount && bestNeighborId != -1)
                        {
                            // ��: �ʐϔ䗦������������D�� (���Ԃ͏������������z�����₷����������Ȃ��Ƃ�������)�B
                            // bestNeighborId�̕�����neighborId�̕����̖ʐϔ䗦���r���܂��B
                            if (_roomDefinitions[neighborId].SizeRatio < _roomDefinitions[bestNeighborId].SizeRatio)
                            {
                                bestNeighborId = neighborId; // ���ʐϔ䗦���������������œK�Ƃ��܂��B
                            }
                        }
                    }
                }
            }

            // �ł������אڂ��Ă��镔���Ɍ��݂̌��ԃZ�������蓖�Ă܂��B
            if (bestNeighborId != -1)
            {
                _grid[gapPos.x, gapPos.y] = bestNeighborId; // �O���b�h�ɕ���ID�����蓖�Ă܂��B
                if (_roomDefinitions.ContainsKey(bestNeighborId)) // �O�̂��߁A������`�����݂��邩�m�F�B
                {
                    _roomDefinitions[bestNeighborId].CurrentSize++; // ���蓖�Ă������̌��݂̃T�C�Y�𑝂₵�܂��B
                }
                filledCount++; // ���߂��Z�������C���N�������g���܂��B
            }
            else
            {
                Debug.LogWarning($"Could not fill gap at {gapPos} - no valid neighbors found."); // �K�؂ȗאڕ�����������Ȃ������ꍇ�̌x���B
            }
        }
        Debug.Log($"Filled {filledCount} gap cells."); // ���O�F���߂�ꂽ���ԃZ���̐��B
    }


    /// <summary>
    /// �O���b�h���ɖ����蓖�ăZ��(-1)���c���Ă��邩�m�F����֐��B
    /// </summary>
    /// <returns>�S�ẴZ�������蓖�čς݂ł����true�A�����łȂ����false�B</returns>
    private bool AreAllCellsFilled()
    {
        // �O���b�h�S�̂𑖍����܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // �����蓖�ăZ���i�l��-1�j�����������ꍇ�B
                {
                    return false; // �����蓖�ăZ�������݂��邽��false��Ԃ��܂��B
                }
            }
        }
        return true; // �S�ẴZ�������蓖�čς݁i-1�̃Z�������݂��Ȃ��j����true��Ԃ��܂��B
    }

    /// <summary>
    /// �O���b�h���̖����蓖�ăZ��(-1)�̃��X�g���擾����֐��B
    /// </summary>
    /// <returns>�����蓖�ăZ���̍��W���X�g�B</returns>
    private List<Vector2Int> FindEmptyCells()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>(); // �����蓖�ăZ���̍��W���i�[���郊�X�g�����������܂��B
        // �O���b�h�S�̂𑖍����܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1) // �����蓖�ăZ���i�l��-1�j�����������ꍇ�B
                {
                    emptyCells.Add(new Vector2Int(x, y)); // ���̍��W�����X�g�ɒǉ����܂��B
                }
            }
        }
        return emptyCells; // �����蓖�ăZ���̃��X�g��Ԃ��܂��B
    }
}