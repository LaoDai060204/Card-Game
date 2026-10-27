--created by assaultcore
Debug.SetAIName("神之宣告")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,2000,0,0)

Debug.AddCard(41420027 ,1,1,LOCATION_SZONE,2,POS_FACEDOWN_ATTACK)

Debug.AddCard(47894537 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(41420027 ,0,0,LOCATION_SZONE,2,POS_FACEDOWN_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()