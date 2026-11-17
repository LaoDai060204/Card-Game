--created by assaultcore
Debug.SetAIName("无限泡影")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(10045474 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(10045474 ,0,0,LOCATION_SZONE,2,POS_FACEDOWN_ATTACK)

Debug.AddCard(83965310 ,1,1,LOCATION_MZONE,2,POS_FACEUP_ATTACK)
Debug.AddCard(83965311 ,1,1,LOCATION_MZONE,3,POS_FACEUP_ATTACK)
Debug.AddCard(58921041 ,1,1,LOCATION_SZONE,2,POS_FACEUP_ATTACK)
Debug.AddCard(58921041 ,1,1,LOCATION_SZONE,3,POS_FACEUP_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()