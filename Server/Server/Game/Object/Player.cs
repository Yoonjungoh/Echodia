using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game
{
    public class Player : GameObject
    {

        // key: 몬스터 TemplateId -> 해당 몬스터를 목표로 하는 킬 퀘스트 목록
        private Dictionary<int, List<KillQuestEntry>> _killQuestByTargetId = new Dictionary<int, List<KillQuestEntry>>();

        // 메모리에서만 변경되고 DB에 아직 반영 안 된 퀘스트 ID 목록 (로그아웃 시 일괄 저장)
        private HashSet<int> _dirtyQuestIds = new HashSet<int>();

        public Player()
        {
            Init();
        }

        public int PlayerId { get; set; }   // DB에 저장된 플레이어 고유 Id
        public ClientSession Session { get; set; }
        public AOIController AOI { get; set; }

        // 플레이어 정보 초기화
        public void Init()
        {
            ObjectType = GameObjectType.Player;
            Name = $"NameNull_Player_{ObjectState.ObjectId}";
            ObjectState.CreatureState = CreatureState.Idle;
            AOI = new AOIController(this);

            InitDbData();
        }

        public void Init(int playerId, string name)
        {
            ObjectType = GameObjectType.Player;
            Name = name;
            PlayerId = playerId;
            ObjectState.CreatureState = CreatureState.Idle;

            InitDbData();
        }

        // TODO JSON - PlayerType에 따른 stat 변경
        public void InitDbData()
        {
            if (ObjectState.Stat == null)
            {
                ObjectState.Stat = new Stat();
            }

            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb player = db.Players
                    .AsNoTracking()
                    .Where(p => p.PlayerDbId == PlayerId)
                    .FirstOrDefault();
                    
                if (player == null)
                    return;

                Level = player.Level;
                Exp = player.Exp;

                // TODO - DB에서 가져오기
                ObjectState.Stat.MaxHp = 100.0f;
                ObjectState.Stat.Hp = ObjectState.Stat.MaxHp;
                ObjectState.Stat.CommonAttackDamage = 30.0f;
                ObjectState.Stat.Defense = 0.0f;
                ObjectState.Stat.MoveSpeed = 7.0f;
                ObjectState.Stat.CommonAttackCoolTime = 2.0f;
                ObjectState.Stat.AttackRange = 10.0f;
                ObjectState.Stat.AttackHalfAngleDeg = 30.0f;
                ObjectState.Stat.AttackHeight = 10.0f;
            }

            LoadActiveQuests();
        }

        // 로그인 시 진행 중인 킬 퀘스트를 메모리에 로드하고 옵저버 인덱스 구성
        private void LoadActiveQuests()
        {
            _killQuestByTargetId.Clear();
            _dirtyQuestIds.Clear();

            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb playerDb = db.Players
                    .Include(p => p.Quests)
                    .AsNoTracking()
                    .FirstOrDefault(p => p.PlayerDbId == PlayerId);

                if (playerDb == null)
                    return;

                List<QuestObjectiveDefinitionMetaData> allObjectives = SpecDataManager.Instance.GetAllQuestObjectiveDefinition();

                foreach (QuestDb quest in playerDb.Quests)
                {
                    if (quest.Status != QuestStatus.Proceeding)
                        continue;

                    QuestObjectiveDefinitionMetaData objective = allObjectives.
                        FirstOrDefault(o =>
                            o.MainQuestId == quest.MainQuestId &&
                            o.SubQuestId == quest.SubQuestId &&
                            o.QuestObjectiveType == QuestObjectiveType.Kill);

                    if (objective == null)
                        continue;

                    if (!_killQuestByTargetId.TryGetValue(objective.TargetId, out List<KillQuestEntry> list))
                    {
                        list = new List<KillQuestEntry>();
                        _killQuestByTargetId[objective.TargetId] = list;
                    }
                    list.Add(
                        new KillQuestEntry
                        {
                            Quest = quest,
                            TargetCount = objective.RequiredCount
                        });
                }
            }
        }

        // 특정 몬스터 킬 시 해당 몬스터를 목표로 하는 퀘스트 진행도 업데이트
        public void OnMonsterKilled(int monsterTemplateId)
        {
            if (!_killQuestByTargetId.TryGetValue(monsterTemplateId, out List<KillQuestEntry> entries))
                return;

            List<KillQuestEntry> completedEntries = null;

            foreach (KillQuestEntry entry in entries)
            {
                // 현재 진행도 증가 (QuestDb.RequiredCount = 현재 킬 수)
                ++entry.Quest.RequiredCount;
                _dirtyQuestIds.Add(entry.Quest.QuestDbId);

                // TODO: S_QuestProgressUpdate 패킷 클라이언트에 전송

                // 퀘스트 완료 의미
                if (entry.Quest.RequiredCount >= entry.TargetCount)
                {
                    entry.Quest.Status = QuestStatus.Completed;
                    entry.Quest.ClearedDate = DateTime.UtcNow;

                    if (completedEntries == null)
                    {
                        completedEntries = new List<KillQuestEntry>();
                    }
                    completedEntries.Add(entry);

                    // TODO: S_QuestCompleted 패킷 클라이언트에 전송
                }
            }

            if (completedEntries != null)
            {
                foreach (KillQuestEntry entry in completedEntries)
                {
                    entries.Remove(entry);
                    _dirtyQuestIds.Remove(entry.Quest.QuestDbId); // 즉시 저장하므로 dirty 제거
                }
                List<QuestDb> completedQuests = completedEntries.Select(e => e.Quest).ToList();
                QuestManager.Instance.CompleteQuestsAsync(completedQuests);
            }
        }

        // 로그아웃 시 메모리에서만 변경된 퀘스트 진행도를 DB에 저장
        private void SaveDirtyQuests()
        {
            if (_dirtyQuestIds.Count == 0) 
                return;

            List<QuestDb> dirtyQuests = _killQuestByTargetId.Values
                .SelectMany(list => list)
                .Where(e => _dirtyQuestIds.Contains(e.Quest.QuestDbId))
                .Select(e => e.Quest)
                .ToList();

            _dirtyQuestIds.Clear();
            QuestManager.Instance.SaveQuestProgressAsync(dirtyQuests);
        }

        public override void OnDamaged(GameObject instigator, float damage)
        {
            base.OnDamaged(instigator, damage);

        }

        public void OnLeaveGame()
        {
            SaveDirtyQuests();
        }
    }
}
