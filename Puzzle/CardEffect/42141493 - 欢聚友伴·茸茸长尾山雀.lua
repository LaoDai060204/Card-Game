--created by assaultcore
Debug.SetAIName("欢聚友伴·茸茸长尾山雀")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(42141493,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)

Debug.AddCard(89943723,0,0,LOCATION_DECK,0,POS_FACEUP_ATTACK)
Debug.AddCard(7394770,0,0,LOCATION_EXTRA,0,POS_FACEUP_ATTACK)

Debug.AddCard(89943723,1,1,LOCATION_DECK,0,POS_FACEUP_ATTACK)
Debug.AddCard(7394770,1,1,LOCATION_EXTRA,0,POS_FACEUP_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()