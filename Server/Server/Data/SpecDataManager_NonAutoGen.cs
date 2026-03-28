using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf.Protocol;

namespace Server.Data
{
    public partial class SpecDataManager
    {
        private Dictionary<ValueTuple<int, int>, QuestObjectiveDefinitionMetaData> _questObjectiveDefinitions = new Dictionary<(int, int), QuestObjectiveDefinitionMetaData>();
        private Dictionary<int, QuestDefinitionMetaData> _questDefinitionByMainQuestId = new Dictionary<int, QuestDefinitionMetaData>();

        public async Task Init()
        {
            await DownloadDataSheet();

            LoadData();
        }

        private void LoadData()
        {
            var objectiveList = GetAllQuestObjectiveDefinition();
            foreach (var quest in objectiveList)
                _questObjectiveDefinitions[(quest.MainQuestId, quest.SubQuestId)] = quest;

            var definitionList = GetAllQuestDefinition();
            foreach (var def in definitionList)
                _questDefinitionByMainQuestId[def.MainQuestId] = def;
        }

        public QuestObjectiveDefinitionMetaData GetQuestObjectiveDefinition(int mainQuestId, int subQuestId)
        {
            _questObjectiveDefinitions.TryGetValue((mainQuestId, subQuestId), out var quest);
            return quest;
        }

        public int GetExpRequiredForLevel(int level)
        {
            ExpMetaData entry = GetAllExp().FirstOrDefault(x => x.Level == level);
            return entry != null ? (int)entry.RequiredExp : 0;
        }
    }
}