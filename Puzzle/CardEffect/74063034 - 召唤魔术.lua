--created by assaultcore
Debug.SetAIName("召唤魔术")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,5)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(74063034,0,0,LOCATION_HAND,0,POS_FACEDOWN)
Debug.AddCard(74063035,0,0,LOCATION_HAND,0,POS_FACEDOWN)
Debug.AddCard(86120751,0,0,LOCATION_HAND,0,POS_FACEDOWN)
Debug.AddCard(89631139,0,0,LOCATION_HAND,0,POS_FACEDOWN)

Debug.AddCard(75286621,0,0,LOCATION_EXTRA,0,POS_FACEUP_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()