using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace CardGame.UI
{
    public class DoWhenDisabled : MonoBehaviour
    {
        public Action action;

        private void OnDisable()
        {
            action?.Invoke();
        }
    }
}
