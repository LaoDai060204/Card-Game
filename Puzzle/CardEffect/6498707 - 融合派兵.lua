--created by assaultcore
Debug.SetAIName("融合派兵")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(6498707 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(6498706 ,0,0,LOCATION_HAND,1,POS_FACEUP_ATTACK)
Debug.AddCard(68468459,0,0,LOCATION_HAND,2,POS_FACEUP_ATTACK)
Debug.AddCard(44146295 ,0,0,LOCATION_EXTRA,0,POS_FACEUP_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()