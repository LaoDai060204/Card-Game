--created by assaultcore
Debug.SetAIName("小丑与锁鸟")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(94145021 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)

Debug.AddCard(30922149 ,0,0,LOCATION_SZONE,2,POS_FACEDOWN_ATTACK)

Debug.AddCard(94145021,1,1,LOCATION_DECK,0,POS_FACEDOWN_ATTACK)
Debug.AddCard(94145021,1,1,LOCATION_DECK,1,POS_FACEDOWN_ATTACK)
Debug.AddCard(94145021,1,1,LOCATION_DECK,2,POS_FACEDOWN_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()