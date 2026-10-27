--created by assaultcore
Debug.SetAIName("王宫的敕命")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,2000,0,0)

Debug.AddCard(61740673 ,0,0,LOCATION_SZONE,1,POS_FACEDOWN_ATTACK)
Debug.AddCard(61740673 ,0,0,LOCATION_SZONE,2,POS_FACEDOWN_ATTACK)
Debug.AddCard(61740673 ,0,0,LOCATION_SZONE,3,POS_FACEDOWN_ATTACK)

Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()