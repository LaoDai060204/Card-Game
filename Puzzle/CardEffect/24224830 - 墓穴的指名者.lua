--created by assaultcore
Debug.SetAIName("墓穴的指名者")
Debug.ReloadFieldBegin(DUEL_ATTACK_FIRST_TURN+DUEL_SIMPLE_AI,4) -- 4 = new master rule
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)

Debug.AddCard(24224830 ,0,0,LOCATION_HAND,0,POS_FACEUP_ATTACK)
Debug.AddCard(24224831 ,0,0,LOCATION_HAND,1,POS_FACEUP_ATTACK)

Debug.AddCard(23434538 ,1,1,LOCATION_HAND,1,POS_FACEUP_ATTACK)
Debug.AddCard(73642296 ,1,1,LOCATION_HAND,1,POS_FACEUP_ATTACK)


Debug.ReloadFieldEnd()
Debug.ShowHint("在这个回合取得胜利！")
aux.BeginPuzzle()