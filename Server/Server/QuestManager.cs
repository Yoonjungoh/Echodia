using Google.Protobuf.Protocol;
using Newtonsoft;
using Newtonsoft.Json;
using Server.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static Server.Define;

namespace Server
{
    // TODO - JSON 파싱
    public class QuestManager
    {
        public static QuestManager Instance { get; } = new QuestManager();

        // 퀘스트 생성 조건 달성했는지 확인 후, 패킷 전송
        // 퀘스트 관련 패킷 유저에게 전송
        // 퀘스트 생성 조건 (레벨업, 퀘스트 클리어)
        public void Refresh(Player player)
        {
            
        }

        public bool CanClear(Player player)
        {

            return false;
        }

        // 항상 메인 퀘스트의 마지막 서브 퀘스트를 클리어하면 서브 퀘스트 마지막 보상과 같이 수령함
        private void GetMainQuestReward(Player player, int mainQuestId, int SubQuestId)
        {
            
        }
        
        private void GetSubQuestReward(Player player, int mainQuestId, int SubQuestId)
        {
            
        }
    }
}