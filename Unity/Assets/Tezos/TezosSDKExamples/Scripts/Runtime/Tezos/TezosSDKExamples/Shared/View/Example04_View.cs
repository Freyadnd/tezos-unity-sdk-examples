using RMC.Core.UI;
using TezosSDKExamples.Scenes;
using UnityEngine;

#pragma warning disable CS1998
namespace TezosSDKExamples.View
{
    /// <summary>
    /// UI for <see cref="Example04_FA2Transfer"/>
    /// </summary>
    public class Example04_View : Scene_BaseView
    {
        //  Events ----------------------------------------

        //  Properties ------------------------------------
        public ButtonUI TransferButtonUI { get { return _transferButtonUI; } }

        //  Fields ----------------------------------------
        [Header("Child")]

        [SerializeField]
        private ButtonUI _transferButtonUI;

        //  Unity Methods  --------------------------------
        protected override async void Awake()
        {
            base.Awake();
        }

        protected override async void Start()
        {
            base.Start();
        }

        //  Methods ---------------------------------------

        //  Event Handlers --------------------------------
    }
}
