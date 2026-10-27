--created by assaultcore
Debug.SetAIName("封印之黄金柜")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(75500286 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(75500286 ,0,0,LOCATION_HAND,1,POS_FACEUP_ATTACK)
Debug.AddCard(75500286 ,0,0,LOCATION_HAND,2,POS_FACEUP_ATTACK)

Debug.AddCard(75500286 ,0,0,LOCATION_DECK,0,POS_FACEUP_ATTACK)
Debug.AddCard(75500286 ,0,0,LOCATION_DECK,1,POS_FACEUP_ATTACK)
Debug.AddCard(75500286 ,0,0,LOCATION_DECK,2,POS_FACEUP_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()