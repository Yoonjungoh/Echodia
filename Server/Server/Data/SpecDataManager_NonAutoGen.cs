using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf.Protocol;

// 퀘스트의 메인Id랑 서브Id를 이용해 퀘스틑 찾아오게 하기

namespace Server.Data
{
    public partial class SpecDataManager
    {
        private Dictionary<ValueTuple<int, int>, QuestObjectiveDefinitionMetaData> _questObjectiveDefinitions = new Dictionary<(int, int), QuestObjectiveDefinitionMetaData>();

        public async Task Init()
        {
            await DownloadDataSheet();

            LoadData();
        }

        private void LoadData()
        {
            // 로드된 리스트를 순회하며 미리 딕셔너리(캐시) 완성
            var list = GetAllQuestObjectiveDefinition();
            int count = list.Count;
            foreach (var quest in list)
            {
                _questObjectiveDefinitions[(quest.MainQuestId, quest.SubQuestId)] = quest;
            }
        }

        public QuestObjectiveDefinitionMetaData GetQuestObjectiveDefinition(int mainQuestId, int subQuestId)
        {
            _questObjectiveDefinitions.TryGetValue((mainQuestId, subQuestId), out var quest);
            return quest;
        }
    }
}